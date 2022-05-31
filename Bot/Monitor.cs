using CommandLine;
using Microsoft.Extensions.Configuration;
using RedditSharp;
using System.Text.RegularExpressions;

namespace HadesBoonBot.Bot
{
    [Verb("bot", HelpText = "Run bot")]
    internal class BotOptions
    {
        [Flags]
        internal enum ProcessMode
        {
            /// <summary>
            /// DM the dev with a short summary
            /// </summary>
            PrivateMessage = 1,

            /// <summary>
            /// Comment on the post
            /// </summary>
            Comment = 2,

            /// <summary>
            /// Add to Github repo page
            /// </summary>
            GitHubPage = 4,
        }

        [Option('s', "state_file", Required = true, HelpText = "Bot state file for suspend/resume")]
        public string StateFile { get; set; }

        [Option('m', "mode", Required = true, HelpText = "Which mode to process posts in")]
        public ProcessMode Mode { get; set; }

        [Option('h', "holding_area", Required = false, HelpText = "Local folder for storing post images etc")]
        public string? HoldingArea { get; set; }

        public BotOptions()
        {
            StateFile = string.Empty;
        }
    }

    internal class Monitor : IDisposable
    {
        readonly HttpClient m_client = new();
        readonly BotOptions m_runOptions;
        readonly IConfigurationRoot m_config;

        public Monitor(BotOptions runOptions, IConfigurationRoot config)
        {
            m_runOptions = runOptions;
            m_config = config;
        }

        internal async Task<int> Run()
        {
            //load creds
            var webAgent = new BotWebAgent(
                m_config["reddit-user"],
                m_config["reddit-pass"],
                m_config["reddit-client-id"],
                m_config["reddit-client-secret"],
                "http://localhost:8080"
                )
            {
                UserAgent = $"User-Agent: {m_config["reddit-user-agent"]}"
            };

            var client = new Reddit(webAgent, true);
            BotState state = File.Exists(m_runOptions.StateFile) ? BotState.FromFile(m_runOptions.StateFile) : new();

            void postAction(RedditSharp.Things.Post post)
            {
                if (!state.ProcessedPosts.ContainsKey(post.Id))
                {
                    Console.WriteLine($"Processing {post.Id}");
                    BotState.ProcessedPost? result = ProcessPost(client, post);
                    state.ProcessedPosts.Add(post.Id, result);
                    state.Save(m_runOptions.StateFile);
                }
            }

            var subreddit = await client.GetSubredditAsync("/r/HadesTheGame");

            //first catch up on recent posts
            Console.WriteLine("Checking history...");
            var recentPosts = subreddit.GetPosts(RedditSharp.Things.Subreddit.Sort.New, 500);
            await recentPosts.ForEachAsync(postAction);

            //then subscribe to new ones
            var postStream = subreddit.GetPosts(RedditSharp.Things.Subreddit.Sort.New).Stream();
            postStream.Subscribe(postAction);

            //TODO maybe support cancellation lol
            Console.WriteLine("Waiting for posts...");
            await postStream.Enumerate(CancellationToken.None);

            return 0;
        }

        BotState.ProcessedPost? ProcessPost(Reddit client, RedditSharp.Things.Post post)
        {
            //is it flaired as an endgame screen?
            if (string.Compare(post.LinkFlairText, "victory screen", true) != 0)
            {
                return null;
            }
            else
            {
                Console.WriteLine($"Found victory screen post {post.Id} at {post.Permalink}");

                //TODO check we definitely haven't posted in this thread/already done requested action to avoid over-reliance on the state file
                //TODO proper logging

                var images = GetImageLinks(post);

                if (images.Any())
                {
                    string targetFolder = "screens";
                    if (!string.IsNullOrEmpty(m_runOptions.HoldingArea))
                    {
                        targetFolder = Path.Combine(m_runOptions.HoldingArea, targetFolder);
                    }

                    for (int i = 0; i < images.Count; i++)
                    {
                        string ext = Path.GetExtension(new Uri(images[i]).LocalPath.ToString())[1..];
                        string targetFile = Path.Combine(targetFolder, $"{post.Id}_{i}.{ext}");
                        Util.DownloadFile(m_client, images[i], targetFile);

                        //TODO: process here
                    }

                    if ((m_runOptions.Mode & BotOptions.ProcessMode.PrivateMessage) == BotOptions.ProcessMode.PrivateMessage)
                    {
                        client.ComposePrivateMessageAsync(
                            $"Victory screen {post.Id}",
                            string.Join("\n\n", new[] { $"New victory screen posted by /u/{post.AuthorName}: {post.Permalink}" }.Concat(images)),
                            m_config["reddit-dm-user"]).Wait();
                    }
                }
                else
                {
                    Console.Error.WriteLine($"Failed to find any images for {post.Id}");
                }
            }

            return null;
        }

        static bool IsImageFormat(string file)
        {
            Uri addr = new(file);
            string ext = Path.GetExtension(addr.AbsolutePath);

            HashSet<string> supportedExts = new()
            {
                ".png",
                ".jpg",
                ".jpeg",
                ".webp",
            };

            return supportedExts.Contains(ext);
        }

        static string FixJson(string input)
        {
            //rather than heading into Reddit# postfixing every request with ?raw_json=1
            return input
                .Replace("&gt;", ">")
                .Replace("&lt;", "<")
                .Replace("&amp;", "&")
                ;
        }

        static List<string> GetImageLinks(RedditSharp.Things.Post post)
        {
            List<string> links = new();

            //pull out any links from self posts
            if (post.IsSelfPost && !string.IsNullOrEmpty(post.SelfText))
            {
                var parseLinks = new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var mtch = parseLinks.Match(post.SelfText);

                while (mtch?.Success == true)
                {
                    if (IsImageFormat(mtch.Value))
                    {
                        links.Add(mtch.Value);
                    }

                    mtch = mtch.NextMatch();
                }
            }

            //use post json to find media links
            if (!links.Any())
            {
                try
                {
                    var parsed = post.RawJson.ToObject<RedditModels.Post>();
                    if (parsed?.Media?.Values != null)
                    {
                        foreach (var item in parsed.Media.Values)
                        {
                            if (IsImageFormat(item.Source.Url))
                            {
                                links.Add(item.Source.Url);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to parse post json: {ex}");
                }
            }

            //parse the previews too
            if (!links.Any())
            {
                var images = post.Preview?.Images;
                if (images != null)
                {
                    foreach (var image in images)
                    {
                        string url = image.Source.Url.ToString();
                        if (IsImageFormat(url)) //probably safe here though
                        {
                            links.Add(url);
                        }
                    }
                }
            }

            for (int i = 0; i < links.Count; i++)
            {
                links[i] = FixJson(links[i]);
            }

            return links;
        }

        public void Dispose()
        {
            m_client.Dispose();
        }
    }
}
