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

            var codex = new Lazy<Codex>(() => Codex.FromFile("codex.json", true));

            switch (args[0])
            {
                case "trainingdatagen":
                    TrainingDataGen tp = new();
                    return tp.Run(args, codex);

                case "classify_psnr":
                    ClassifierPSNR classifier = new();
                    return classifier.Run(args, codex);

                default:
                    Console.WriteLine($"Unknown mode {args[0]}");
                    return 1;
            }
        }
    }
}
