using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    internal class CVUtil
    {
        /// <summary>
        /// Mask out the 4 corners of an image to create a diamond
        /// </summary>
        /// <param name="input">Image to draw on</param>
        /// <param name="size">Normalised diamond size, <1 values will draw larger corner masks to reduce the size of the diamond</param>
        public static OCV.Mat MakeComparable(OCV.Mat input, float size = 0.9f)
        {
            var output = input.Clone();

            size = 1 - (size / 2.0f);
            int halfX = (int)(output.Width * size);
            int halfY = (int)(output.Height * size);

            //draw a poly in each corner - have to render each one separately
            output.FillPoly(new[] { new[] { new OCV.Point(0, 0), new OCV.Point(halfX, 0), new OCV.Point(0, halfY) } }, OCV.Scalar.White);
            output.FillPoly(new[] { new[] { new OCV.Point(output.Width, 0), new OCV.Point(output.Width, halfY), new OCV.Point(output.Width - halfX, 0) } }, OCV.Scalar.White);
            output.FillPoly(new[] { new[] { new OCV.Point(output.Width, output.Height), new OCV.Point(output.Width - halfX, output.Height), new OCV.Point(output.Width, output.Height - halfY) } }, OCV.Scalar.White);
            output.FillPoly(new[] { new[] { new OCV.Point(0, output.Height), new OCV.Point(0, output.Height - halfY), new OCV.Point(halfX, output.Height) } }, OCV.Scalar.White);

            return output;
        }
    }
}
