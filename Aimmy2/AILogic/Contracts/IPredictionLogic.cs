using System.Drawing;

namespace Aimmy2.AILogic.Contracts;

public interface IPredictionLogic
{
    Task<Prediction[]> Predict(Bitmap frame, Rectangle detectionBox);
    OnnxExecutionProvider ExecutionProvider { get; }
}