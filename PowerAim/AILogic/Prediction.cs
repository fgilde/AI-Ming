using System.Drawing;
using PowerAim.Types;

namespace PowerAim.AILogic;

/// <summary>
///     A single detection produced by the AI pipeline. Centers are stored in screen-normalized
///     coordinates (0..1 over the detection box) so downstream consumers (sticky aim, mouse mover,
///     UI overlay) can scale to the active resolution without re-reading the inference tensor.
/// </summary>
public class Prediction
{
    public float Confidence { get; set; }
    public float CenterXTranslated { get; set; }
    public float CenterYTranslated { get; set; }

    /// <summary>
    ///     Index of the winning class within the loaded ONNX model. Defaults to <c>0</c> so legacy
    ///     single-class models and consumers that ignore class info keep working unchanged.
    /// </summary>
    public int ClassId { get; set; } = 0;

    /// <summary>
    ///     Human-readable name resolved from the ONNX <c>names</c> metadata. Defaults to
    ///     <c>"Enemy"</c> to match the prior implicit assumption.
    /// </summary>
    public string ClassName { get; set; } = "Enemy";

    //public RelativeRect HeadRelativeRect { get; set; } = RelativeRect.Default;
    public RectangleF Rectangle { get; set; }
    public RectangleF TranslatedRectangle { get; set; }


    //public bool IsIntersectingHeadRelativeArea() => IsIntersectingCenter(HeadRelativeRect);

    public bool IsIntersectingCenter(RelativeRect relativeRect)
    {
        var rect = Rectangle;
        float centerX = PredictionLogic.CurrentImageSize / 2.0f;
        float centerY = PredictionLogic.CurrentImageSize / 2.0f;

        // Calculate the size and position of the relative rectangle
        float relativeWidth = rect.Width * relativeRect.WidthPercentage;
        float relativeHeight = rect.Height * relativeRect.HeightPercentage;
        float leftMargin = rect.Width * relativeRect.LeftMarginPercentage;
        float topMargin = rect.Height * relativeRect.TopMarginPercentage;

        float relativeX = rect.X + leftMargin;
        float relativeY = rect.Y + topMargin;

        RectangleF relativeRectF = new RectangleF(relativeX, relativeY, relativeWidth, relativeHeight);

        return relativeRectF.Left <= centerX && relativeRectF.Right >= centerX &&
               relativeRectF.Top <= centerY && relativeRectF.Bottom >= centerY;
    }

    public bool IsIntersectingCenter()
    {
        var rect = Rectangle;
        float centerX = PredictionLogic.CurrentImageSize / 2.0f;
        float centerY = PredictionLogic.CurrentImageSize / 2.0f;

        return rect.Left <= centerX && rect.Right >= centerX &&
               rect.Top <= centerY && rect.Bottom >= centerY;
    }

}
