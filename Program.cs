using OCV = OpenCvSharp;
using Cv2 = OpenCvSharp.Cv2;
using CommandLine;
using HadesBoonBot.Training;

namespace HadesBoonBot
{
    class Program
    {
        static int Main(string[] args)
        {
            using var codex = Codex.FromFile("codex.json", Codex.IconLoadMode.Raw);
            var MLmodels = ML.Model.CreateModels();

            try
            {
                return Parser.Default.ParseArguments<GenerateTraitsOptions, GenerateScreensOptions, ClassifierPSNROptions>(args)
                .MapResult(
                    
                    (GenerateTraitsOptions options) =>
                    {
                        TraitDataGen tp = new();
                        tp.Run(options, codex);
                        return 0;
                    },

                    (GenerateScreensOptions options) =>
                    {
                        ScreenDataGen sdg = new();
                        sdg.Run(options, MLmodels);
                        return 0;
                    },

                    (ClassifierPSNROptions options) =>
                    {
                        using IClassifier classifier = new ClassifierPSNR(options, codex);
                        return Classifiers.Run(options.ScreensDir, options.DebugOutput, codex, classifier);
                    },

                    errors =>
                    {
                        Console.Error.WriteLine("Error parsing command line");
                        return int.MinValue;
                    }
                );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Exception during execution: {0}", ex);
                return 1;
            }
        }
    }
}
