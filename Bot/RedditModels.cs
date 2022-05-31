using Newtonsoft.Json;

namespace HadesBoonBot.Bot
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    internal class RedditModels
    {
        internal class Post
        {
            [JsonProperty("author_fullname")]
            public string Username { get; set; }

            [JsonProperty("media_metadata")]
            public Dictionary<string, MediaMetadata> Media { get; set; }
        }

        internal class MediaMetadata
        {
            public string Status { get; set; }

            [JsonProperty("m")]
            public string MediaType { get; set; }
            
            [JsonProperty("p")]
            public List<MediaFile> Previews { get; set; }

            [JsonProperty("s")]
            public MediaFile Source { get; set; }
        }

        internal class MediaFile
        {
            [JsonProperty("x")]
            public int Width { get; set; }

            [JsonProperty("y")]
            public int Height { get; set; }

            [JsonProperty("u")]
            public string Url { get; set; }
        }
    }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
