using CommandLine;

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
                return Parser.Default.ParseArguments<Training.GenerateTraitsOptions, Training.GenerateScreensOptions, Classifiers.ClassifierPSNROptions>(args)
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
                        sdg.Run(options, MLmodels);
                        return 0;
                    },

                    (Classifiers.ClassifierPSNROptions options) =>
                    {
                        using IClassifier classifier = new Classifiers.ClassifierPSNR(options, codex);
                        TrainingData? trained = options.TrainingData == null ? null : TrainingData.Load(options.TrainingData);

                        if (File.Exists(options.Input))
                        {
                            Classifiers.ClassifiedScreen? result = Classifiers.Runner.RunSingle(options, options.Input, MLmodels, codex, trained, classifier);
                            return 0;
                        }
                        else if (Directory.Exists(options.Input))
                        {
                            return Classifiers.Runner.RunBatch(options, options.Input, MLmodels, codex, trained, classifier);
                        }
                        else
                        {
                            throw new ArgumentException($"Path passed to {nameof(Classifiers.ClassifierCommonOptions)} must be a file or directory that exists", nameof(args));
                        }
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
