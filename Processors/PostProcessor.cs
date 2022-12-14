using System.Reflection;

namespace HadesBoonBot.Processors
{
    abstract class PostProcessor
    {
        public abstract void Run(IEnumerable<Classifiers.ClassifiedScreenMeta> screens, Reddit.RedditClient client, Reddit.Controllers.Post post, Codex codex);

        /// <summary>
        /// Apply settings from a json object to `this`
        /// </summary>
        /// <param name="settingsObject">Json object, can be null</param>
        /// <returns>This</returns>
        public PostProcessor ApplySettings(Newtonsoft.Json.Linq.JObject? settingsObject)
        {
            //this is not optimal
            var myType = this.GetType();
            var parsed = settingsObject?.ToObject(myType);
            if (parsed != null)
            {
                foreach (var prop in myType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    prop.SetValue(this, prop.GetValue(parsed));
                }
            }

            return this;
        }
    }
}
