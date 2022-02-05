using Microsoft.ML.Data;

namespace HadesBoonBot.ML
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    //reusable classes for a model that's trained solely on "bad" and "good" images

    public class ModelInput
    {
        [ColumnName(@"Label")]
        public string Label { get; set; }

        [ColumnName(@"ImageSource")]
        public string ImageSource { get; set; }

        public ModelInput(string imageSource)
        {
            ImageSource = imageSource;
        }
    }

    public class ModelOutput
    {
        [ColumnName("PredictedLabel")]
        public string Prediction { get; set; }

        public float[] Score { get; set; }
    }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    internal class Util
    {
        internal static bool IsGood(ModelOutput result)
        {
            return result.Prediction == "good" && result.Score.Max() > 0.9f;
        }
    }
}
