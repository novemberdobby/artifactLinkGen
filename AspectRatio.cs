using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    internal class AspectRatio
    {
        internal enum Ratio
        {
            Unknown,
            _16_9,
            _21_9,
        }

        internal static readonly Dictionary<Ratio, double> Values = new()
        {
            { Ratio._16_9, 16.0 / 9.0 },
            { Ratio._21_9, 64.0 / 27.0 }, //this was VERY confusing until it became clear that "21:9" is only a marketing term
        };

        internal static Ratio Measure(OCV.Size size)
        {
            double ratio = size.Width / (double)size.Height;
            foreach (var aspectType in Values)
            {
                //people might crop them a little so we need an epsilon
                if (Math.Abs(ratio - aspectType.Value) < 0.04)
                {
                    return aspectType.Key;
                }
            }

            return Ratio.Unknown;
        }
    }
}
