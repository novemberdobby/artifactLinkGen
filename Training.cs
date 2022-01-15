using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    internal class TrainingDataGen
    {
        internal int Run(string[] args, Lazy<Codex> codex)
        {
            TrainingData inputData = TrainingData.Load(args[1])!;
            string outputDataDir = args[2];

            //save out sample data from manually classified victory screens
            foreach (TrainingData.Screen screen in inputData.Screens!)
            {
                if (!File.Exists(screen.FileName))
                {
                    Console.WriteLine($"Skipping {screen.FileName} due to missing file");
                    continue;
                }

                Lazy<OCV.Mat> image = new(() =>
                {
                    Console.WriteLine($"Reading {screen.FileName}");
                    return Cv2.ImRead(screen.FileName);
                });

                foreach (var trait in screen.Traits!)
                {
                    string targetDir = Path.Combine(outputDataDir, trait.Name!);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    //save it with a name pointing back to the source
                    string targetFile = Path.Combine(targetDir, $"{Path.GetFileName(screen.FileName)}_{trait.Col}_{trait.Row}.png");
                    if (!File.Exists(targetFile))
                    {
                        ScreenMetadata dim = new(image.Value.Width);
                        if(dim.GetTraitRect(trait.Col, trait.Row, out OCV.Rect? traitRect))
                        {
                            OCV.Mat traitImg = image.Value.SubMat(traitRect!.Value);
                            traitImg = CVUtil.MakeComparable(traitImg);
                            traitImg.SaveImage(targetFile);
                        }
                    }
                }
            }

            //also save out the raw boon icons (TODO: expand this with mutations)
            foreach (var trait in codex.Value)
            {
                string targetDir = Path.Combine(outputDataDir, trait.Name!);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                string targetFile = Path.Combine(targetDir, "normal.png");
                if (!File.Exists(targetFile))
                {
                    trait.Icon!.SaveImage(targetFile);
                }
            }

            return 0;
        }
    }
}
