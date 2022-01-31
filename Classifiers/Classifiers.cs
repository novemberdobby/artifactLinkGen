using OpenCvSharp;
using System.Diagnostics;

namespace HadesBoonBot
{
    internal class Classifiers
    {
        public static int Run(string[] args, bool debugOutput, Codex codex, params IClassifier[] classifiers)
        {
            string inputImageDir = args[0];

            //iterate through victory screens
            Stopwatch timer = new();
            foreach (var imagePath in Directory.EnumerateFiles(inputImageDir))
            {
                using var image = Cv2.ImRead(imagePath);
                foreach (IClassifier classer in classifiers)
                {
                    timer.Restart();
                    ClassifiedScreen result = classer.Classify(image, imagePath, debugOutput);
                    Console.WriteLine($"Classified {Path.GetFileName(imagePath)} with {classer} in {timer.Elapsed.TotalSeconds:N2}s. Valid: {result.IsValid}");

                    //TODO: optionally verify against The Database
                }
            }

            return 0;
        }
    }

    internal class ClassifiedScreen
    {
        public string? WeaponName;
        public List<Slot> Slots;
        public bool IsValid { get; private set; }

        public class Slot
        {
            public Codex.Provider.Trait Trait;
            public int Col;
            public int Row;

            public Slot(Codex.Provider.Trait trait, int col, int row)
            {
                Trait = trait;
                Col = col;
                Row = row;
            }

            public override string ToString()
            {
                return $"{Col}_{Row}: {Trait}";
            }
        }

        public ClassifiedScreen(Codex codex, IEnumerable<Slot> inSlots)
        {
            Slots = inSlots.ToList();
            WeaponName = Codex.DetermineWeapon(Slots.Select(s => s.Trait));

            IsValid = WeaponName != null;
        }
    }
}
