using CommandLine;
using System.Reflection;
using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;

namespace HadesBoonBot.Training
{
    [Verb("generate_traits", HelpText = "Create sample data for each trait type, based on raw icons and manually classified victory screens")]
    internal class GenerateTraitsOptions
    {
        [Option('c', "clean", Required = false, Default = false, HelpText = "Clean output directory first")]
        public bool Clean { get; set; }

        [Option('t', "training_data", Required = true, HelpText = "Training data file")]
        public string TrainingData { get; set; }

        [Option('o', "output_dir", Required = true, HelpText = "Output data directory")]
        public string OutputDir { get; set; }

        [Option('f', "flat", Required = false, Default = false, HelpText = "Create all folders at top-level (required for ML.net)")]
        public bool Flat { get; set; }

        public GenerateTraitsOptions()
        {
            TrainingData = string.Empty;
            OutputDir = string.Empty;
        }
    }

    internal class TraitDataGen
    {
        internal enum SampleCategory
        {
            /// <summary>
            /// Created from codex icons with mutations (no pin overlays)
            /// </summary>
            Autogen,

            /// <summary>
            /// Created from codex icons with mutations (all pin overlays)
            /// </summary>
            AutogenPinned,

            /// <summary>
            /// Created from classified victory screens (trait tray)
            /// </summary>
            TrayIcons,

            /// <summary>
            /// Created from classified victory screens (pin rows)
            /// </summary>
            PinIcons,
        }

        internal void Run(GenerateTraitsOptions options, Codex codex)
        {
            TrainingData inputData = TrainingData.Load(options.TrainingData);
            Util.CreateDir(options.OutputDir, options.Clean);

            //track how many real/artificial examples we're creating
            Dictionary<string, int> realSamples = new();
            int fakeSamples = 0;

            //save out sample data from manually classified victory screens
            foreach (TrainingData.Screen screen in inputData.Screens)
            {
                if (!File.Exists(screen.FileName))
                {
                    Console.Error.WriteLine($"Skipping {screen.FileName} due to missing file");
                    continue;
                }

                if (!(screen.IsValid ?? true))
                {
                    Console.WriteLine($"Skipping screen as it's invalid: {screen.FileName}");
                    continue;
                }

                Lazy<OCV.Mat> image = new(() =>
                {
                    Console.WriteLine($"Reading {screen.FileName}");
                    using var loaded = Cv2.ImRead(screen.FileName, OCV.ImreadModes.Unchanged);
                    var validated = ScreenMetadata.TryMakeValidScreen(loaded);
                    if (validated == null)
                    {
                        throw new Exception($"Screen is no longer valid: {screen.FileName}");
                    }

                    return validated;
                });

                Lazy<ScreenMetadata> meta = new(() => new(image.Value));

                IEnumerable<(TrainingData.Screen.Trait trait, bool isPin)> traits = screen.Traits.Select(trait => (trait, false));
                if (screen.PinnedTraits != null)
                {
                    traits = traits.Concat(screen.PinnedTraits.Select(trait => (trait, true)));
                }

                //save tray trait diamonds
                foreach ((var trait, bool isPin) in traits)
                {
                    if (trait.Name == null)
                    {
                        Console.Error.WriteLine($"Found a null trait name in {screen.FileName}");
                        continue;
                    }

                    //for traits that share icons, always use the first name alphabetically
                    var sharedIcons = codex.GetIconSharingTraits(trait.Name);
                    string traitName = sharedIcons.First().Name;

                    //make sure all sharers have an entry in the dict, so we can check any that have been left out later
                    foreach (var sharer in sharedIcons)
                    {
                        if (!realSamples.ContainsKey(sharer.Name))
                        {
                            realSamples[sharer.Name] = 0;
                        }
                    }

                    realSamples[traitName]++;

                    //save it with a name pointing back to the source
                    string targetDir = options.Flat
                        ? Path.Combine(options.OutputDir, $"{traitName}_{(isPin ? SampleCategory.PinIcons : SampleCategory.TrayIcons)}")
                        : Path.Combine(options.OutputDir, traitName, (isPin ? SampleCategory.PinIcons : SampleCategory.TrayIcons).ToString());

                    string targetFile = Path.Combine(targetDir, $"{Path.GetFileName(screen.FileName)}_{trait.GetPos()}.png");
                    if (!File.Exists(targetFile))
                    {
                        Util.CreateDir(targetDir);

                        OCV.Rect? iconRect = null;
                        if (isPin)
                        {
                            if (screen.ColumnCount.HasValue)
                            {
                                iconRect = meta.Value.GetPinRect(screen.ColumnCount.Value, trait.Row).iconRect;
                            }
                            else
                            {
                                throw new Exception($"Failed to get pinned trait rect for {trait} in {screen.FileName} - unknown tray column count");
                            }
                        }
                        else
                        {
                            if (meta.Value.TryGetTraitRect(trait.Col, trait.Row, out OCV.Rect? traitRect))
                            {
                                iconRect = traitRect!.Value;
                            }
                            else
                            {
                                throw new Exception($"Failed to get tray trait rect for {trait} in {screen.FileName}");
                            }
                        }

                        using OCV.Mat traitImg = image.Value.SubMat(iconRect.Value);
                        using var comparable = CVUtil.MakeComparable(traitImg);
                        comparable.SaveImage(targetFile);
                    }
                }

                if (image.IsValueCreated)
                {
                    image.Value.Dispose();
                }
            }

            //are there any traits we don't have any real-world samples of?
            var missingSamples = codex.Where(t => !realSamples.ContainsKey(t.Name));
            foreach (var trait in missingSamples)
            {
                Console.WriteLine($"Warning: no real-world sample data for trait \"{trait.Name}\"");
            }

            //also save out the raw icons with modifications to emulate their possible appearance in screenshots
            var mutationMethods = GetType()
                                    .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                                    .Where(m => m.GetCustomAttribute<MutateMethod>() != null)
                                    .ToDictionary(m => m.GetCustomAttribute<MutateMethod>()!.MutateType, m => m);

            var mutationTypes = Enum.GetValues(typeof(Mutation))
                                    .OfType<Mutation>()
                                    .Where(m => m != Mutation.Max);

            //ensure we have one of each
            if (!mutationMethods.Keys.SequenceEqual(mutationTypes))
            {
                throw new Exception("Failed to find one method for each mutation type");
            }

            foreach (var trait in codex.ByIcon.Values.Select(bi => bi.First()))
            {
                if (trait.Icon == null)
                {
                    throw new Exception("One or more trait icons are null when generating fake sample data");
                }

                string targetDir = Util.CreateDir(options.Flat
                    ? Path.Combine(options.OutputDir, $"{trait.Name}_{SampleCategory.Autogen}")
                    : Path.Combine(options.OutputDir, trait.Name, SampleCategory.Autogen.ToString()));

                string targetDirPinned = Util.CreateDir(options.Flat
                    ? Path.Combine(options.OutputDir, $"{trait.Name}_{SampleCategory.AutogenPinned}")
                    : Path.Combine(options.OutputDir, trait.Name, SampleCategory.AutogenPinned.ToString()));
                
                //go through each combination
                fakeSamples += Mutation.Max - Mutation.Original;
                Parallel.For((int)Mutation.Original, (int)Mutation.Max, m =>
                {
                    Mutation mutate = (Mutation)m;
                    string id = mutate.ToString().Replace(", ", "_");

                    bool isPinned = (mutate & Mutation.Pinned) == Mutation.Pinned;
                    if (isPinned && !Codex.IsSlotFilled(trait))
                    {
                        //it's not possible to pin empty slots (:
                        return;
                    }

                    string thisTargetDir = isPinned ? targetDirPinned : targetDir;

                    string targetFile = Path.Combine(thisTargetDir, $"_{id}.png"); //prefix with underscore so we know which are real/generated
                    if (!File.Exists(targetFile))
                    {
                        OCV.Mat image = trait.Icon!.Clone();
                        foreach (Mutation thisMutation in mutationTypes)
                        {
                            if ((mutate & thisMutation) == thisMutation)
                            {
                                MethodInfo modifyFunc = mutationMethods[thisMutation];
                                OCV.Mat modified = (OCV.Mat)modifyFunc.Invoke(null, new[] { image })!; //these all return clones
                                image.Dispose();
                                image = modified;
                            }
                        }

                        //ditch the alpha channel & convert to a boondiamond
                        using var bgr = image.CvtColor(OCV.ColorConversionCodes.BGRA2BGR);
                        using var tidied = CVUtil.MakeComparable(bgr);

                        tidied.SaveImage(targetFile);
                        image.Dispose();
                    }
                });
            }

            //clean up any empty dirs or ML.net will complain
            if (options.Flat)
            {
                foreach (var traitDir in Directory.GetDirectories(options.OutputDir))
                {
                    if (!Directory.EnumerateFiles(traitDir).Any())
                    {
                        Directory.Delete(traitDir);
                    }
                }
            }

            Console.WriteLine($"Generated {realSamples.Sum(s => s.Value)} real samples and {fakeSamples} artificial");
        }

