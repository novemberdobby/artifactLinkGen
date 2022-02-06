using static HadesBoonBot.Codex.Provider;
using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    /// <summary>
    /// Classify traits on a victory screen by running PSNR comparisons against previously-classified training data
    /// </summary>
    internal class ClassifierPSNR : IClassifier, IDisposable
    {
        /// <summary>
        /// List of previously classified images, by trait name
        /// </summary>
        private readonly Dictionary<string, List<TraitMatch>> m_preclassifiedTraits = new();
        private readonly Codex m_codex;

        public ClassifierPSNR(string[] args, Codex codex)
        {
            string classifiedTraitSource = args[1];
            m_codex = codex;

            //load up possible matches
            foreach (string traitNameFolder in Directory.EnumerateDirectories(classifiedTraitSource))
            {
                string traitName = Path.GetFileName(traitNameFolder);

                List<TraitMatch> imagesForTrait = new();
                m_preclassifiedTraits.Add(traitName, imagesForTrait);

                Parallel.ForEach(Directory.EnumerateFiles(traitNameFolder), file =>
                {
                    var trait = m_codex.ByName[traitName];
                    var image = Cv2.ImRead(file);
                    lock (imagesForTrait)
                    {
                        imagesForTrait.Add(new(trait, file, image));
                    }
                });
            }

            //does every trait have at least one sample image?
            var missingTraits = m_codex.Where(t => !m_preclassifiedTraits.ContainsKey(t.Name!));
            if (missingTraits.Any())
            {
                string missingStr = string.Join(Environment.NewLine, missingTraits.Select(t => t.Name));
                throw new Exception($"Unable to construct {nameof(ClassifierPSNR)}; missing sample data for one or more traits: {missingStr}");
            }
        }

        public ClassifiedScreen? Classify(OCV.Mat screen, string filePath, bool debugOutput)
        {
            string? debugPath = null;
            if (debugOutput)
            {
                string parentPath = Path.GetDirectoryName(filePath)!;
                debugPath = Path.Combine(parentPath, Path.GetFileNameWithoutExtension(filePath) + "_results_psnr");
                if (!Directory.Exists(debugPath))
                {
                    Directory.CreateDirectory(debugPath);
                }
            }

            ScreenMetadata meta = new(screen.Width);
            List<(int Column, int Row, OCV.Rect traitRect, List<TraitMatch> Matches)> slots = new();

            //build list of potential trait locations on the screen
            for (int column = 0; column < ScreenMetadata.BoonColumnsMax; column++)
            {
                for (int row = 0; row < ScreenMetadata.BoonRowsMax; row++)
                {
                    if (meta.GetTraitRect(column, row, out OCV.Rect? traitRect))
                    {
                        slots.Add((column, row, traitRect!.Value, new()));
                    }
                }
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

                List<TraitMatch> possibleTraits = new();
                foreach (var filtered in filteredTraits)
                {
                    possibleTraits.AddRange(m_preclassifiedTraits[filtered.Name!]);
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

                foreach (var possTrait in possibleTraits)
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
                            resized[possTrait.Trait.Category][mySize] = toCompare!;
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
                    traitComparables[winner.Trait.Category].SaveImage(Path.Combine(debugPath, $"{column}_{row}.png"));
                    winner.Image.SaveImage(Path.Combine(debugPath, $"{column}_{row}_guess.png"));
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

            //detect when we run out of traits, otherwise we might start picking up pinned items/random other bits of the screen
            {
                bool emptySlotsFound = false;
                int emptySlotRun = 0;
                const int maxEmptySlotRun = 3;
                string shortFile = Path.GetFileName(filePath);

                for (int i = 0; i < slots.Count; i++)
                {
                    var (Column, _, _, Matches) = slots[i];

                    //skip first column as people may choose to leave their base upgrades empty (weird tbh)
                    if (Column == 0)
                    {
                        continue;
                    }

                    var bestMatch = Matches.First();
                    if (!Codex.IsSlotFilled(bestMatch.Trait))
                    {
                        emptySlotRun++;
                    }
                    else
                    {
                        emptySlotRun = 0;
                    }

                    if (emptySlotRun >= maxEmptySlotRun)
                    {
                        int removeFrom = i - maxEmptySlotRun;
                        Console.WriteLine($"Detected empty slots after #{removeFrom} in {shortFile}");
                        slots.RemoveRange(removeFrom, slots.Count - removeFrom);
                        emptySlotsFound = true;
                        break;
                    }
                }

                if (!emptySlotsFound)
                {
                    Console.WriteLine($"Failed to detect a run of empty slots in {shortFile}. This could be suspicious but doesn't necessarily indicate an invalid screen.");
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
                    sw.WriteLine($"{column}_{row}:");
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
            foreach (var classified in m_preclassifiedTraits)
            {
                foreach (var item in classified.Value)
                {
                    item.Image.Dispose();
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
        }
    }
}