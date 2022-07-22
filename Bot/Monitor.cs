using CommandLine;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RedditSharp;
using System.Text.RegularExpressions;

namespace HadesBoonBot.Bot
{
    [Verb("bot", HelpText = "Run bot")]
    internal class BotOptions
    {
        [Option('c', "config", Required = true, HelpText = "Configuration file containing bot settings")]
        public string ConfigFile { get; set; }

        public BotOptions()
        {
            ConfigFile = string.Empty;
        }
    }

    internal class BotConfig
    {
        [Flags]
        internal enum ProcessMode
        {
            /// <summary>
            /// Output debug info on local machine
            /// </summary>
            LocalDebug = 1,

            /// <summary>
            /// DM the dev with a short summary
            /// </summary>
            PrivateMessage = 2,

            /// <summary>
            /// Comment on the post
            /// </summary>
            Comment = 4,

            /// <summary>
            /// Add to Github repo page
            /// </summary>
            GitHubPage = 8,
        }

        /// <summary>
        /// Bot state file for suspend/resume
        /// </summary>
        public string StateFile { get; set; }

        /// <summary>
        /// Which mode to process posts in
        /// </summary>
        public ProcessMode Mode { get; set; }

        /// <summary>
        /// Local folder for storing post images etc
        /// </summary>
        public string? HoldingArea { get; set; }

        /// <summary>
        /// Classifier details
        /// </summary>
        [JsonProperty("Classifier")]
        public ClassifierInfo? ClassifierOptions { get; set; }

        internal class ClassifierInfo
        {
            public string Type { get; set; }
            public Newtonsoft.Json.Linq.JObject? Options { get; set; }

            public ClassifierInfo()
            {
                Type = string.Empty;
            }
        }

        public BotConfig()
        {
            StateFile = string.Empty;
        }
    }

    internal class Monitor : IDisposable
    {
        readonly HttpClient m_client = new();
        readonly BotConfig m_runOptions;
        readonly IConfigurationRoot m_config;
        readonly List<ML.Model> m_models;

        public Monitor(BotOptions runConfig, IConfigurationRoot config, List<ML.Model> models)
        {
            string runOptions = File.ReadAllText(runConfig.ConfigFile);
            m_runOptions = JsonConvert.DeserializeObject<BotConfig>(runOptions);
            m_config = config;
            m_models = models;
        }

        internal async Task<int> Run(Codex codex)
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

            var (classifier, classifierOptions) = Classifiers.ClassifierFactory.Create(
                m_runOptions.ClassifierOptions!.Type, m_runOptions.ClassifierOptions!.Options, codex);

            var client = new Reddit(webAgent, true)
            {
                RateLimit = RateLimitMode.Pace
            };

            BotState state = File.Exists(m_runOptions.StateFile) ? BotState.FromFile(m_runOptions.StateFile) : new();

            void postAction(RedditSharp.Things.Post post)
            {
                if (!state.ProcessedPosts.ContainsKey(post.Id))
                {
                    Console.WriteLine($"Processing {post.Id}");
                    BotState.ProcessedPost? result = ProcessPost(client, post, classifier, classifierOptions);
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

        BotState.ProcessedPost? ProcessPost(Reddit client, RedditSharp.Things.Post post,
            Classifiers.BaseClassifier classifier, Classifiers.BaseClassifierOptions classifierOptions)
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

                var imageLinks = GetImageLinks(post);
                var images = new List<Classifiers.ClassifiedScreenMeta>();

                if (imageLinks.Any())
                {
                    string targetFolder = "screens";
                    if (!string.IsNullOrEmpty(m_runOptions.HoldingArea))
                    {
                        targetFolder = Path.Combine(m_runOptions.HoldingArea, targetFolder);
                    }

                    for (int i = 0; i < imageLinks.Count; i++)
                    {
                        string remotePath = imageLinks[i];

                        string ext = Path.GetExtension(new Uri(remotePath).LocalPath.ToString())[1..];
                        string targetFile = Path.Combine(targetFolder, $"{post.Id}_{i}.{ext}");
                        Util.DownloadFile(m_client, remotePath, targetFile);

                        images.Add(new(classifier.RunSingle(classifierOptions, targetFile, m_models, null), remotePath, targetFile));
                    }
                    
                    //todo extract method
                    if ((m_runOptions.Mode & BotConfig.ProcessMode.LocalDebug) == BotConfig.ProcessMode.LocalDebug)
                    {
                        int imgIdx = -1;
                        foreach (var img in images)
                        {
                            imgIdx++;
                            if (img.LocalSource != null && img.RemoteSource != null && img.Screen != null)
                            {
                                using OpenCvSharp.Mat screen = OpenCvSharp.Cv2.ImRead(img.LocalSource);

                                string debugFolder = "local_debug";
                                if (!string.IsNullOrEmpty(m_runOptions.HoldingArea))
                                {
                                    debugFolder = Path.Combine(m_runOptions.HoldingArea, debugFolder);
                                }

                                debugFolder = Util.CreateDir(Path.Combine(debugFolder, post.Id));

                                string ext = Path.GetExtension(img.LocalSource);
                                screen.SaveImage(Path.Combine(debugFolder, $"{post.Id}_{imgIdx}{ext}"));
                                ScreenMetadata meta = new(screen);

                                foreach (var slot in img.Screen.Slots)
                                {
                                    if (meta.TryGetTraitRect(slot.Col, slot.Row, out var getRect))
                                    {
                                        var rect = getRect!.Value;
                                        screen.DrawMarker(new OpenCvSharp.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2),
                                            OpenCvSharp.Scalar.White, OpenCvSharp.MarkerTypes.Diamond);
                                    }
                                }

                                foreach (var pinSlot in img.Screen.PinSlots)
                                {
                                    var rect = meta.GetPinRect(img.Screen.GetColumnCount(), pinSlot.Row).iconRect;
                                    screen.DrawMarker(new OpenCvSharp.Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2),
                                        OpenCvSharp.Scalar.White, OpenCvSharp.MarkerTypes.Diamond);
                                }

                                screen.SaveImage(Path.Combine(debugFolder, $"{post.Id}_{imgIdx}_debug{ext}"));
                                Console.WriteLine($"Ran local debug for image {img.RemoteSource}");
                            }
                            else
                            {
                                Console.Error.WriteLine("Local debug failed for image");
                            }
                        }
                    }

                    if ((m_runOptions.Mode & BotConfig.ProcessMode.PrivateMessage) == BotConfig.ProcessMode.PrivateMessage)
                    {
                        List<string> msgLines = new();
                        foreach (var img in images.OrderBy(i => i.RemoteSource))
                        {
                            msgLines.Add($"{img.RemoteSource}, valid: {img.Screen?.IsValid == true}");
                        }

                        client.ComposePrivateMessageAsync(
                            $"Victory screen {post.Id}",
                            string.Join("\n\n", new[] { $"New victory screen posted by /u/{post.AuthorName}: {post.Permalink}" }.Concat(msgLines)),
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
