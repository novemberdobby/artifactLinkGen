using Microsoft.ML;
using Newtonsoft.Json;

namespace HadesBoonBot.ML
{
    internal class Model
    {
        private readonly MLContext m_context;
        private PredictionEngine<ModelInput, ModelOutput>? m_predictEngine;
        private readonly ModelConfig m_config;

        public Model(MLContext context, string modelName, ExtractDelegate extractorMethod)
        {
            m_context = context;
            Name = modelName;
            Extract = extractorMethod;
            m_config = ModelConfig.FromFile(MLNetModelPath);
        }

        private PredictionEngine<ModelInput, ModelOutput> CreatePredictEngine()
        {
            ITransformer mlModel = m_context.Model.Load(MLNetModelPath, out _);
            return m_context.Model.CreatePredictionEngine<ModelInput, ModelOutput>(mlModel);
        }

        public readonly string Name; //e.g. HealthCheck
        public string MLNetConfigPath => Path.GetFullPath($@"ML\{Name}Model.mbconfig");
        public string MLNetModelPath => Path.GetFullPath($@"ML\{Name}Model.zip");

        public delegate bool ExtractDelegate(ScreenMetadata meta, OpenCvSharp.Mat screen, string targetFilename);
        public readonly ExtractDelegate Extract;

        public ITransformer RetrainPipeline(IDataView trainData)
        {
            // Data process configuration with pipeline data transformations
            var pipeline = m_context.Transforms.Conversion.MapValueToKey(@"Label", @"Label")
                                    .Append(m_context.Transforms.LoadRawImageBytes(outputColumnName: @"ImageSource_featurized", imageFolder: @"", inputColumnName: @"ImageSource"))
                                    .Append(m_context.Transforms.CopyColumns(@"Features", @"ImageSource_featurized"))
                                    .Append(m_context.MulticlassClassification.Trainers.ImageClassification(labelColumnName: @"Label"))
                                    .Append(m_context.Transforms.Conversion.MapKeyToValue(@"PredictedLabel", @"PredictedLabel"));

            return pipeline.Fit(trainData);
        }

        /// <summary>
        /// Use this method to predict on <see cref="ModelInput"/>.
        /// </summary>
        /// <param name="input">model input.</param>
        /// <returns><seealso cref="ModelOutput"/></returns>
        public ModelOutput Predict(ModelInput input)
        {
            m_predictEngine ??= CreatePredictEngine();
            return m_predictEngine.Predict(input);
        }

        internal static Dictionary<string, Model> CreateModels()
        {
            MLContext mlContext = new();
            return new()
            {
                { "HealthCheck", new(mlContext, "HealthCheck", ScreenMetadata.ExtractML_HealthCheck) },
                { "CastCheck", new(mlContext, "CastCheck", ScreenMetadata.ExtractML_CastCheck) },
            };
        }

        internal string GetTrainingPath()
        {
            string? folder = m_config?.Source?.FolderPath;
            if (folder == null)
            {
                throw new Exception($"Missing or invalid training folder for model {Name}");
            }

            return folder;
        }

        private class ModelConfig
        {
            [JsonProperty("DataSource")]
            public DataSource? Source { get; set; }

            public class DataSource
            {
                public string? FolderPath { get; set; }
            }

            public static ModelConfig FromFile(string inputFile)
            {
                string data = File.ReadAllText(inputFile);
                return JsonConvert.DeserializeObject<ModelConfig>(data);
            }
        }
    }
}
