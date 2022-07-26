using Newtonsoft.Json;
using System.Linq;
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
            public List<Trait> PinnedTraits { get; set; }

            public bool? IsValid { get; set; }
            public bool? ValidHealth { get; set; }
            public bool? ValidCast { get; set; }
            public bool? ValidBackButton { get; set; }
            public int? ColumnCount { get; set; }
            public DateTime? VerifiedDateUTC { get; set; }

            public Screen(string fileName)
            {
                FileName = fileName;
                Traits = new();
                PinnedTraits = new();
            }

            public class Trait
            {
                public string? Name { get; set; }
                public int Col { get; set; }
                public int Row { get; set; }
                public bool? IsPinned { get; set; }

                public override string ToString()
                {
                    return Col == -1 ? $"Pin {Row}: {Name}" : $"Tray {Col}_{Row}: {Name}";
                }

                public string GetPos()
                {
                    return Col == -1 ? Row.ToString() : $"{Col}_{Row}";
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
            var data = JsonConvert.DeserializeObject<TrainingData>(dataFile.ReadToEnd());

            foreach (var screen in data.Screens)
            {
                OrderTraits(screen);
            }

            return data;
        }

        internal void Save(string filename)
        {
            foreach (var screen in this.Screens)
            {
                OrderTraits(screen);
            }

            using StreamWriter file = new(filename);
            file.Write(JsonConvert.SerializeObject(this, Formatting.Indented));
        }

        private static void OrderTraits(Screen screen)
        {
            screen.Traits = screen.Traits.OrderBy(t => t.Row).ThenBy(t => t.Col).ToList();

            if (screen.PinnedTraits != null)
            {
                screen.PinnedTraits = screen.PinnedTraits.OrderBy(t => t.Row).ToList();
            }
        }
    }
}
