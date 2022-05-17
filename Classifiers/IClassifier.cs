using OCV = OpenCvSharp;

namespace HadesBoonBot
{
    internal interface IClassifier : IDisposable
    {
        public Classifiers.ClassifiedScreen? Classify(OCV.Mat screen, string filePath, int columnCount, int pinRows, bool debugOutput);
    }
}