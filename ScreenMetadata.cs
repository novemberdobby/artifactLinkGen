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
            if(imageWidth <= 0)
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

        OCV.Point VerificationPos => new(517 * Multiplier, 994 * Multiplier);
        OCV.Size VerificationSize => new(29 * Multiplier, 41 * Multiplier);
        static readonly OCV.Mat IconCast;

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
    }
}
