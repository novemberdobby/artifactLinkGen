using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    internal interface IClassifier : IDisposable
    {
        public ClassifiedScreen? Classify(OCV.Mat image, string filePath, bool debugOutput);
    }
}