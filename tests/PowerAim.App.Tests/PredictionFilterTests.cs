using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class PredictionFilterTests
{
    private static List<Prediction> Parse(DenseTensor<float> tensor, bool transposed) =>
        PredictionFilter.CreatePredictions(tensor, new Rectangle(0, 0, 640, 640), 640, 8400, 1,
            new Dictionary<int, string> { [0] = "Enemy" }, 0.5f, null, 0, 640, 0, 640,
            outputTransposed: transposed);

    [Fact]
    public void Output_without_a_score_channel_yields_nothing_instead_of_throwing()
        => Assert.Empty(Parse(new DenseTensor<float>([1, 4, 8400]), transposed: false));

    [Fact]
    public void Channels_last_box_is_read()
    {
        var tensor = new DenseTensor<float>([1, 756, 5]);
        float[] box = [320, 300, 40, 80, 0.9f];
        for (int c = 0; c < box.Length; c++) tensor[0, 10, c] = box[c];

        var p = Assert.Single(Parse(tensor, transposed: true));

        Assert.Equal(new RectangleF(300, 260, 40, 80), p.Rectangle);
        Assert.Equal(0.9f, p.Confidence);
    }
}
