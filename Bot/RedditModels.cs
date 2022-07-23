using Newtonsoft.Json;

namespace HadesBoonBot.Bot
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    internal class RedditModels
    {
        internal class Post
        {
            [JsonProperty("images")]
            public List<PreviewImage> PreviewImages { get; set; }
        }

        internal class PreviewImage
        {
            [JsonProperty("source")]
            public ImageMetadata Metadata { get; set; }
        }

        internal class ImageMetadata
        {
            public string Url { get; set; }
        }
    }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
