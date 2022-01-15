using System.Collections.Generic;
using Cv2 = OpenCvSharp.Cv2;
using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    internal class ClassifierPSNR
    {
        internal int Run(string[] args, Lazy<Codex> codex)
        {
            string inputImage = args[1];

            var image = Cv2.ImRead(inputImage);
            ScreenMetadata dim = new(image.Width);

            for (int row = 0; row < ScreenMetadata.BoonRowsMax; row++)
            {
                for (int column = 0; column < ScreenMetadata.BoonColumnsMax; column++)
                {
                    if(dim.GetTraitRect(column, row, out OCV.Rect? traitRect))
                    {
                        //grab the image
                        OCV.Mat traitImg = image.SubMat(traitRect!.Value);

                        //first, filter by slot
                        var possibleTraits = dim.FindPossibleTraits(codex.Value, column, row);
                    }
                }
            }

            return 0;
        }
    }
}