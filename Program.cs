using CommandLine;

namespace HadesBoonBot
{
    class Program
    {
        static int Main(string[] args)
        {
            using var codex = Codex.FromFile("codex.json", Codex.IconLoadMode.Raw);
            var commonModels = ML.Model.CreateModels();

            try
            {
                return Parser.Default.ParseArguments<Training.GenerateTraitsOptions, Training.GenerateScreensOptions, Classifiers.ClassifierPSNROptions, Classifiers.ClassifierMLOptions>(args)
                .MapResult(
                    
                    (Training.GenerateTraitsOptions options) =>
                    {
                        Training.TraitDataGen tp = new();
                        tp.Run(options, codex);
                        return 0;
                    },

                    (Training.GenerateScreensOptions options) =>
                    {
                        Training.ScreenDataGen sdg = new();
                        sdg.Run(options, commonModels);
                        return 0;
                    },

                    (Classifiers.ClassifierPSNROptions options) =>
                    {
                        using Classifiers.ClassifierPSNR classifier = new(options, codex);
                        return classifier.Run(options, commonModels);
                    },

                    (Classifiers.ClassifierMLOptions options) =>
                    {
                        using Classifiers.ClassifierML classifier = new(options, codex);
                        return classifier.Run(options, commonModels);
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
