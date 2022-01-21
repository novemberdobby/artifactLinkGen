namespace HadesBoonBot
{
    class Program
    {
        static int Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine($"Invalid arguments");
                return 1;
            }

            try
            {
                var codex = new Lazy<Codex>(() => Codex.FromFile("codex.json", Codex.IconLoadMode.Raw));

                switch (args[0])
                {
                    case "trainingdatagen":
                        TrainingDataGen tp = new();
                        tp.Run(args, codex);
                        break;

                    case "classify_psnr":
                        ClassifierPSNR classifier = new();
                        return classifier.Run(args, codex);

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
