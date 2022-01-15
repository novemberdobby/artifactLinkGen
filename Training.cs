using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    internal class TrainingDataGen
    {
        internal int Run(string[] args)
        {
            TrainingData inputData = TrainingData.Load(args[1])!;
            string outputDataDir = args[2];

            //save out sample data from manually classified victory screens
            foreach (TrainingData.Screen screen in inputData.Screens)
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

                foreach (var trait in screen.Traits)
                {
                    string targetDir = Path.Combine(outputDataDir, trait.Name);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    //save it with a name pointing back to the source
                    string targetFile = Path.Combine(targetDir, $"{Path.GetFileName(screen.FileName)}_{trait.Col}_{trait.Row}.png");
                    if (!File.Exists(targetFile))
                    {
                        Dimensions dim = new(image.Value.Width);
                        OCV.Rect traitRect = dim.FindTrait(trait.Col, trait.Row);

                        OCV.Mat traitImg = image.Value.SubMat(traitRect);
                        traitImg = CVUtil.MakeComparable(traitImg);
                        traitImg.SaveImage(targetFile);
                    }
                }
            }

            return 0;
        }
    }
}
