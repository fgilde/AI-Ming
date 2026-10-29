using System.Drawing;
using Microsoft.ML.OnnxRuntime.Tensors;
using PowerAim.AI;

namespace PowerAim.App.Tests;

public class ModelFormatTests
{
    private static readonly Dictionary<int, string> Names = new() { [0] = "a", [1] = "b", [2] = "c" };

    private static List<Prediction> Parse(DenseTensor<float> tensor, bool transposed, ModelOutputKind kind,
        int numClasses, int keypoints = 0, float minConf = 0.5f) =>
        PredictionFilter.CreatePredictions(tensor, new Rectangle(0, 0, 640, 640), 640, 0, numClasses, Names,
            minConf, null, 0, 640, 0, 640, outputTransposed: transposed, kind: kind, keypointCount: keypoints);

    [Fact]
    public void A_small_input_two_class_head_is_not_taken_for_end_to_end()
        // 160 px input → 525 anchors; channels-last two-class head [1, 525, 6].
        => Assert.False(PredictionLogic.IsEndToEnd([1, 525, 6], MathUtil.CalculateNumDetections(160)));

    [Theory]
    [InlineData(new[] { 1, 300, 6 }, true)]
    [InlineData(new[] { -1, 300, 6 }, true)]
    [InlineData(new[] { 1, 8400, 6 }, false)]
    [InlineData(new[] { 1, 6, 8400 }, false)]
    public void End_to_end_outputs_are_recognised(int[] dims, bool expected)
        => Assert.Equal(expected, PredictionLogic.IsEndToEnd(dims));

    [Fact]
    public void Objectness_head_is_recognised_from_the_label_count()
        => Assert.Equal(new OutputFormat(ModelOutputKind.YoloV5Raw, 80, 0, 0),
            PredictionLogic.ResolveOutputFormat(85, 80, null, null));

    [Fact]
    public void Plain_head_stays_plain()
        => Assert.Equal(new OutputFormat(ModelOutputKind.YoloV8Raw, 80, 0, 0),
            PredictionLogic.ResolveOutputFormat(84, 80, null, null));

    [Fact]
    public void Plain_head_without_labels_stays_plain()
        => Assert.Equal(new OutputFormat(ModelOutputKind.YoloV8Raw, 2, 0, 0),
            PredictionLogic.ResolveOutputFormat(6, 0, null, null));

    [Fact]
    public void Pose_head_is_recognised_from_kpt_shape()
        => Assert.Equal(new OutputFormat(ModelOutputKind.Pose, 1, 17, 3),
            PredictionLogic.ResolveOutputFormat(56, 1, "[17, 3]", null));

    [Fact]
    public void Segment_head_is_recognised_from_the_proto_output()
        => Assert.Equal(new OutputFormat(ModelOutputKind.Segment, 80, 0, 0),
            PredictionLogic.ResolveOutputFormat(116, 80, null, [1, 32, 160, 160]));

    [Fact]
    public void End_to_end_rows_are_read_as_corner_boxes_without_nms()
    {
        var t = new DenseTensor<float>([1, 300, 6]);
        float[][] rows = [[100, 100, 140, 180, 0.9f, 2], [102, 101, 141, 181, 0.8f, 2]];
        for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < 6; c++) t[0, r, c] = rows[r][c];

        var preds = Parse(t, transposed: true, ModelOutputKind.EndToEnd, 3);

        Assert.Equal(2, preds.Count); // already NMS'd by the model — overlapping boxes both stay
        var p = preds.First(x => x.Confidence == 0.9f);
        Assert.Equal(new RectangleF(100, 100, 40, 80), p.Rectangle);
        Assert.Equal(2, p.ClassId);
        Assert.Equal("c", p.ClassName);
    }

    [Fact]
    public void Objectness_scales_the_class_score()
    {
        var t = new DenseTensor<float>([1, 25200, 7]);
        float[] row = [320, 300, 40, 80, 0.8f, 0.1f, 0.9f];
        for (int c = 0; c < row.Length; c++) t[0, 3, c] = row[c];

        var p = Assert.Single(Parse(t, transposed: true, ModelOutputKind.YoloV5Raw, 2));

        Assert.Equal(1, p.ClassId);
        Assert.Equal(0.72f, p.Confidence, 3);
    }

    [Fact]
    public void Low_objectness_drops_the_box()
    {
        var t = new DenseTensor<float>([1, 25200, 7]);
        float[] row = [320, 300, 40, 80, 0.3f, 0.1f, 0.9f];
        for (int c = 0; c < row.Length; c++) t[0, 3, c] = row[c];

        Assert.Empty(Parse(t, transposed: true, ModelOutputKind.YoloV5Raw, 2));
    }

    [Fact]
    public void Pose_keypoints_are_read()
    {
        var t = new DenseTensor<float>([1, 56, 8400]);
        float[] head = [320, 300, 40, 80, 0.9f];
        for (int c = 0; c < head.Length; c++) t[0, c, 7] = head[c];
        // keypoint 0 (nose) and keypoint 6 (right shoulder)
        t[0, 5, 7] = 321; t[0, 6, 7] = 270; t[0, 7, 7] = 0.95f;
        t[0, 5 + 18, 7] = 330; t[0, 6 + 18, 7] = 285; t[0, 7 + 18, 7] = 0.4f;

        var p = Assert.Single(Parse(t, transposed: false, ModelOutputKind.Pose, 1, keypoints: 17));

        Assert.NotNull(p.Keypoints);
        Assert.Equal(17, p.Keypoints!.Length);
        Assert.Equal(new Keypoint(321, 270, 0.95f), p.Keypoints[0]);
        Assert.Equal(new Keypoint(330, 285, 0.4f), p.Keypoints[6]);
    }

    [Fact]
    public void Segment_mask_coefficients_are_not_read_as_classes()
    {
        var t = new DenseTensor<float>([1, 38, 8400]);
        float[] head = [320, 300, 40, 80, 0.1f, 0.7f];
        for (int c = 0; c < head.Length; c++) t[0, c, 7] = head[c];
        for (int c = 6; c < 38; c++) t[0, c, 7] = 5f; // large mask coefficients

        var p = Assert.Single(Parse(t, transposed: false, ModelOutputKind.Segment, 2));

        Assert.Equal(1, p.ClassId);
        Assert.Equal(0.7f, p.Confidence, 3);
    }
}
