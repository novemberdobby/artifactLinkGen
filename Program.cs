using System.Text.Json;
using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    class Program
    {
        static int Main(string[] args)
        {
            TrainingData inputData;
            using (StreamReader data = new(args[0]))
            {
                inputData = JsonSerializer.Deserialize<TrainingData>(data.ReadToEnd())!;
            }

            foreach (TrainingData.Screen screen in inputData.Screens)
            {
                if(!File.Exists(screen.FileName))
                {
                    Console.WriteLine($"Skipping {screen.FileName} due to missing file");
                    continue;
                }
                else
                {
                    Console.WriteLine($"Reading {screen.FileName}");
                }

                OCV.Mat image = Cv2.ImRead(screen.FileName);
            }

            return 0;
        }
    }
}
