using OCV = OpenCvSharp;
using static HadesBoonBot.Codex.Provider;

namespace HadesBoonBot
{
    /// <summary>
    /// Slot info and offset variables adjusted for image size
    /// </summary>
    internal class ScreenMetadata
    {
        public ScreenMetadata(OCV.Mat image)
        {
            if (image.Width <= 0)
            {
                throw new ArgumentException("Image width must be > 0", nameof(image));
            }

            //constants below are based on an FHD screenshot
            Multiplier = image.Width / 1920.0f;
        }

        public readonly float Multiplier;

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

        OCV.Point BackButtonCheckPos => new(1575 * Multiplier, 963 * Multiplier);
        OCV.Size BackButtonCheckSize => new(52 * Multiplier, 52 * Multiplier);
        static readonly OCV.Mat IconBackButton;

        int TrayMaskTop => (int)(248 * Multiplier);
        int TrayMaskHeight => (int)(592 * Multiplier);

        OCV.Point[] TrayFillPositions => new[]
        {
            new OCV.Point(186 * Multiplier, 822 * Multiplier),
            new OCV.Point(121 * Multiplier, 815 * Multiplier),
        };

        /// <summary>
        /// How many columns the tray contains if the right side of it is near these X co-ordinates. Screens with more than 6 columns are not supported as this bugs out ingame (example: oew3lir35h081)
        /// </summary>
        public static readonly Dictionary<int, int> TrayRightToColumnCount = new()
        {
            { 232, 3 },
            { 298, 4 },
            { 365, 5 },
            { 432, 6 },
        };

        static ScreenMetadata()
        {
            IconCast = OCV.Cv2.ImRead(@"icons_overlay\icon_cast.png", OCV.ImreadModes.Unchanged);
            IconBackButton = OCV.Cv2.ImRead(@"icons_overlay\icon_back.png", OCV.ImreadModes.Unchanged);
        }

        internal bool TryGetTrayColumnCount(OCV.Mat image, out int columns, out OCV.Rect trayRect)
        {
            /*
               find the tray area. the only measurement we care about is the right hand side of it, which tells us:
                a) how many columns we need to search for traits
                b) where any pinned traits will appear horizontally

                note: the tray expands with the addition of new traits, but doesn't shrink as they're removed (e.g. by purging boons)
            */

            //mask out the top & bottom of the image where the tray can't appear
            using OCV.Mat trayMask = new(new OCV.Size(image.Width + 2, image.Height + 2), OCV.MatType.CV_8UC1, OCV.Scalar.Black);

            trayMask.Rectangle(new OCV.Rect(0, 0, trayMask.Width, TrayMaskTop), OCV.Scalar.White, -1);
            trayMask.Rectangle(new OCV.Rect(0, TrayMaskTop + TrayMaskHeight, trayMask.Width, TrayMaskHeight), OCV.Scalar.White, -1);

            trayRect = new(TrayFillPositions.First(), OCV.Size.Zero);
            OCV.Scalar tolerance = new(15, 15, 15);

            for (int i = 0; i < TrayFillPositions.Length; i++)
            {
                OCV.Mat thisTrayMask = (i == 0 ? trayMask : trayMask.Clone());

                image.FloodFill(thisTrayMask, TrayFillPositions[i], OCV.Scalar.Orange, out var thisTrayRect, tolerance, tolerance, OCV.FloodFillFlags.MaskOnly);
                trayRect = trayRect.Union(thisTrayRect);

                if (i > 0)
                {
                    thisTrayMask.Dispose();
                }
            }

            //clean it up
            trayRect = new(0, TrayMaskTop, trayRect.Right, TrayMaskHeight);

            //find which expected value it's closest to
            int normalisedRight = (int)(trayRect.Right / Multiplier);
            int closestRight = -1;
            int closestRightDist = int.MaxValue;

            foreach (var expectedRight in TrayRightToColumnCount.Keys)
            {
                int thisDist = Math.Abs(expectedRight - normalisedRight);
                if (thisDist < closestRightDist)
                {
                    closestRightDist = thisDist;
                    closestRight = expectedRight;
                }
            }

            //is it within a tolerance of that value?
            int rightDiff = Math.Abs(closestRight - normalisedRight);
            if (rightDiff <= 15)
            {
                columns = TrayRightToColumnCount[closestRight];
                return true;
            }
            else
            {
                columns = -1;
                return false;
            }
        }

