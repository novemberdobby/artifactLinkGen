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

        public GenerateTraitsOptions()
        {
            TrainingData = string.Empty;
            OutputDir = string.Empty;
        }
    }

    internal class TraitDataGen
    {
        internal void Run(GenerateTraitsOptions options, Codex codex)
        {
            TrainingData inputData = TrainingData.Load(options.TrainingData);

            if (options.Clean)
            {
                Directory.Delete(options.OutputDir, true);
            }

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

                if(!(screen.IsValid ?? true))
                {
                    Console.WriteLine($"Skipping screen as it's invalid: {screen.FileName}");
                    continue;
                }

                Lazy<OCV.Mat> image = new(() =>
                {
                    Console.WriteLine($"Reading {screen.FileName}");
                    using var loaded = Cv2.ImRead(screen.FileName, OCV.ImreadModes.Unchanged);
                    var validated = ScreenMetadata.TryMakeValidScreen(loaded);
                    if(validated == null)
                    {
                        throw new Exception($"Screen is no longer valid: {screen.FileName}");
                    }

                    return validated;
                });

                Lazy<ScreenMetadata> meta = new(() => new(image.Value));

                //save trait diamonds
                foreach (var trait in screen.Traits)
                {
                    if(trait.Name == null)
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
                    string targetDir = Path.Combine(options.OutputDir, traitName);
                    string targetFile = Path.Combine(targetDir, $"{Path.GetFileName(screen.FileName)}_{trait.Col}_{trait.Row}.png");
                    if (!File.Exists(targetFile))
                    {
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        if (meta.Value.TryGetTraitRect(trait.Col, trait.Row, out OCV.Rect? traitRect))
                        {
                            using OCV.Mat traitImg = image.Value.SubMat(traitRect!.Value);
                            using var comparable = CVUtil.MakeComparable(traitImg);
                            comparable.SaveImage(targetFile);
                        }
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

            if (codex.Any(t => t.Icon == null))
            {
                throw new Exception("One or more trait icons are null when generating fake sample data");
            }

            foreach (var trait in codex)
            {
                string targetDir = Path.Combine(options.OutputDir, trait.Name);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                //go through each combination
                fakeSamples += Mutation.Max - Mutation.Original;
                Parallel.For((int)Mutation.Original, (int)Mutation.Max, m =>
                {
                    Mutation mutate = (Mutation)m;
                    string id = mutate.ToString().Replace(", ", "_");

                    string targetFile = Path.Combine(targetDir, $"_{id}.png"); //prefix with underscore so we know which are real/generated
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

                        //ditch the alpha channel
                        using var bgr = image.CvtColor(OCV.ColorConversionCodes.BGRA2BGR);
                        bgr.SaveImage(targetFile);
                        image.Dispose();
                    }
                });
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
