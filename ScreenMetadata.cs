using OCV = OpenCvSharp;
using static HadesBoonBot.Codex.Provider;

namespace HadesBoonBot
{
    /// <summary>
    /// Slot info and offset variables adjusted for image size
    /// </summary>
    internal class ScreenMetadata
    {
        public ScreenMetadata(int imageWidth)
        {
            if (imageWidth <= 0)
            {
                throw new ArgumentException("Image width must be > 0", nameof(imageWidth));
            }

            //constants below are based on an FHD screenshot
            Multiplier = imageWidth / 1920.0f;
        }

        public float Multiplier { get; private set; }

        public float FirstBoonIconX => 50 * Multiplier; //X location of the first icon (equipped companion)
        public float FirstBoonIconY => 206 * Multiplier; //Y location of above
        public float SecondBoonIconX => 122 * Multiplier; //X location of the second column

        public float BoonWidth => 77 * Multiplier; //width of the diamond

        public float BoonColumnSep => 64.25f * Multiplier; //distance between columns (from 2nd column onwards)
        public float BoonRowSep => 93.6f * Multiplier; //distance between rows

        public float BoonColumnYoffset => 47 * Multiplier; //vertical offset of every second column

        public static float BoonColumnsMax => 6; //the highest number of columns that can be visible
        public static float BoonRowsMax => 7; //only the first column contains this many rows

        OCV.Point CastCheckPos => new(517 * Multiplier, 994 * Multiplier);
        OCV.Size CastCheckSize => new(29 * Multiplier, 41 * Multiplier);
        static readonly OCV.Mat IconCast;

        OCV.Point HealthCheckPos => new(62 * Multiplier, 1009 * Multiplier);
        OCV.Size HealthCheckSize => new(300 * Multiplier, 17 * Multiplier);

        static ScreenMetadata()
        {
            IconCast = OCV.Cv2.ImRead(@"icons_overlay\icon_cast.png", OCV.ImreadModes.Unchanged);
        }

        /// <summary>
        /// Find a rectangle containing the position of a trait in the image
        /// </summary>
        /// <param name="column">Trait column</param>
        /// <param name="row">Trait row</param>
        /// <param name="result">Rectangle containing trait</param>
        /// <returns>Whether a valid trait location was provided</returns>
        internal bool GetTraitRect(int column, int row, out OCV.Rect? result)
        {
            result = null;

            OCV.Point2f startPoint = column == 0 //the distance between columns 0&1 is unique
                ? new(FirstBoonIconX, FirstBoonIconY)
                : new(SecondBoonIconX - BoonColumnSep, FirstBoonIconY);

            if (row == 0 && column != 0)
            {
                //the only row-0 item is the companion (in column 0)
                return false;
            }

            if (column % 2 == 1)
            {
                if (row == BoonRowsMax - 1)
                {
                    //odd columns have 1 less item
                    return false;
                }

                startPoint.Y += BoonColumnYoffset;
            }

            OCV.Point2f separation = new(BoonColumnSep, BoonRowSep);

            //find the middle of the trait
            OCV.Point middle = new(startPoint.X + column * separation.X, startPoint.Y + row * separation.Y);

            float halfSize = BoonWidth / 2.0f;
            result = new((int)(middle.X - halfSize), (int)(middle.Y - halfSize), (int)BoonWidth, (int)BoonWidth);
            return true;
        }

        /// <summary>
        /// Restrict some slots to categories/empty
        /// </summary>
        private static readonly Dictionary<OCV.Point, Category> SlotsForCategories = new()
        {
            { new(0, 0), Category.Companions },
            { new(0, BoonRowsMax - 1), Category.Keepsakes },
        };

        /// <summary>
        /// Restrict other slots to subcategories/empty
        /// </summary>
        private static readonly Dictionary<OCV.Point, Subcategory> SlotsForSubcategories = new()
        {
            { new(0, 1), Subcategory.Attack },
            { new(0, 2), Subcategory.Special },
            { new(0, 3), Subcategory.Cast },
            { new(0, 4), Subcategory.Dash },
            { new(0, 5), Subcategory.Call },
        };

