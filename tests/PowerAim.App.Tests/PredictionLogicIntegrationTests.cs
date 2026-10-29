using System.Drawing;
using System.IO;
using System.Reflection;
using PowerAim.AI;
using PowerAim.Config;

namespace PowerAim.App.Tests;

/// <summary>The shipped default model loads, is recognised as a plain detection head and runs end to end on the CPU.</summary>
// Shares the global AppConfig with tests that replace it — never run side by side.
[Collection(nameof(TestConfig))]
public class PredictionLogicIntegrationTests
{
    private static string DefaultModel()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "PowerAim.App", "Resources", "default.onnx");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("default.onnx not found above the test output.");
    }

    [Fact]
    public void Default_model_runs_through_the_whole_parse_path()
    {
        TestConfig.Ensure();
        var ai = AppConfig.Current!.AISettings;
        var (provider, precision) = (ai.PreferredExecutionProvider, ai.Precision);
        ai.PreferredExecutionProvider = ExecutionProviderPreference.Cpu;
        ai.Precision = ModelPrecision.Fp32;
        try
        {
            using var logic = new PredictionLogic(DefaultModel());
            Assert.True(logic.IsLoaded, logic.LoadError);

            var format = (OutputFormat)typeof(PredictionLogic).GetField("_outputFormat", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(logic)!;
            Assert.Equal(ModelOutputKind.YoloV8Raw, format.Kind);
            Assert.Equal(logic.NumClasses, format.NumClasses);

            int size = logic.ImageSize;
            using var frame = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Assert.Empty(logic.Predict(frame, new Rectangle(0, 0, size, size)).Result);

            // A FOV larger than the model input goes through the resize path.
            using var big = new Bitmap(size + 160, size + 160, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            Assert.Empty(logic.Predict(big, new Rectangle(0, 0, big.Width, big.Height)).Result);
        }
        finally
        {
            ai.PreferredExecutionProvider = provider;
            ai.Precision = precision;
        }
    }
}