        /// <summary>
        /// Find a rectangle containing the position of a trait in the image
        /// </summary>
        /// <param name="column">Trait column</param>
        /// <param name="row">Trait row</param>
        /// <param name="result">Rectangle containing trait</param>
        /// <returns>Whether a valid trait location was provided</returns>
        internal bool TryGetTraitRect(int column, int row, out OCV.Rect? result)
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
        /// Get the full path of a folder we can use to output debug images, living next to filePath and prefixed with its name (without the extension)
        /// </summary>
        /// <param name="filePath">Full file path</param>
        /// <param name="debugTypeName">Suffix added to the file's name</param>
        /// <returns>Debug path</returns>
        public static string GetDebugOutputFolder(string filePath, string debugTypeName)
        {
            string parentPath = Path.GetDirectoryName(filePath)!;
            string debugPath = Path.Combine(parentPath, $"{Path.GetFileNameWithoutExtension(filePath)}_{debugTypeName}");
            if (!Directory.Exists(debugPath))
            {
                Directory.CreateDirectory(debugPath);
            }

            return debugPath;
        }

        /// <summary>
        /// Get the full path of a folder we can use to output debug images, living next to filePath and based on its name (without the extension)
        /// </summary>
        /// <param name="filePath">Full file path</param>
        /// <returns>Debug path</returns>
        public static string GetDebugOutputFolder(string filePath)
        {
            string parentPath = Path.GetDirectoryName(filePath)!;
            string debugPath = Path.Combine(parentPath, Path.GetFileNameWithoutExtension(filePath));
            if (!Directory.Exists(debugPath))
            {
                Directory.CreateDirectory(debugPath);
            }

            return debugPath;
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
        /// <param name="minimumScore">Score a screen must achieve to be considered valid</param>
        /// <returns>Validity</returns>
        internal bool IsValidScreenML(OCV.Mat screen, List<ML.Model> models, int minimumScore)
        {
            ScreenMetadata meta = new(screen);

            //TODO predict without having to use any temp files
            string tempDir = Path.Combine(Path.GetTempPath(), $"hbb_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            int score = 0;
            try
            {
                List<(ML.Model, string)> modelFiles = new();
                foreach (ML.Model model in models)
                {
                    string tempFile = Path.Combine(tempDir, $"{model.Name}.png");
                    modelFiles.Add((model, tempFile));

                    //if any fail to extract then there's no need to run ML at all
                    if (!model.Extract(this, screen, tempFile))
                    {
                        return false;
                    }
                }

                foreach ((ML.Model model, string tempFile) in modelFiles)
                {
                    var sampleData = new ML.ModelInput(tempFile);
                    var result = model.Predict(sampleData);
                    if (ML.Util.IsGood(result))
                    {
                        //return success as soon as possible
                        score++;
                        if (score >= minimumScore)
                        {
                            return true;
                        }
                    }
                }
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }

            return false;
        }

        internal static bool ExtractML_CastCheck(ScreenMetadata meta, OCV.Mat screen, string filename)
        {
            //look for a cast icon which is always present at this location
            using OCV.Mat? chopped = GetRect(screen, meta.CastCheckPos, meta.CastCheckSize);
            if (chopped != null)
            {
                using OCV.Mat resized = chopped.Resize(IconCast.Size(), 0, 0, OCV.InterpolationFlags.Cubic);
                using OCV.Mat withAlpha = resized.CvtColor(OCV.ColorConversionCodes.BGR2BGRA);

                //stomp alpha
                OCV.Cv2.MixChannels(new[] { IconCast }, new[] { withAlpha }, new[] { 3, 3 });
                return withAlpha.SaveImage(filename);
            }

            return false;
        }

        internal static bool ExtractML_HealthCheck(ScreenMetadata meta, OCV.Mat screen, string filename)
        {
            //check healthbar area
            using OCV.Mat? chopped = GetRect(screen, meta.HealthCheckPos, meta.HealthCheckSize);
            if (chopped != null)
            {
                return chopped.SaveImage(filename);
            }

            return false;
        }

        internal static bool ExtractML_BackButtonCheck(ScreenMetadata meta, OCV.Mat screen, string filename)
        {
            //look for the "back" button under the victory stats panel
            using OCV.Mat? chopped = GetRect(screen, meta.BackButtonCheckPos, meta.BackButtonCheckSize);
            if (chopped != null)
            {
                using OCV.Mat resized = chopped.Resize(IconBackButton.Size(), 0, 0, OCV.InterpolationFlags.Cubic);
                using OCV.Mat withAlpha = resized.CvtColor(OCV.ColorConversionCodes.BGR2BGRA);

                //stomp alpha
                OCV.Cv2.MixChannels(new[] { IconBackButton }, new[] { withAlpha }, new[] { 3, 3 });
                return withAlpha.SaveImage(filename);
            }

            return false;
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