        /// <summary>
        /// Attempt to filter the available traits based on the icon location
        /// </summary>
        /// <param name="codex">Codex for trait lookup</param>
        /// <param name="column">Trait column</param>
        /// <param name="row">Trait row</param>
        /// <returns>List of possible traits</returns>
        internal static IEnumerable<Trait> GetSlotTraits(Codex codex, int column, int row)
        {
            IEnumerable<Trait> possibleTraits = codex;

            bool isCategory = SlotsForCategories.TryGetValue(new(column, row), out Category filterCat);
            bool isSubCategory = SlotsForSubcategories.TryGetValue(new(column, row), out Subcategory filterSubCat);

            if (isCategory)
            {
                possibleTraits = codex.Where(e => e.Category == filterCat);
            }
            else if (isSubCategory)
            {
                possibleTraits = codex.Where(e => e.SingletonType == filterSubCat);
            }
            else
            {
                possibleTraits = codex.Where(e => !SlotsForCategories.Values.Any(v => e.Category == v) && !SlotsForSubcategories.Values.Any(v => e.SingletonType == v));
            }

            //TODO: actually, EmptyBoon can't appear in the 5abils slots
            possibleTraits = possibleTraits.Concat(new[] { codex.EmptyBoon }); //any slot can be empty (also used as "invalid" where we're not in the tray any more)

            possibleTraits = possibleTraits.Distinct(new Codex.TraitEqualityComparer());
            return possibleTraits;
        }

        #region Screen validation

        /// <summary>
        /// Perform initial checks on a victory screen, determine whether it's a supported resolution/aspect ratio
        /// </summary>
        /// <param name="original">Victory screen</param>
        /// <param name="debugFilename">Filename for debug purposes</param>
        /// <returns>Corrected image if checks pass, otherwise null</returns>
        internal static OCV.Mat? TryMakeValidScreen(OCV.Mat original, string? debugFilename = null)
        {
            OCV.Mat image = original.Clone();

            //some images have a strange <255 alpha border (???), remove that
            if (image.Channels() == 4)
            {
                using var alpha = image.ExtractChannel(3);
                using var alphaThresh = alpha.Threshold(254, 255, OCV.ThresholdTypes.Binary);

                //compute hull
                alphaThresh.FindContours(out OCV.Point[][] alphaContours, out _, OCV.RetrievalModes.External, OCV.ContourApproximationModes.ApproxSimple);
                var alphaBound = OCV.Cv2.BoundingRect(alphaContours.SelectMany(c => c));

                if (alphaBound.Width < image.Width || alphaBound.Height < image.Height)
                {
                    //crop it out
                    var cropped = image.SubMat(alphaBound);
                    image.Dispose();
                    image = cropped;

                    if(debugFilename != null)
                    {
                        Console.WriteLine($"Found alpha channel letterboxing in {debugFilename}");
                    }
                }

                //always convert to bgr for later operations (including those beyond this function)
                var bgr = image.CvtColor(OCV.ColorConversionCodes.BGRA2BGR);
                image.Dispose();
                image = bgr;
            }

            //then undo any normal letterboxing
            using var grey = image.CvtColor(OCV.ColorConversionCodes.BGR2GRAY);
            using var thresh = grey.Threshold(0, 255, OCV.ThresholdTypes.Binary);
            thresh.FindContours(out OCV.Point[][] contours, out _, OCV.RetrievalModes.External, OCV.ContourApproximationModes.ApproxSimple);

            var bounding = OCV.Cv2.BoundingRect(contours.SelectMany(c => c));
            if (bounding.Width < image.Width || bounding.Height < image.Height)
            {
                var cropped = image.SubMat(bounding);
                image.Dispose();
                image = cropped;

                if (debugFilename != null)
                {
                    Console.WriteLine($"Found rgb letterboxing in {debugFilename}");
                }
            }

            var aspect = AspectRatio.Measure(image.Size());
            if (aspect == AspectRatio.Ratio._16_9)
            {
                //send it back, this is what we expect. if it's the right size but isn't not a victory screen (e.g. FHD camera picture), the ML pass *should* notice
                return image;
            }
            else if (aspect == AspectRatio.Ratio._21_9)
            {
                //the game adds "letterboxes" (padding images) at the sides for widescreen, so chop those off to make it 16:9
                int widthForHeight = (int)(image.Height * AspectRatio.Values[AspectRatio.Ratio._16_9]);

                OCV.Rect subArea = new(image.Width / 2 - widthForHeight / 2, 0, widthForHeight, image.Height);
                var cropped = image.SubMat(subArea);
                image.Dispose();
                return cropped;
            }

            //invalid, clean up
            image.Dispose();
            return null;
        }

