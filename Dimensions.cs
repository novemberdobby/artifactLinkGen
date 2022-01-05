namespace HadesBoonBot
{
    /// <summary>
    /// Constants adjusted for image size
    /// </summary>
    internal class Dimensions
    {
        public Dimensions(int imageWidth)
        {
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
    }
}
