using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PowerAim.Visuality;

/// <summary>
///     Shown while a TensorRT engine is being built (the slow, silent first-time compile inside
///     <c>new InferenceSession</c>). ONNX Runtime exposes no build progress, so the bar is indeterminate
///     and we show live elapsed time instead. The native build can't be aborted mid-call, so "cancel"
///     means <see cref="BuildOutcome.SwitchToCuda"/> — the caller starts a fresh CUDA load (no engine
///     build) and discards the still-running TensorRT one.
/// </summary>
public partial class TensorRtBuildDialog : BaseDialog
{
    public enum BuildOutcome { None, Background, SwitchToCuda }

    /// <summary>What the user chose. <see cref="BuildOutcome.None"/> means the dialog was closed
    /// programmatically because the build finished on its own.</summary>
    public BuildOutcome Outcome { get; private set; } = BuildOutcome.None;

    public string ModelName { get; }

    public string ElapsedText
    {
        get => field;
        private set => SetField(ref field, value);
    } = "0:00";

    private readonly DispatcherTimer _timer;
    private readonly DateTime _start = DateTime.Now;

    public TensorRtBuildDialog(string modelName)
    {
        InitializeComponent();
        ModelName = modelName ?? "";
        DataContext = this;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var elapsed = DateTime.Now - _start;
            ElapsedText = $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2}";
        };
        _timer.Start();
    }

    private void Background_Click(object sender, RoutedEventArgs e)
    {
        Outcome = BuildOutcome.Background;
        Close();
    }

    private void SwitchCuda_Click(object sender, RoutedEventArgs e)
    {
        Outcome = BuildOutcome.SwitchToCuda;
        Close();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
