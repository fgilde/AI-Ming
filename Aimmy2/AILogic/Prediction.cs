using System.Drawing;
using Aimmy2.Types;

namespace Aimmy2.AILogic;

public class Prediction
{
    public float Confidence { get; set; }
    public float CenterXTranslated { get; set; }
    public float CenterYTranslated { get; set; }
    //public RelativeRect HeadRelativeRect { get; set; } = RelativeRect.Default;
    public RectangleF Rectangle { get; set; }
    public RectangleF TranslatedRectangle { get; set; }


    //public bool IsIntersectingHeadRelativeArea() => IsIntersectingCenter(HeadRelativeRect);

    public bool IsIntersectingCenter(RelativeRect relativeRect)
    {
        var rect = Rectangle;
        float centerX = PredictionLogic.IMAGE_SIZE / 2.0f;
        float centerY = PredictionLogic.IMAGE_SIZE / 2.0f;

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
        float centerX = PredictionLogic.IMAGE_SIZE / 2.0f;
        float centerY = PredictionLogic.IMAGE_SIZE / 2.0f;

        return rect.Left <= centerX && rect.Right >= centerX &&
               rect.Top <= centerY && rect.Bottom >= centerY;
    }

}