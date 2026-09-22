using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PowerAim.AILogic;
using PowerAim.Config;

namespace PowerAim.UILibrary;

/// <summary>
///     Status + one-click setup for the local Jev decision server. Mirrors
///     <see cref="OllamaStatusIndicator"/>: a dot, a line of state, and exactly the button that makes
///     sense right now — install Python, set up, start, or stop. Everything heavy runs in
///     <see cref="JevServerSetup"/>; this control only drives it and shows what happened.
/// </summary>
public partial class JevStatusIndicator : UserControl
{
    private static readonly Brush Ok = Frozen(0x55, 0xFF, 0x55);
    private static readonly Brush Busy = Frozen(0xFF, 0xAA, 0x00);
    private static readonly Brush Bad = Frozen(0xFF, 0x55, 0x55);

    private readonly JevDecisionClient _client = new();
    private readonly System.Windows.Threading.DispatcherTimer _poll;
    private bool _working;

    public JevStatusIndicator()
    {
        InitializeComponent();
        _poll = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _poll.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) => { _poll.Start(); await RefreshAsync(); };
        Unloaded += (_, _) => _poll.Stop();
    }

    private static Brush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    /// <summary>Probe the server and pick the one action that makes sense from here.</summary>
    private async Task RefreshAsync()
    {
        if (_working) return; // a setup/start run owns the UI until it finishes

        var settings = AppConfig.Current?.JevSettings;
        bool hosted = settings?.BaseUrl?.Contains("typesafe.ai", StringComparison.OrdinalIgnoreCase) == true;
        bool reachable = await _client.IsAvailableAsync();

        if (reachable)
        {
            Set(Ok, Locale.JevConnected, $"{settings?.BaseUrl} · {settings?.Model}", null);
            Show(setup: false, start: false, stop: JevServerSetup.IsServerRunning && !hosted, python: false);
            return;
        }

        // Hosted API: nothing to install locally — the user only needs a key / working URL.
        if (hosted)
        {
            Set(Bad, Locale.JevDisconnected, settings?.BaseUrl, _client.LastError);
            Show(setup: false, start: false, stop: false, python: false);
            return;
        }

        if (!JevServerSetup.IsInstalled)
        {
            bool hasPython = JevServerSetup.FindPython() != null;
            Set(Bad, Locale.JevNotInstalled,
                hasPython ? Locale.JevReadyToSetUp : string.Format(Locale.JevPythonMissing, Core.Jev.PythonVersion.Minimum),
                null);
            Show(setup: hasPython, start: false, stop: false, python: !hasPython);
            return;
        }

        Set(Bad, Locale.JevServerStopped, JevServerSetup.InstallDir, _client.LastError);
        Show(setup: false, start: true, stop: false, python: false);
    }

    private void Set(Brush dot, string status, string? detail, string? error)
    {
        StatusIndicator.Fill = dot;
        StatusText.Text = status;
        DetailText.Text = detail ?? "";
        DetailText.Visibility = string.IsNullOrWhiteSpace(detail) ? Visibility.Collapsed : Visibility.Visible;
        ErrorText.Text = error ?? "";
        ErrorText.Visibility = string.IsNullOrWhiteSpace(error) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Show(bool setup, bool start, bool stop, bool python)
    {
        SetupButton.Visibility = setup ? Visibility.Visible : Visibility.Collapsed;
        StartButton.Visibility = start ? Visibility.Visible : Visibility.Collapsed;
        StopButton.Visibility = stop ? Visibility.Visible : Visibility.Collapsed;
        InstallPythonButton.Visibility = python ? Visibility.Visible : Visibility.Collapsed;
        ActionRow.Visibility = (setup || start || stop || python) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Runs a long operation with the buttons disabled and the progress bar live.</summary>
    private async Task RunAsync(Func<IProgress<JevSetupProgress>, Task> op)
    {
        _working = true;
        Show(setup: false, start: false, stop: false, python: false);
        SetupProgress.Visibility = Visibility.Visible;
        SetupProgress.IsIndeterminate = true;
        StatusIndicator.Fill = Busy;

        var progress = new Progress<JevSetupProgress>(p =>
        {
            StatusText.Text = PhaseText(p.Phase);
            if (p.Fraction >= 0) { SetupProgress.IsIndeterminate = false; SetupProgress.Value = p.Fraction; }
            else SetupProgress.IsIndeterminate = true;
            if (!string.IsNullOrWhiteSpace(p.Detail)) DetailText.Text = Trim(p.Detail);
            DetailText.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;
        });

        try
        {
            await op(progress);
        }
        catch (Exception ex)
        {
            Set(Bad, Locale.JevSetupFailed, null, ex.Message);
        }
        finally
        {
            SetupProgress.Visibility = Visibility.Collapsed;
            _working = false;
            await RefreshAsync();
        }
    }

    // pip and the model loader emit very long lines; keep the last, most informative chunk.
    private static string Trim(string s) => s.Length <= 120 ? s : "…" + s[^120..];

    private static string PhaseText(JevSetupPhase phase) => phase switch
    {
        JevSetupPhase.CheckingPython => Locale.JevPhaseCheckingPython,
        JevSetupPhase.Downloading => Locale.JevPhaseDownloading,
        JevSetupPhase.Extracting => Locale.JevPhaseExtracting,
        JevSetupPhase.CreatingEnvironment => Locale.JevPhaseCreatingEnvironment,
        JevSetupPhase.InstallingTorch => Locale.JevPhaseInstallingTorch,
        JevSetupPhase.InstallingServer => Locale.JevPhaseInstallingServer,
        JevSetupPhase.Starting => Locale.JevPhaseStarting,
        _ => Locale.JevPhaseDone,
    };

    private async void Setup_Click(object sender, RoutedEventArgs e)
    {
        bool useCuda = (AppConfig.Current?.JevSettings?.Device ?? "cuda")
            .Equals("cuda", StringComparison.OrdinalIgnoreCase);
        await RunAsync(async p =>
        {
            await JevServerSetup.SetUpAsync(useCuda, p);
            await JevServerSetup.StartServerAsync(p);
        });
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
        => await RunAsync(p => JevServerSetup.StartServerAsync(p));

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        JevServerSetup.StopServer();
        await RefreshAsync();
    }

    private void InstallPython_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = JevServerSetup.PythonDownloadUrl,
                UseShellExecute = true,
            });
        }
        catch { /* no default browser */ }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
}
