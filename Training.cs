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
                        OCV.Point2f startPoint = trait.Col == 0 //the distance between columns 0&1 is unique
                            ? new(dim.FirstBoonIconX, dim.FirstBoonIconY)
                            : new(dim.SecondBoonIconX - dim.BoonColumnSep, dim.FirstBoonIconY);

                        if (trait.Col % 2 == 1)
                        {
                            startPoint.Y += dim.BoonColumnYoffset;
                        }

                        OCV.Point2f separation = new(dim.BoonColumnSep, dim.BoonRowSep);

                        //find the middle of the trait
                        OCV.Point middle = new(startPoint.X + trait.Col * separation.X, startPoint.Y + trait.Row * separation.Y);

                        //chop out a sub-image
                        float halfSize = dim.BoonWidth / 2.0f;

                        OCV.Mat unknownTrait = image.Value.SubMat((int)(middle.Y - halfSize), (int)(middle.Y + halfSize), (int)(middle.X - halfSize), (int)(middle.X + halfSize));
                        unknownTrait = CVUtil.MakeComparable(unknownTrait);
                        unknownTrait.SaveImage(targetFile);
                    }
                }
            }

            return 0;
        }
    }
}
