using HadesBoonBot.ML;
using OCV = OpenCvSharp;
using Cv2 = OpenCvSharp.Cv2;
using CommandLine;

namespace HadesBoonBot.Training
{
    [Verb("generate_screens", HelpText = "Extract sample data from victory screens to use for validating new ones")]
    internal class GenerateScreensOptions
    {
        [Option('c', "clean", Required = false, Default = false, HelpText = "Clean output directory first")]
        public bool Clean { get; set; }

        [Option('t', "training_data", Required = true, HelpText = "Training data file")]
        public string TrainingData { get; set; }

        public GenerateScreensOptions()
        {
            TrainingData = string.Empty;
        }
    }

    internal class ScreenDataGen
    {
        public void Run(GenerateScreensOptions options, List<Model> models)
        {
            TrainingData inputData = TrainingData.Load(options.TrainingData);

            //create ML input dirs
            foreach (var validity in new[] { "good", "bad" })
            {
                foreach (var model in models)
                {
                    string trainingDir = Path.Combine(model.TrainingPath, validity);
                    if (options.Clean && Directory.Exists(trainingDir))
                    {
                        Directory.Delete(trainingDir, true);
                    }

                    if (!Directory.Exists(trainingDir))
                    {
                        Directory.CreateDirectory(trainingDir);
                    }
                }
            }

            //chop out good-for-training parts of the victory screens
            Parallel.ForEach(inputData.Screens, trained =>
            {
                if (new[] { trained.IsValid, trained.ValidHealth, trained.ValidCast, trained.ValidBackButton }.Any(b => b == null))
                {
                    throw new Exception($"Classification data incomplete; unclear if this image can be used for ML validity training: {trained.FileName}");
                }

                using OCV.Mat image = Cv2.ImRead(trained.FileName, OCV.ImreadModes.Unchanged);
                using OCV.Mat? firstValid = ScreenMetadata.TryMakeValidScreen(image, trained.FileName);
                var useForExtraction = firstValid ?? image;

                ScreenMetadata meta = new(useForExtraction);
                foreach (var model in models)
                {
                    string trainingDir = Path.Combine(model.TrainingPath, model.GetIsValid(trained) ? "good" : "bad");
                    string targetFile = Path.Combine(trainingDir, Path.ChangeExtension(Path.GetFileName(trained.FileName), ".png"));
                    if (!File.Exists(targetFile))
                    {
                        model.Extract(meta, useForExtraction, targetFile); //this might fail if the image is an incorrect size, which is ok
                    }
                }
            });
        }
    }
}
