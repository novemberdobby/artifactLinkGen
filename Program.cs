using OCV = OpenCvSharp;
using Cv2 = OpenCvSharp.Cv2;

namespace HadesBoonBot
{
    class Program
    {
        static int Main(string[] args)
        {
            //TODO this should really be an argument
#if DEBUG
            const bool debugOutput = true;
#else
            const bool debugOutput = false;
#endif

            if (args.Length == 0)
            {
                Console.WriteLine($"Invalid arguments");
                return 1;
            }

            using var codex = Codex.FromFile("codex.json", Codex.IconLoadMode.Raw);
            var MLmodels = ML.Model.CreateModels();

            try
            {
                string[] cmdArgs = args.Skip(1).ToArray();
                switch (args[0])
                {
                    case "trainingdatagen":
                        {
                            Training.TraitDataGen tp = new();
                            tp.Run(args, codex);
                        }
                        break;

                    case "classify_psnr":
                        {
                            using IClassifier classifier = new ClassifierPSNR(cmdArgs, codex);
                            return Classifiers.Run(cmdArgs, debugOutput, codex, classifier);
                        }

                    default:
                        Console.WriteLine($"Unknown mode {args[0]}");
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Exception during execution: {0}", ex);
                return 2;
            }

            return 0;
        }
    }
}
