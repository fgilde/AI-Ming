using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim.AILogic.Actions;
using PowerAim.Config;

namespace PowerAim.UILibrary;

/// <summary>
///     Shows WHICH layer is steering AutoPlay right now and how sure it is: a coloured source badge
///     (Heuristic / Ollama / Jev), the chosen mode, the per-mode probability bars Jev returns, the danger
///     reading, and the decision latency. A strategic failure is shown as a warning line so a silent
///     fallback to the heuristic can't be mistaken for a deliberate decision.
/// </summary>
public partial class AutoPlayDecisionPanel : UserControl
{
    /// <summary>One probability bar. Pre-formatted so the DataTemplate stays converter-free.</summary>
    public sealed class ModeRow
    {
        public string Mode { get; init; } = "";
        public double Probability { get; init; }
        public string Percent => $"{Probability * 100:0}%";
        public bool IsChosen { get; init; }
        public Brush LabelBrush => IsChosen ? Accent : Dim;
        public Brush BarBrush => IsChosen ? Accent : Dim;
    }

    private static readonly Brush Accent = Frozen(0x4C, 0x9A, 0xFF);
    private static readonly Brush Dim = Frozen(0x7A, 0x7A, 0x7A);
    private static readonly Brush HeuristicBadge = Frozen(0x6E, 0x6E, 0x6E);
    private static readonly Brush OllamaBadge = Frozen(0x8A, 0x5C, 0xD6);
    private static readonly Brush JevBadge = Frozen(0x2E, 0x9E, 0x6B);

    public ObservableCollection<ModeRow> Modes { get; } = new();

    private readonly System.Windows.Threading.DispatcherTimer _timer;
    private DateTime _lastShown = DateTime.MinValue;

    public AutoPlayDecisionPanel()
    {
        InitializeComponent();
        ProbabilityList.ItemsSource = Modes;

        // 300 ms is fast enough to feel live for a decision that lands every 0.3–5 s, and cheap: we only
        // touch the visual tree when the snapshot actually changed.
        _timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _timer.Tick += (_, _) => Update();
        Loaded += (_, _) => { _timer.Start(); Update(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void Update()
    {
        var d = AutoPlayGameAction.CurrentDecision;
        if (d == null)
        {
            SourceText.Text = "—";
            SourceBadge.Background = HeuristicBadge;
            ModeText.Text = AppConfig.Current?.ToggleState?.AutoPlay == true ? Locale.AutoPlayDecisionWaiting : Locale.AutoPlayDecisionOff;
            DetailText.Text = "";
            LatencyText.Text = "";
            ErrorText.Visibility = Visibility.Collapsed;
            Modes.Clear();
            return;
        }

        if (d.At == _lastShown && Modes.Count > 0) return; // nothing new since the last tick
        _lastShown = d.At;

        SourceText.Text = d.Source;
        SourceBadge.Background = d.Source switch
        {
            "Jev" => JevBadge,
            "Ollama" => OllamaBadge,
            _ => HeuristicBadge,
        };

        ModeText.Text = string.IsNullOrEmpty(d.Direction) ? d.Mode : $"{d.Mode} · {d.Direction}";

        var parts = new List<string>();
        if (d.Confidence is { } c) parts.Add(string.Format(Locale.AutoPlayDecisionConfidence, c * 100));
        if (d.Danger is { } danger) parts.Add(string.Format(Locale.AutoPlayDecisionDanger, danger * 100));
        if (!string.IsNullOrEmpty(d.Action)) parts.Add(string.Format(Locale.AutoPlayDecisionAbility, d.Action));
        DetailText.Text = string.Join("   ", parts);
        DetailText.Visibility = parts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        LatencyText.Text = d.LatencyMs is { } ms ? $"{ms:0} ms" : "";

        ErrorText.Text = d.Error ?? "";
        ErrorText.Visibility = string.IsNullOrWhiteSpace(d.Error) ? Visibility.Collapsed : Visibility.Visible;

        Modes.Clear();
        if (d.ModeProbabilities != null)
            foreach (var kv in d.ModeProbabilities.OrderByDescending(k => k.Value))
                Modes.Add(new ModeRow { Mode = kv.Key, Probability = kv.Value, IsChosen = kv.Key == d.Mode });
    }
}
