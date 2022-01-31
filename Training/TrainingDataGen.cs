using System.Reflection;
using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    internal class TrainingDataGen
    {
        internal void Run(string[] args, Codex codex)
        {
            TrainingData inputData = TrainingData.Load(args[1])!;
            string outputDataDir = args[2];

            //track how many real/artificial examples we're creating
            Dictionary<string, int> realSamples = new();
            int fakeSamples = 0;

            //save out sample data from manually classified victory screens
            foreach (TrainingData.Screen screen in inputData.Screens!)
            {
                if (!File.Exists(screen.FileName))
                {
                    Console.WriteLine($"Skipping {screen.FileName} due to missing file");
                    continue;
                }

                Lazy<OCV.Mat> image = new(() =>
                {
                    Console.WriteLine($"Reading {screen.FileName}");
                    return Cv2.ImRead(screen.FileName);
                });

                foreach (var trait in screen.Traits!)
                {
                    string traitName = trait.Name!;
                    var sharedIcons = codex.GetIconSharingTraits(traitName);
                    traitName = sharedIcons.First().Name!;

                    //for traits that share icons, always use the first name alphabetically
                    foreach (var sharer in sharedIcons)
                    {
                        if (!realSamples.ContainsKey(sharer.Name!))
                        {
                            realSamples[sharer.Name!] = 0;
                        }
                    }

                    realSamples[traitName]++;

                    //save it with a name pointing back to the source
                    string targetDir = Path.Combine(outputDataDir, traitName);
                    string targetFile = Path.Combine(targetDir, $"{Path.GetFileName(screen.FileName)}_{trait.Col}_{trait.Row}.png");
                    if (!File.Exists(targetFile))
                    {
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }

                        ScreenMetadata dim = new(image.Value.Width);
                        if (dim.GetTraitRect(trait.Col, trait.Row, out OCV.Rect? traitRect))
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
            var missingSamples = codex.Where(t => !realSamples.ContainsKey(t.Name!));
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

            foreach (var trait in codex)
            {
                string targetDir = Path.Combine(outputDataDir, trait.Name!);
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
