using CommandLine;
using static HadesBoonBot.Codex.Provider;
using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;
using SampleCategory = HadesBoonBot.Training.TraitDataGen.SampleCategory;

namespace HadesBoonBot.Classifiers
{
    [Verb("classify_psnr", HelpText = "Classify traits on a victory screen via PSNR")]
    class ClassifierPSNROptions : ClassifierCommonOptions
    {
        [Option('t', "trait_source", Required = true, HelpText = "Root folder for trait images (stored in subfolders corresponding to each trait name)")]
        public string TraitSource { get; set; }

        public ClassifierPSNROptions()
        {
            TraitSource = string.Empty;
        }
    }

    /// <summary>
    /// Classify traits on a victory screen by running PSNR comparisons against previously-classified training data
    /// </summary>
    internal class ClassifierPSNR : IClassifier, IDisposable
    {
        /// <summary>
        /// List of previously classified images, by trait name then sample category
        /// </summary>
        private readonly Dictionary<string, Dictionary<SampleCategory, List<TraitMatch>>> m_preclassifiedTraits = new();
        private readonly Codex m_codex;

        public ClassifierPSNR(ClassifierPSNROptions options, Codex codex)
        {
            m_codex = codex;

            //load up possible matches
            foreach (string traitNameFolder in Directory.EnumerateDirectories(options.TraitSource))
            {
                string traitName = Path.GetFileName(traitNameFolder);
                m_preclassifiedTraits.Add(traitName, new());

                foreach (string categoryFolder in Directory.EnumerateDirectories(traitNameFolder))
                {
                    string categoryName = Path.GetFileName(categoryFolder);
                    var category = Enum.Parse<SampleCategory>(categoryName);

                    List<TraitMatch> imagesForTrait = new();
                    m_preclassifiedTraits[traitName].Add(category, imagesForTrait);

                    Parallel.ForEach(Directory.EnumerateFiles(categoryFolder), file =>
                    {
                        var trait = m_codex.ByName[traitName];
                        var image = Cv2.ImRead(file);
                        lock (imagesForTrait)
                        {
                            imagesForTrait.Add(new(trait, file, image));
                        }
                    });
                }
            }

            //does every trait have at least one sample image?
            var missingTraits = m_codex.Where(t =>
            {
                //todo replace with updated logic
                string name = m_codex.GetIconSharingTraits(t.Name).First().Name;
                if (!m_preclassifiedTraits.TryGetValue(name, out var traits))
                {
                    return false;
                }

                return !traits.ContainsKey(SampleCategory.TrayIcons) || !traits[SampleCategory.TrayIcons].Any();
            });

            if (missingTraits.Any())
            {
                string missingStr = string.Join(Environment.NewLine, missingTraits.Select(t => t.Name));
                Console.WriteLine($"{nameof(ClassifierPSNR)}(); missing sample data for one or more traits:{Environment.NewLine}{missingStr}");
            }
        }

