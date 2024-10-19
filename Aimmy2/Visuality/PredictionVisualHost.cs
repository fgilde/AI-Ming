using Aimmy2.AILogic;
using System.Windows.Media;
using System.Windows;
using Aimmy2.Other;
using Aimmy2.Config;
using Aimmy2.Types;
using System.Windows.Forms;

namespace Visuality;

public class PredictionVisualHost : FrameworkElement
{
    private readonly VisualCollection _children;

    public PredictionVisualHost()
    {
        _children = new VisualCollection(this);
    }

    public void DrawPredictions(IEnumerable<Prediction> predictions, Rect? targetArea = null)
    {
        if(!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => DrawPredictions(predictions, targetArea));
            return;
        }
        _children.Clear();

        foreach (var prediction in predictions)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                DrawPrediction(dc, prediction, targetArea);
            }
            _children.Add(visual);
        }
    }

    private void DrawPrediction(DrawingContext dc, Prediction prediction, Rect? targetArea)
    {
       PredictionDrawer.DrawPrediction(dc, prediction, targetArea);
    }

    protected override int VisualChildrenCount => _children.Count;

    protected override Visual GetVisualChild(int index)
    {
        return _children[index];
    }
}
