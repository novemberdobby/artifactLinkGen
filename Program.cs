using CommandLine;
using Microsoft.Extensions.Configuration;

namespace HadesBoonBot
{
    class Program
    {
        static int Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddUserSecrets<Program>()
                .Build();

            using var codex = Codex.FromFile("codex.json", Codex.IconLoadMode.Raw);
            var commonModels = ML.Model.CreateModels();

            try
            {
                return Parser.Default.ParseArguments<
                    Training.GenerateTraitsOptions,
                    Training.GenerateScreensOptions,
                    Classifiers.ClassifierPSNROptions,
                    Classifiers.ClassifierMLOptions,
                    Bot.BotOptions
                        >(args)

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
                        using var classifier = Classifiers.ClassifierFactory.Create(options, codex);
                        return classifier.Run(options, commonModels);
                    },

                    (Classifiers.ClassifierMLOptions options) =>
                    {
                        using var classifier = Classifiers.ClassifierFactory.Create(options, codex);
                        return classifier.Run(options, commonModels);
                    },

                    (Bot.BotOptions options) =>
                    {
                        using Bot.Monitor monitor = new(options, config, commonModels);
                        return monitor.Run(codex);
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