        public ClassifiedScreen? Classify(OCV.Mat screen, string filePath, int columnCount, int pinRows, bool debugOutput)
        {
            string? debugPath = null;
            if (debugOutput)
            {
                debugPath = ScreenMetadata.GetDebugOutputFolder(filePath, "results_psnr");
            }

            ScreenMetadata meta = new(screen);
            List<(int Column, int Row, OCV.Rect traitRect, List<TraitMatch> Matches)> slots = new();

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

            //classify each one
            Parallel.ForEach(slots, slot =>
            {
                (int column, int row, OCV.Rect traitRect, List<TraitMatch> finalMatches) = slot;

                //must be a problematic image (wrong dimensions, photo of a screen etc)
                if (traitRect.Left < 0 || traitRect.Top < 0 || traitRect.Right > screen.Width || traitRect.Bottom > screen.Height)
                {
                    return;
                }

                //grab the image
                using OCV.Mat traitImg = screen.SubMat(traitRect);

                //first, filter by slot location
                var filteredTraits = ScreenMetadata.GetSlotTraits(m_codex, column, row);

                //then filter by sample category
                List<SampleCategory> compareCategories = new();
                if (column == -1)
                {
                    compareCategories.Add(SampleCategory.Autogen);
                    compareCategories.Add(SampleCategory.PinIcons);
                }
                else
                {
                    compareCategories.Add(SampleCategory.AutogenPinned);
                    compareCategories.Add(SampleCategory.TrayIcons);
                }

                HashSet<string> possibleAdded = new();
                List<TraitMatch> possibleMatches = new();
                foreach (var filtered in filteredTraits)
                {
                    string sharedName = m_codex.ByIcon[filtered.IconFile].First().Name;

                    if (!possibleAdded.Contains(sharedName))
                    {
                        possibleAdded.Add(sharedName);
                        var classed = m_preclassifiedTraits[sharedName];

                        foreach (var category in compareCategories)
                        {
                            if (classed.ContainsKey(category))
                            {
                                possibleMatches.AddRange(classed[category]);
                            }
                        }
                    }
                }

                if (!possibleMatches.Any())
                {
                    throw new Exception($"Found 0 possible traits for slot {column}_{row}, this shouldn't happen");
                }

                //make the trait comparable with the various categories
                Dictionary<Category, OCV.Mat> traitComparables = new();
                foreach (Category category in Enum.GetValues(typeof(Category)))
                {
                    OCV.Mat comparable = traitImg;
                    if (NeedsPreprocess(m_codex.Providers.First().ProviderCategory))
                    {
                        comparable = CVUtil.MakeComparable(traitImg);
                    }

                    traitComparables.Add(category, comparable);
                }

                //cache to avoid repeatedly creating the same images
                var resized = new Dictionary<Category, Dictionary<OCV.Size, OCV.Mat>>();
                foreach (var cat in traitComparables.Keys)
                {
                    resized.Add(cat, new());
                }

                //do the comparisons
                Dictionary<string, double> matchValues = new();
                List<TraitMatch> matches = new();

                foreach (var possTrait in possibleMatches)
                {
                    var toCompare = traitComparables[possTrait.Trait.Category];
                    var mySize = possTrait.Image.Size();

                    //resize if necessary
                    if (toCompare.Size() != mySize)
                    {
                        if (resized[possTrait.Trait.Category].TryGetValue(mySize, out OCV.Mat? useImage))
                        {
                            toCompare = useImage;
                        }
                        else
                        {
                            toCompare = toCompare.Resize(mySize, 0, 0, OCV.InterpolationFlags.Cubic);
                            resized[possTrait.Trait.Category][mySize] = toCompare;
                        }
                    }

                    double psnr = Cv2.PSNR(toCompare, possTrait.Image);
                    matchValues.Add(possTrait.Filename, psnr);
                    matches.Add(possTrait);
                }

                //store results
                var ordered = matches.OrderByDescending(p => matchValues[p.Filename]).ToList();
                finalMatches.AddRange(ordered);

                //save "source" vs "best guess" thumbs
                if (debugPath != null)
                {
                    var winner = ordered.First();
                    string columnName = column == -1 ? "pin" : column.ToString();
                    traitComparables[winner.Trait.Category].SaveImage(Path.Combine(debugPath, $"{columnName}_{row}.png"));
                    winner.Image.SaveImage(Path.Combine(debugPath, $"{columnName}_{row}_guess.png"));
                }

                //clean up
                {
                    foreach (var resizeCache in resized.Values)
                    {
                        foreach (var toDispose in resizeCache)
                        {
                            toDispose.Value.Dispose();
                        }
                    }

                    foreach (var comparable in traitComparables.Values)
                    {
                        comparable.Dispose();
                    }
                }
            });

            //any missing results?
            {
                var failedSlots = slots.Where(s => !s.Matches.Any());
                if (failedSlots.Any())
                {
                    Console.WriteLine($"Found missing slots in {filePath}, classification failed");
                    return null;
                }
            }

            if (debugPath != null)
            {
                //write out the top 10 matches for each trait
                string outInfo = Path.Combine(debugPath, "result.txt");

                using StreamWriter sw = new(outInfo);

                int fromSamples = slots.Count(s => !Path.GetFileName(s.Matches.First().Filename).StartsWith("_"));
                sw.WriteLine($"From samples: {fromSamples}, from generated: {slots.Count - fromSamples}");

                foreach (var (column, row, _, bestMatches) in slots)
                {
                    string columnName = column == -1 ? "pin" : column.ToString();
                    sw.WriteLine($"{columnName}_{row}:");
                    foreach (var match in bestMatches.Take(10))
                    {
                        sw.WriteLine($"{match.Trait} ({match.Filename})");
                    }

                    sw.WriteLine();
                }
            }

            return new(m_codex, slots.Select(r => new ClassifiedScreen.Slot(r.Matches.First().Trait, r.Column, r.Row)));
        }

        public void Dispose()
        {
            foreach (var category in m_preclassifiedTraits)
            {
                foreach (var classified in category.Value)
                {
                    foreach (var traitMatch in classified.Value)
                    {
                        traitMatch.Image.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Support class
        /// </summary>
        private class TraitMatch
        {
            public string Filename { get; set; }
            public OCV.Mat Image { get; set; }

            public Trait Trait { get; set; }

            public TraitMatch(Trait trait, string filename, OCV.Mat image)
            {
                Trait = trait;
                Filename = filename;
                Image = image;
            }

            public override string ToString()
            {
                return Trait.Name;
            }
        }
    }
}