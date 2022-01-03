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
    }
}
