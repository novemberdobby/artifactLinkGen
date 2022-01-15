﻿using OpenCvSharp;

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

        /// <summary>
        /// Find a rectangle containing the position of a trait in the image
        /// </summary>
        /// <param name="column">Trait column</param>
        /// <param name="row">Trait row</param>
        /// <param name="result">Rectangle containing trait</param>
        /// <returns>Whether a valid trait location was provided</returns>
        internal bool FindTrait(int column, int row, out Rect? result)
        {
            result = null;

            Point2f startPoint = column == 0 //the distance between columns 0&1 is unique
                ? new(FirstBoonIconX, FirstBoonIconY)
                : new(SecondBoonIconX - BoonColumnSep, FirstBoonIconY);

            if (column % 2 == 1)
            {
                if (row == BoonRowsMax - 1)
                {
                    //odd columns have 1 less item
                    return false;
                }

                startPoint.Y += BoonColumnYoffset;
            }

            Point2f separation = new(BoonColumnSep, BoonRowSep);

            //find the middle of the trait
            Point middle = new(startPoint.X + column * separation.X, startPoint.Y + row * separation.Y);

            float halfSize = BoonWidth / 2.0f;
            result = new((int)(middle.X - halfSize), (int)(middle.Y - halfSize), (int)BoonWidth, (int)BoonWidth);
            return true;
        }
    }
}
