using CommandLine;
using OCV = OpenCvSharp;
using System.Diagnostics;
using System.Text;

namespace HadesBoonBot.Classifiers
{
    internal class ClassifierCommonOptions
    {
        [Option('d', "debug", Required = false, Default = false, HelpText = "Save out debugging images")]
        public bool DebugOutput { get; set; }

        [Option('i', "input", Required = true, HelpText = "Root folder for screens to classify, or path to single image")]
        public string Input { get; set; }

        [Option('t', "training_data", Required = false, HelpText = "Training data file, check results against this file if supplied")]
        public string? TrainingData { get; set; }

        protected ClassifierCommonOptions()
        {
            Input = string.Empty;
        }
    }

    internal class Runner
    {
        /// <summary>
        /// Run a set of classifiers against a set of images - currently meant for debugging purposes
        /// </summary>
        public static int RunBatch(ClassifierCommonOptions options, string screensPath, List<ML.Model> models, Codex codex, TrainingData? trained, params IClassifier[] classifiers)
        {
            int errors = 0;

            foreach (var screenPath in Directory.EnumerateFiles(screensPath))
            {
                foreach (IClassifier classer in classifiers)
                {
                    ClassifiedScreen? result = RunSingle(options, screenPath, models, codex, trained, classer);
                    if (result == null || !result.IsValid)
                    {
                        errors++;
                    }
                }
            }

            return errors;
        }

        /// <summary>
        /// Run a classifier against an image
        /// </summary>
        /// <param name="options">Command options</param>
        /// <param name="screenPath">Full path to image</param>
        /// <param name="codex">Codex (to check icon-sharing traits)</param>
        /// <param name="models">ML models to determine screen validity</param>
        /// <param name="trained">Training data to verify against when not null</param>
        /// <param name="classer">Classifier</param>
        /// <returns>Classified screen (which may or may not be valid) or null</returns>
        public static ClassifiedScreen? RunSingle(ClassifierCommonOptions options, string screenPath, List<ML.Model> models, Codex codex, TrainingData? trained, IClassifier classer)
        {
            string shortFile = Path.GetFileName(screenPath);
            string screenPathLower = screenPath.ToLower();
            using var origImage = OCV.Cv2.ImRead(screenPath, OCV.ImreadModes.Unchanged);
            bool appearsValid = true;

            Stopwatch timer = new();
            timer.Start();

            //is it the right size?
            using var image = ScreenMetadata.TryMakeValidScreen(origImage, screenPath);
            if (image == null)
            {
                Console.WriteLine($"Failed to make valid image from {screenPath}");
                appearsValid = false;
            }

            //does the robot think it's real?
            if (appearsValid)
            {
                ScreenMetadata meta = new(image!.Width);
                int validityScore = meta.IsValidScreenML(image, models);
                if (validityScore < 2)
                {
                    Console.WriteLine($"ML reports invalid image: {screenPath}");
                    appearsValid = false;
                }
            }

            Console.WriteLine($"Initial validation of {shortFile} took {timer.Elapsed.TotalSeconds:N2}s. Appears valid: {appearsValid}");

            if (trained != null)
            {
                if (trained.ScreensByFile.TryGetValue(screenPathLower, out var trainedScreen))
                {
                    if (trainedScreen.IsValid != appearsValid)
                    {
                        throw new Exception($"Screen validity doesn't match training validity: {screenPath}");
                    }
                }
                else
                {
                    throw new Exception($"Verification requested for screen that doesn't exist in the training data: {screenPath}");
                }
            }

            if (!appearsValid)
            {
                return null;
            }

            timer.Restart();
            ClassifiedScreen? result = classer.Classify(image!, screenPath, options.DebugOutput);

            //if it's null something went very wrong
            if (result == null)
            {
                Console.Error.WriteLine($"Failed to classify {shortFile} with {classer}");
            }
            else
            {
                Console.WriteLine($"Classified {shortFile} with {classer} in {timer.Elapsed.TotalSeconds:N2}s. Valid: {result.IsValid}");

                //optionally verify against The Database
                if (trained != null)
                {
                    if (trained.ScreensByFile.TryGetValue(screenPathLower, out var trainedScreen))
                    {
                        int correct = 0;
                        List<ClassifiedScreen.Slot> incorrect = new();

                        Dictionary<OCV.Point, string> trainedTraits = new();
                        foreach (var trait in trainedScreen.Traits)
                        {
                            trainedTraits.Add(new(trait.Col, trait.Row), trait.Name!);
                        }

                        foreach (var slot in result.Slots)
                        {
                            var knownCorrectName = trainedTraits[new(slot.Col, slot.Row)];
                            IEnumerable<string> goodNames = codex.GetIconSharingTraits(knownCorrectName).Select(t => t.Name);

                            if (goodNames.Contains(slot.Trait.Name))
                            {
                                correct++;
                            }
                            else
                            {
                                incorrect.Add(slot);
                            }
                        }

                        StringBuilder resultText = new($"For {screenPath}, {correct}/{correct + incorrect.Count} were correct");
                        if (incorrect.Any())
                        {
                            resultText.Append($" (incorrect slots: {string.Join(", ", incorrect)})");
                        }

                        Console.WriteLine(resultText.ToString());
                    }
                    else
                    {
                        throw new Exception($"Verification requested for screen that doesn't exist in the training data: {screenPath}");
                    }
                }
            }

            return result;
        }
    }
}
