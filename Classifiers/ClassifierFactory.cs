namespace HadesBoonBot.Classifiers
{
    internal class ClassifierFactory
    {
        internal static (BaseClassifier classifier, BaseClassifierOptions options) Create(string type, Newtonsoft.Json.Linq.JObject? json, Codex codex)
        {
            BaseClassifierOptions options = type.ToLower() switch
            {
                "psnr" => json?.ToObject<ClassifierPSNROptions>()!,
                "ml" => json?.ToObject<ClassifierMLOptions>()!,
                _ => throw new ArgumentException($"Unknown classifier type {type}", nameof(type)),
            };

            return (Create(options, codex), options);
        }

        internal static BaseClassifier Create(BaseClassifierOptions options, Codex codex)
        {
            if (options is ClassifierPSNROptions optionsPSNR)
            {
                return new ClassifierPSNR(optionsPSNR, codex);
            }
            else if (options is ClassifierMLOptions optionsML)
            {
                return new ClassifierML(optionsML, codex);
            }
            else
            {
                throw new ArgumentException($"Unknown classifier type: {options}", nameof(options));
            }
        }
    }
}
