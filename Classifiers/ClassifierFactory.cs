namespace HadesBoonBot.Classifiers
{
    internal class ClassifierFactory
    {
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