        /// <summary>
        /// Determine whether this image looks like a valid victory screen
        /// </summary>
        /// <param name="screen">Screenshot</param>
        /// <returns>Validity score</returns>
        internal static int IsValidScreenML(OCV.Mat screen)
        {
            ScreenMetadata meta = new(screen.Width);

            //TODO predict without having to use any temp files
            string tempDir = Path.Combine(Path.GetTempPath(), $"hbb_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            List<Task<ML.ModelOutput>> tasks = new();
            try
            {
                //look for a cast icon which is always present at this location
                {
                    using OCV.Mat? chopped = GetRect(screen, meta.CastCheckPos, meta.CastCheckSize);
                    if(chopped == null)
                    {
                        return 0;
                    }

                    using OCV.Mat resized = chopped.Resize(IconCast.Size(), 0, 0, OCV.InterpolationFlags.Cubic);
                    using OCV.Mat withAlpha = resized.CvtColor(OCV.ColorConversionCodes.BGR2BGRA);

                    //stomp alpha
                    OCV.Cv2.MixChannels(new[] { IconCast }, new[] { withAlpha }, new[] { 3, 3 });

                    string tempFile = Path.Combine(tempDir, "cast.png");
                    withAlpha.SaveImage(tempFile);
                    var sampleData = new ML.ModelInput(tempFile);
                    tasks.Add(Task.Factory.StartNew(() => ML.CastCheckModel.Predict(sampleData)));
                }

                //check healthbar area
                {
                    using OCV.Mat? chopped = GetRect(screen, meta.HealthCheckPos, meta.HealthCheckSize);
                    if (chopped == null)
                    {
                        return 0;
                    }

                    string tempFile = Path.Combine(tempDir, "health.png");
                    chopped.SaveImage(tempFile);
                    var sampleData = new ML.ModelInput(tempFile);
                    tasks.Add(Task.Factory.StartNew(() => ML.HealthCheckModel.Predict(sampleData)));
                }
            }
            finally
            {
                Task.WaitAll(tasks.ToArray());
                Directory.Delete(tempDir, true);
            }

            //calculate score
            return tasks.Sum(t => ML.Util.IsGood(t.Result) ? 1 : 0);
        }

        #endregion

        /// <summary>
        /// Try to extract a submat from an image
        /// </summary>
        /// <param name="image">Image to extract from</param>
        /// <param name="pos">Rectangle top left</param>
        /// <param name="size">Rectangle size</param>
        /// <returns>Material, or null if the bounds weren't valid</returns>
        private static OCV.Mat? GetRect(OCV.Mat image, OCV.Point pos, OCV.Size size)
        {
            OCV.Rect rect = new(pos, size);
            if (rect.Left < 0 || rect.Top < 0 || rect.Right > image.Width || rect.Bottom > image.Height)
            {
                return null;
            }

            return image.SubMat(rect);
        }
    }
}
