using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace HadesBoonBot
{
    internal class TrainingData
    {
        public List<Screen> Screens { get; set; } = new();

        [JsonIgnore]
        public Dictionary<string, Screen> ScreensByFile = new();

        public class Screen
        {
            public string FileName { get; set; }
            public List<Trait> Traits { get; set; }

            public bool? IsValid { get; set; }
            public bool? ValidHealth { get; set; }
            public bool? ValidCast { get; set; }
            public bool? ValidBackButton { get; set; }
            public int? ColumnCount { get; set; }

            public Screen(string fileName)
            {
                FileName = fileName;
                Traits = new();
            }

            public class Trait
            {
                public string? Name { get; set; }
                public int Col { get; set; }
                public int Row { get; set; }
                public bool IsPinned { get; set; }

                public override string ToString()
                {
                    return $"{Col}_{Row}: {Name}";
                }
            }

            public override string ToString()
            {
                return $"{Path.GetFileName(FileName)}, valid: '{IsValid}'";
            }
        }

        [OnDeserialized]
        void OnDeserialized(StreamingContext context)
        {
            foreach (var screen in Screens)
            {
                ScreensByFile[screen.FileName.ToLower()] = screen;
            }
        }

        internal static TrainingData Load(string filename)
        {
            using StreamReader dataFile = new(filename);
            return JsonConvert.DeserializeObject<TrainingData>(dataFile.ReadToEnd());
        }

        internal void Save(string filename)
        {
            using StreamWriter file = new(filename);
            file.Write(JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