        #region Mutation
#pragma warning disable IDE0051 // Remove unused private members

        /// <summary>
        /// Modifications we can make to the raw trait icons to emulate what might appear in victory screens
        /// </summary>
        [Flags]
        enum Mutation
        {
            Original = 1,
            Pinned = 2,
            Highlighted = 4,
            ScaledDown = 8,
            LowJpgQuality = 16,

            Max = 32,
        }

        [AttributeUsage(AttributeTargets.Method)]
        private class MutateMethod : Attribute
        {
            public readonly Mutation MutateType;
            public MutateMethod(Mutation mutationType)
            {
                MutateType = mutationType;
            }
        }

        [MutateMethod(Mutation.Original)]
        private static OCV.Mat ModifyNone(OCV.Mat input)
        {
            return input.Clone();
        }

        [MutateMethod(Mutation.Pinned)]
        private static OCV.Mat ModifyPin(OCV.Mat input)
        {
            return CVUtil.Blend(input, CVUtil.OverlayPin, CVUtil.BlendMode.Blend);
        }

        [MutateMethod(Mutation.Highlighted)]
        private static OCV.Mat ModifyHighlight(OCV.Mat input)
        {
            return CVUtil.Blend(input, CVUtil.OverlayHover, CVUtil.BlendMode.Additive);
        }

        [MutateMethod(Mutation.ScaledDown)]
        private static OCV.Mat ModifyHalfsize(OCV.Mat input)
        {
            OCV.Size smaller = new(input.Width / 2, input.Height / 2);
            return input.Resize(smaller, 0, 0, OCV.InterpolationFlags.Cubic);
        }

        [MutateMethod(Mutation.LowJpgQuality)]
        private static OCV.Mat ModifyJpg(OCV.Mat input)
        {
            using var ms = input.ToMemoryStream(".jpg", new OCV.ImageEncodingParam(OCV.ImwriteFlags.JpegQuality, 20));
            return OCV.Mat.FromStream(ms, OCV.ImreadModes.Unchanged);
        }

#pragma warning restore IDE0051 // Remove unused private members
        #endregion
    }
}
