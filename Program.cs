using HadesBoonBot;
using System.Text.Json;

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

            return 0;
        }
    }
}
