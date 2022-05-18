using CommandLine;
using HadesBoonBot.ML;
using Microsoft.ML;
using OCV = OpenCvSharp;
using SampleCategory = HadesBoonBot.Training.TraitDataGen.SampleCategory;

namespace HadesBoonBot.Classifiers
{
    [Verb("classify_ml", HelpText = "Classify traits on a victory screen via ML.NET")]
    class ClassifierMLOptions : BaseClassifierOptions
    {

    }

    /// <summary>
    /// Classify traits on a victory screen by running predictions against a trained ML model
    /// </summary>
    internal class ClassifierML : BaseClassifier
    {
        private readonly Codex m_codex;
        private readonly PredictionEngine<ModelInput, ModelOutput> m_predictEngine;
        
        private readonly string[] m_modelLabels;
        private readonly HashSet<string> m_pinNames = new();
        private readonly HashSet<string> m_trayNames = new();

        public ClassifierML(ClassifierMLOptions options, Codex codex) : base(codex)
        {
            m_codex = codex;

            var context = new MLContext();
            ITransformer mlModel = context.Model.Load(Path.GetFullPath($@"ML\Models\Traits.zip"), out _);
            m_predictEngine = context.Model.CreatePredictionEngine<ModelInput, ModelOutput>(mlModel);

            var labelBuffer = new Microsoft.ML.Data.VBuffer<ReadOnlyMemory<char>>();
            m_predictEngine.OutputSchema["Score"].Annotations.GetValue("SlotNames", ref labelBuffer);
            m_modelLabels = labelBuffer.DenseValues().Select(l => l.ToString()).ToArray();

            //the model is trained on items with the name format: <trait_name>_<SampleCategory_enum>
            //categorise them so we can test each image against only the correct set of items
            foreach (var label in m_modelLabels)
            {
                string labelType = label[(label.LastIndexOf('_') + 1)..];
                if (labelType == SampleCategory.PinIcons.ToString() || labelType == SampleCategory.Autogen.ToString())
                {
                    m_pinNames.Add(label);
                }
                else if (labelType == SampleCategory.TrayIcons.ToString() || labelType == SampleCategory.AutogenPinned.ToString())
                {
                    m_trayNames.Add(label);
                }
                else
                {
                    throw new Exception($"Unknown model label type: {labelType}");
                }
            }
        }

        public override ClassifiedScreen? Classify(OCV.Mat screen, string filePath, int columnCount, int pinRows, bool debugOutput)
        {
            ScreenMetadata meta = new(screen);
            List<(int Column, int Row, OCV.Rect traitRect, List<(string, float)> Matches)> slots = new();

            //TODO move some of this boilerplate into the base class

            //build list of potential trait locations on the screen
            for (int column = 0; column < ScreenMetadata.BoonColumnsMax; column++)
            {
                //if we know the column count, respect it
                if (columnCount > 0 && column >= columnCount)
                {
                    break;
                }

                for (int row = 0; row < ScreenMetadata.BoonRowsMax; row++)
                {
                    if (meta.TryGetTraitRect(column, row, out OCV.Rect? traitRect))
                    {
                        slots.Add((column, row, traitRect!.Value, new()));
                    }
                }
            }

            for (int i = 0; i < pinRows; i++)
            {
                var pinIconRect = meta.GetPinRect(columnCount, i).iconRect;
                slots.Add((-1, i, pinIconRect, new()));
            }

            string tempDir = Path.Combine(Path.GetTempPath(), $"hbb_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                foreach (var slot in slots)
                {
                    (int column, int row, OCV.Rect traitRect, List<(string, float)> finalMatches) = slot;

                    //must be a problematic image (wrong dimensions, photo of a screen etc)
                    //TODO do this earlier so we can bail before spending too much time on comparisons (&in psnr too)
                    if (traitRect.Left < 0 || traitRect.Top < 0 || traitRect.Right > screen.Width || traitRect.Bottom > screen.Height)
                    {
                        continue;
                    }

                    //grab the image
                    using OCV.Mat traitImg = screen.SubMat(traitRect);

                    //get possible trait list
                    var filteredTraits = ScreenMetadata.GetSlotTraits(m_codex, column, row);
                    var filteredTraitNames = new HashSet<string>(filteredTraits.Select(f => f.Name));

                    //TODO predict without temp file
                    string tempFile = Path.Combine(tempDir, $"{slot.Column}_{slot.Row}.png");
                    using var tempMat = screen.SubMat(traitRect);
                    tempMat.SaveImage(tempFile);

                    var input = new ModelInput(tempFile);
                    var output = m_predictEngine.Predict(input);

                    //first build up a full list of matches
                    Dictionary<string, float> result = new();
                    for (int i = 0; i < output.Score.Length; i++)
                    {
                        result.Add(m_modelLabels[i], output.Score[i]);
                    }

                    //then trim it down to relevant matches
                    var relevantList = column == -1 ? m_pinNames : m_trayNames;
                    var ordered = result
                        .Where(r => relevantList.Contains(r.Key)) //filter by sample type
                        .Where(r => filteredTraitNames.Contains(r.Key[..r.Key.LastIndexOf('_')])) //then by slot location
                        .OrderByDescending(r => r.Value);

                    finalMatches.AddRange(ordered.Select(o => (o.Key, o.Value)));
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }

            return new(m_codex, slots.Select(r =>
            {
                //TODO a proper data structure for these
                string traitName = r.Matches.First().Item1;
                traitName = traitName[..traitName.LastIndexOf('_')];

                return new ClassifiedScreen.Slot(m_codex.ByName[traitName], r.Column, r.Row);
            }));
        }

        public override void Dispose()
        {
            m_predictEngine.Dispose();
        }
    }
}
