using Newtonsoft.Json;

namespace HadesBoonBot.Bot
{
    class BotState
    {
        public static BotState FromFile(string inputFile)
        {
            string data = File.ReadAllText(inputFile);
            return JsonConvert.DeserializeObject<BotState>(data);
        }

        public void Save(string filename)
        {
            using StreamWriter file = new(filename);
            file.Write(JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        public class ProcessedPost
        {
            public string CommentID;

            private ProcessedPost()
            {
                CommentID = string.Empty;
            }
        }

        public Dictionary<string, ProcessedPost?> ProcessedPosts { get; set; } = new();
    }
}
