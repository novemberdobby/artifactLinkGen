﻿using CommandLine;
using HadesBoonBot.Classifiers;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Reddit;
using Reddit.Controllers;
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
        /// <summary>
        /// Bot state file for suspend/resume
        /// </summary>
        public string StateFile { get; set; }

        /// <summary>
        /// Process a specific post and do nothing else
        /// </summary>
        public string? ForceProcess { get; set; }

        /// <summary>
        /// Which modes to process posts in
        /// </summary>
        public List<ProcessMode> Modes { get; set; }

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

        internal class ProcessMode
        {
            public string Name { get; set; }
            public Newtonsoft.Json.Linq.JObject? Options { get; set; }

            public ProcessMode()
            {
                Name = string.Empty;
            }
        }

        public BotConfig()
        {
            StateFile = string.Empty;
            Modes = new();
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

        internal int Run(Codex codex)
        {
            //load creds
            var client = new RedditClient(
                appId: m_config["reddit-client-id"],
                refreshToken: m_config["reddit-client-refresh-token"],
                appSecret: m_config["reddit-client-secret"],
                userAgent: m_config["reddit-user-agent"]
                );

            var (classifier, classifierOptions) = ClassifierFactory.Create(
                m_runOptions.ClassifierOptions!.Type, m_runOptions.ClassifierOptions!.Options, codex);

            BotState state = File.Exists(m_runOptions.StateFile) ? BotState.FromFile(m_runOptions.StateFile) : new();
            if (m_runOptions.ForceProcess != null && state.ProcessedPosts.ContainsKey(m_runOptions.ForceProcess))
            {
                state.ProcessedPosts.Remove(m_runOptions.ForceProcess);
            }

            void postAction(Post post)
            {
                if (!state.ProcessedPosts.ContainsKey(post.Id))
                {
                    Console.WriteLine($"Processing {post.Id}");
                    BotState.ProcessedPost? result = ProcessPost(client, post, classifier, classifierOptions, codex);
                    state.ProcessedPosts.Add(post.Id, result);
                    state.Save(m_runOptions.StateFile);
                }
            }

            if (m_runOptions.ForceProcess != null)
            {
                var post = client.Post("t3_" + m_runOptions.ForceProcess).About();
                postAction(post);
            }
            else
            {
                var subreddit = client.Subreddit("HadesTheGame").About();

                //first catch up on recent posts
                Console.WriteLine("Checking history...");

                //we have to do a wee bit of pagination ourselves as the API doesn't (fortunately it still rate limits)
                int checkLatest = 500;
                List<Post> newestPosts = new(checkLatest);
                while (newestPosts.Count < checkLatest)
                {
                    var recentPosts = subreddit.Posts.GetNew(limit: checkLatest - newestPosts.Count, after: newestPosts.Any() ? newestPosts.Last().Fullname : string.Empty);
                    newestPosts.AddRange(recentPosts);
                }

                foreach (var post in newestPosts)
                {
                    postAction(post);
                }

                //then subscribe to new ones
                subreddit.Posts.NewUpdated += (_, updateArgs) =>
                {
                    foreach (var newPost in updateArgs.Added)
                    {
                        postAction(newPost);
                    }
                };

                Console.WriteLine("Waiting for posts...");
                subreddit.Posts.MonitorNew();

                //TODO maybe support cancellation lol
                while (subreddit.Posts.NewPostsIsMonitored())
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            }

            return 0;
        }

        BotState.ProcessedPost? ProcessPost(
            RedditClient client, Post post,
            BaseClassifier classifier, BaseClassifierOptions classifierOptions,
            Codex codex
            )
        {
            //is it flaired as an endgame screen?
            if (string.Compare(post.Listing.LinkFlairText, "victory screen", true) != 0)
            {
                return null;
            }
            else
            {
                Console.WriteLine($"Found victory screen post {post.Id} at {post.Permalink}");

                //TODO check we definitely haven't posted in this thread/already done requested action to avoid over-reliance on the state file
                //TODO proper logging
                
                var imageLinks = GetImageLinks(post);
                var images = new List<ClassifiedScreenMeta>();

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

                    //TODO go back through all existing posts and see if any fail to find images

                    var modes = m_runOptions.Modes.ToDictionary(x => x.Name);
                    foreach(Type procType in new[] { typeof(Processors.LocalDebug), typeof(Processors.WebPage), typeof(Processors.PrivateMessage) })
                    {
                        if (modes.TryGetValue(procType.Name, out var procMode))
                        {
                            var processor = (Activator.CreateInstance(procType) as Processors.PostProcessor)!;
                            processor.ApplySettings(procMode.Options);
                            processor.Run(images, client, post, codex);
                        }
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

        static List<string> GetImageLinks(Post post)
        {
            List<string> links = new();

            if (post is SelfPost selfPost) //pull out any links from self posts
            {
                var parseLinks = new Regex(@"\b(?:https?://|www\.)\S+\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var mtch = parseLinks.Match(selfPost.SelfText);

                while (mtch?.Success == true)
                {
                    if (IsImageFormat(mtch.Value))
                    {
                        links.Add(mtch.Value);
                    }

                    mtch = mtch.NextMatch();
                }
            }
            else if (post is LinkPost linkPost) //use post json to find media links
            {
                try
                {
                    if (linkPost.Preview != null)
                    {
                        var parsed = linkPost.Preview.ToObject<RedditModels.Post>();
                        if (parsed?.PreviewImages?.Any() == true)
                        {
                            foreach (var item in parsed.PreviewImages)
                            {
                                if (IsImageFormat(item.Metadata.Url))
                                {
                                    links.Add(item.Metadata.Url);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to parse post json: {ex}");
                }

                if(linkPost.IsGallery) //gallery post (currently requires pull request #151)
                {
                    var gallery = linkPost.Listing?.MediaMetadata;
                    if (gallery != null)
                    {
                        foreach (var metadata in gallery.Values)
                        {
                            string? source = metadata.s?.u;
                            if (source != null)
                            {
                                links.Add(source);
                            }
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
