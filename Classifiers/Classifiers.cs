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
                string shortFile = Path.GetFileName(imagePath);
                using var image = Cv2.ImRead(imagePath, ImreadModes.Unchanged);
                foreach (IClassifier classer in classifiers)
                {
                    timer.Restart();

                    //TODO use TryMakeValidScreen's result and invoke IsValidScreenML before attempting to classify
                    ClassifiedScreen? result = classer.Classify(image, imagePath, debugOutput);

                    //if it's null something went very wrong
                    if (result == null)
                    {
                        Console.WriteLine($"Failed to classify {shortFile} with {classer}");
                    }
                    else
                    {
                        Console.WriteLine($"Classified {shortFile} with {classer} in {timer.Elapsed.TotalSeconds:N2}s. Valid: {result.IsValid}");
                    }

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
