using static HadesBoonBot.Codex.Provider;
using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    /// <summary>
    /// Classify traits on a victory screen by running PSNR comparisons against previously-classified training data
    /// </summary>
    internal class ClassifierPSNR
    {
        internal int Run(string[] args, Lazy<Codex> codex)
        {
#if DEBUG
            const bool outputResults = true;
#else
            const bool outputResults = false;
#endif

            string inputImageDir = args[1];
            string classifiedTraitSource = args[2];

            //easy trait categorisation
            Dictionary<string, Category> categoryMap = new();
            foreach (var item in codex.Value)
            {
                categoryMap[item.Name!] = item.Category;
            }

            //load up the baseline images
            Dictionary<string, List<TraitMatch>> classifiedTraits = new();
            foreach (string traitFolder in Directory.EnumerateDirectories(classifiedTraitSource))
            {
                string traitName = Path.GetFileName(traitFolder);
                List<TraitMatch> imagesForTrait = new();
                classifiedTraits.Add(traitName, imagesForTrait);
                Category category = categoryMap[traitName];

                foreach (var imagePath in Directory.EnumerateFiles(traitFolder))
                {
                    imagesForTrait.Add(new(imagePath, category, Cv2.ImRead(imagePath)));
                }
            }

            //iterate through victory screens
            foreach (var inputImage in Directory.EnumerateFiles(inputImageDir))
            {
                string? resultPath = null;
                if (outputResults)
                {
                    resultPath = Path.Combine(inputImageDir, Path.GetFileNameWithoutExtension(inputImage) + "_results");
                    if (!Directory.Exists(resultPath))
                    {
                        Directory.CreateDirectory(resultPath);
                    }
                }

                var image = Cv2.ImRead(inputImage);
                ScreenMetadata meta = new(image.Width);
                List<(int column, int row, List<TraitMatch> matches)> results = new();

                for (int row = 0; row < ScreenMetadata.BoonRowsMax; row++)
                {
                    for (int column = 0; column < ScreenMetadata.BoonColumnsMax; column++)
                    {
                        if (meta.GetTraitRect(column, row, out OCV.Rect? traitRect))
                        {
                            //grab the image
                            OCV.Mat traitImg = image.SubMat(traitRect!.Value);

                            //first, filter by slot
                            var filteredTraits = meta.FindPossibleTraits(codex.Value, column, row);

                            //make the trait comparable with the various categories
                            Dictionary<Category, OCV.Mat> traitComparables = new();
                            foreach (Category category in Enum.GetValues(typeof(Category)))
                            {
                                bool needsPreprocess = NeedsPreprocess(codex.Value.Providers.First().ProviderCategory);
                                traitComparables.Add(category, needsPreprocess ? CVUtil.MakeComparable(traitImg) : traitImg);
                            }

                            //build up a list of images to compare against
                            List<TraitMatch> possibleTraits = new();

                            foreach (var filtered in filteredTraits)
                            {
                                if (!classifiedTraits.TryGetValue(filtered.Name!, out List<TraitMatch>? thisPossibleTraits))
                                {
                                    throw new Exception($"Unable to classify; missing any baseline data for {filtered.Name!}");
                                }

                                possibleTraits.AddRange(thisPossibleTraits);
                            }

                            //cache to avoid repeatedly creating the same images
                            var resized = new Dictionary<Category, Dictionary<OCV.Size, OCV.Mat>>();
                            foreach (var cat in traitComparables.Keys)
                            {
                                resized.Add(cat, new());
                            }

                            //do the comparisons
                            List<TraitMatch> psnrValues = new();
                            Parallel.ForEach(possibleTraits, t =>
                            {
                                var toCompare = traitComparables[t.Category];
                                var mySize = t.Image.Size();

                                //resize if necessary
                                if (toCompare.Size() != mySize)
                                {
                                    lock (resized)
                                    {
                                        if (resized[t.Category].TryGetValue(mySize, out OCV.Mat? useImage))
                                        {
                                            toCompare = useImage;
                                        }
                                        else
                                        {
                                            toCompare = toCompare.Resize(mySize, 0, 0, OCV.InterpolationFlags.Cubic);
                                            resized[t.Category][mySize] = toCompare!;
                                        }
                                    }
                                }

                                double psnr = Cv2.PSNR(toCompare, t.Image);
                                lock (psnrValues)
                                {
                                    t.PSNR = psnr;
                                    psnrValues.Add(t);
                                }
                            });

                            //clean up
                            foreach (var resizeCache in resized.Values)
                            {
                                foreach (var toDispose in resizeCache)
                                {
                                    toDispose.Value.Dispose();
                                }
                            }

                            //store results
                            var ordered = psnrValues.OrderByDescending(p => p.PSNR).ToList();
                            results.Add((column, row, ordered));

                            //save "source" vs "best guess" thumbs
                            if (outputResults)
                            {
                                var winner = ordered.First();
                                traitComparables[winner.Category].SaveImage(Path.Combine(resultPath, $"{column}_{row}.png"));
                                winner.Image.SaveImage(Path.Combine(resultPath, $"{column}_{row}_guess.png"));
                            }
                        }
                    }
                }

                if (outputResults)
                {
                    //write out the top 10 matches for each trait
                    string outInfo = Path.Combine(resultPath, "result.txt");

                    using StreamWriter sw = new(outInfo);
                    foreach (var (column, row, matches) in results)
                    {
                        sw.WriteLine($"{column}_{row}:");
                        foreach (var match in matches.Take(10))
                        {
                            sw.WriteLine(match.TraitName);
                        }

                        sw.WriteLine();
                    }
                }
            }

            return 0;
        }

        private class TraitMatch
        {
            public string Filename { get; set; }
            public Category Category { get; set; }
            public OCV.Mat Image { get; set; }
            public double PSNR { get; set; }

            public TraitMatch(string filename, Category category, OCV.Mat image)
            {
                Filename = filename;
                Category = category;
                Image = image;
            }

            public string TraitName => Path.GetFileName(Path.GetDirectoryName(Filename))!;

            public override string ToString()
            {
                return $"{Filename} ({Category})";
            }
        }
    }
}