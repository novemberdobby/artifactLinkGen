namespace HadesBoonBot.Processors
{
    internal class PrivateMessage : PostProcessor
    {
        public string DirectMessageUser { get; set; }

        public override void Run(IEnumerable<Classifiers.ClassifiedScreenMeta> screens, Reddit.RedditClient client, Reddit.Controllers.Post post, Codex codex)
        {
            List<string> msgLines = new();
            foreach (var img in screens.OrderBy(i => i.RemoteSource))
            {
                msgLines.Add($"{img.RemoteSource}, valid: {img.Screen?.IsValid == true}");
            }

            client.Account.Messages.Compose(
                DirectMessageUser,
                $"Victory screen {post.Id}",
                string.Join("\n\n", new[] { $"New victory screen posted by /u/{post.Author}: {post.Permalink}" }.Concat(msgLines)));
        }
    }
}
