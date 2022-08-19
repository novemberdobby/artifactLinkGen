using OCV = OpenCvSharp;
using Cv2 = OpenCvSharp.Cv2;

namespace HadesBoonBot
{
    internal class CVUtil
    {
        /// <summary>
        /// Overlay for pinned traits
        /// </summary>
        public static OCV.Mat OverlayPin;

        /// <summary>
        /// Overlay for traits that are currently being hovered over
        /// </summary>
        public static OCV.Mat OverlayHover;

        static CVUtil()
        {
            OverlayPin = Cv2.ImRead(@"icons_overlay\pin.png", OCV.ImreadModes.Unchanged);
            OverlayHover = Cv2.ImRead(@"icons_overlay\hover.png", OCV.ImreadModes.Unchanged);
        }

        /// <summary>
        /// Mask out the 4 corners of an image to create a diamond (creates a copy)
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

        public enum BlendMode
        {
            Blend,
            Additive,
        }

        /// <summary>
        /// Blend two images together
        /// </summary>
        /// <param name="background">Bottom layer</param>
        /// <param name="foreground">Top layer</param>
        /// <param name="mode">Blend mode</param>
        /// <param name="removeAlpha">Convert resulting image to BGR</param>
        /// <returns>New image</returns>
        public static OCV.Mat Blend(OCV.Mat background, OCV.Mat foreground, BlendMode mode, bool removeAlpha = false)
        {
            //resize if necessary, but never modify the original
            if (background.Size() != foreground.Size())
            {
                background = background.Resize(foreground.Size(), 0, 0, OCV.InterpolationFlags.Cubic);
            }
            else
            {
                background = background.Clone();
            }

            static byte lerp(byte a, byte b, byte amount) { return (byte)(a + (b - a) * amount / 255.0f); }
            
            foreground.GetArray(out OCV.Vec4b[] foreData);
            background.GetArray(out OCV.Vec4b[] backData);

            OCV.Vec4b tmp = new();
            for (int i = 0; i < backData.Length; i++)
            {
                var back = backData[i];
                var fore = foreData[i];

                //are these calculations correct? probably not, but they match the expected result
                if (mode == BlendMode.Blend)
                {
                    tmp.Item0 = lerp(back.Item0, fore.Item0, fore.Item3);
                    tmp.Item1 = lerp(back.Item1, fore.Item1, fore.Item3);
                    tmp.Item2 = lerp(back.Item2, fore.Item2, fore.Item3);
                    tmp.Item3 = (byte)(Math.Clamp(back.Item3 + fore.Item3, 0, 255));
                }
                else
                {
                    tmp.Item0 = lerp(back.Item0, (byte)(Math.Clamp(back.Item0 + fore.Item0, 0, 255)), fore.Item3);
                    tmp.Item1 = lerp(back.Item1, (byte)(Math.Clamp(back.Item1 + fore.Item1, 0, 255)), fore.Item3);
                    tmp.Item2 = lerp(back.Item2, (byte)(Math.Clamp(back.Item2 + fore.Item2, 0, 255)), fore.Item3);
                    tmp.Item3 = (byte)(Math.Clamp(back.Item3 + fore.Item3, 0, 255));
                }

                backData[i] = tmp;
            }

            //update image
            background.SetArray(backData);

            if(removeAlpha)
            {
                var noAlpha = background.CvtColor(OCV.ColorConversionCodes.BGRA2BGR);
                background.Dispose();
                return noAlpha;
            }

            return background;
        }
    }

    internal static class CVUtilExtensions
    {
        /// <summary>
        /// Draw a string to an image with newline support
        /// </summary>
        /// <param name="yPadding">Extra padding pixels between lines</param>
        public static void PutTextMultiline(this OCV.Mat image, string text, OCV.Point origin, OCV.HersheyFonts fontFace, double fontScale, OCV.Scalar colour, int thickness = 1, OCV.LineTypes lineType = OCV.LineTypes.Link8, bool bottomLeftOrigin = false, int yPadding = 3)
        {
            string[] lines = System.Text.RegularExpressions.Regex.Split(text, @"\r?\n");

            int yOffset = 0;
            foreach (string line in lines)
            {
                var size = Cv2.GetTextSize(line, fontFace, fontScale, thickness, out _);
                image.PutText(line, origin + new OCV.Point(0, yOffset), fontFace, fontScale, colour, thickness, lineType, bottomLeftOrigin);

                if (lines.Length > 1)
                {
                    yOffset += size.Height + yPadding;
                }
            }
        }
    }
}
