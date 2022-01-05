using System.Text.Json;

namespace HadesBoonBot
{
    internal class TrainingData
    {
        public List<Screen> Screens { get; set; }

        public class Screen
        {
            public string FileName { get; set; }
            public List<Trait> Traits { get; set; }

            public class Trait
            {
                public string Name { get; set; }
                public int Col { get; set; }
                public int Row { get; set; }

                public override string ToString()
                {
                    return $"{Col}_{Row}: {Name}";
                }
            }
        }

        internal static TrainingData? Load(string filename)
        {
            using StreamReader dataFile = new(filename);
            return JsonSerializer.Deserialize<TrainingData>(dataFile.ReadToEnd());
        }

        internal void Save(string filename)
        {
            using StreamWriter file = new(filename);
            file.Write(JsonSerializer.Serialize(this, new JsonSerializerOptions() { WriteIndented = true }));
        }
    }
}
