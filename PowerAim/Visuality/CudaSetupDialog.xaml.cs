using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.AILogic;

namespace PowerAim.Visuality;

/// <summary>
///     CUDA / TensorRT setup assistant (CUDA build only). The top shows what's actually running and
///     which pieces of the stack are present vs. missing (CUDA 12 runtime, cuDNN 9, TensorRT). One
///     button then sets TensorRT up: it uses the package if it's already next to the exe / in temp /
///     Downloads, otherwise downloads it to temp — with a real progress bar — and extracts the runtime.
/// </summary>
public partial class CudaSetupDialog : BaseDialog
{
    private static readonly Brush OkBrush = Frozen("#3FB950");      // green
    private static readonly Brush MissingBrush = Frozen("#E5534B"); // red

    private readonly Action? _reloadModel;

    public sealed class StatusItem
    {
        public string Glyph { get; init; } = "";
        public Brush GlyphBrush { get; init; } = Brushes.Gray;
        public string Title { get; init; } = "";
        public string Detail { get; init; } = "";
    }

    public ObservableCollection<StatusItem> StatusItems { get; } = new();

    public string IntroText => string.Format(Locale.CudaSetupIntro2, TensorRtInstaller.TensorPackageToUse);
    public string SetUpButtonText => Locale.CudaSetupSetUp;

    public string ActiveProviderText { get => field; set => SetField(ref field, value); } = "";
    public bool CanShowSetup { get => field; set => SetField(ref field, value); }
    public double ProgressValue { get => field; set => SetField(ref field, value); }
    public bool ProgressIndeterminate { get => field; set => SetField(ref field, value); }
    public string ProgressText { get => field; set => SetField(ref field, value); } = "";

    /// <summary>True while a setup run is in flight — drives the progress bar and disables the buttons.</summary>
    public bool IsBusy
    {
        get => field;
        set { if (SetField(ref field, value)) OnPropertyChanged(nameof(IsIdle)); }
    }

    public bool IsIdle => !IsBusy;

    /// <param name="reloadModel">Owner's LoadModel — invoked after setup so TensorRT is picked up and
    /// the active-provider line reflects reality, without this dialog touching MainWindow internals.</param>
    public CudaSetupDialog(Action? reloadModel = null)
    {
        InitializeComponent();
        DataContext = this;
        _reloadModel = reloadModel;
        Loaded += (_, _) => RefreshStatus();
    }

    private void RefreshStatus()
    {
        ActiveProviderText = CudaDiagnostics.ActiveProvider ?? Locale.CudaSetupNoModel;

        StatusItems.Clear();
        foreach (var dep in CudaDiagnostics.Collect())
        {
            StatusItems.Add(new StatusItem
            {
                Glyph = dep.Present ? "" : "",         // check / cancel
                GlyphBrush = dep.Present ? OkBrush : MissingBrush,
                Title = $"{dep.Component}  ({dep.Dll})",
                Detail = dep.Present
                    ? string.Format(Locale.CudaSetupFoundIn, dep.FoundIn)
                    : string.Format(Locale.CudaSetupNeeds, dep.Required),
            });
        }

        // Offer setup only when this build can target TensorRT and the runtime isn't there yet.
        CanShowSetup = TensorRtRuntime.SupportedInThisBuild() && !TensorRtRuntime.IsAvailable();
    }

    private async void SetUp_Click(object sender, RoutedEventArgs e)
    {
        IsBusy = true;
        ProgressIndeterminate = true;
        ProgressValue = 0;
        ProgressText = Locale.CudaSetupLocating;
        var progress = new Progress<TrtSetupProgress>(OnProgress);
        try
        {
            await TensorRtInstaller.SetUpAsync(progress);
            _reloadModel?.Invoke();
            new NoticeBar(Locale.TensorRtInstalled, 4000).Show();
        }
        catch (Exception ex)
        {
            new NoticeBar(string.Format(Locale.TensorRtInstallFailed, ex.Message), 7000).Show();
        }
        finally
        {
            IsBusy = false;
            RefreshStatus();
        }
    }

    private void OnProgress(TrtSetupProgress p)
    {
        ProgressValue = p.Fraction * 100;
        ProgressIndeterminate = p.Phase switch
        {
            TrtPhase.Downloading => p.TotalBytes <= 0, // determinate once we know the size
            TrtPhase.Done => false,                    // full bar at the end
            _ => true,                                 // Locating / Extracting spin
        };
        ProgressText = p.Phase switch
        {
            TrtPhase.Locating => Locale.CudaSetupLocating,
            TrtPhase.Downloading => p.TotalBytes > 0
                ? string.Format(Locale.CudaSetupDownloading, p.DoneBytes >> 20, p.TotalBytes >> 20)
                : string.Format(Locale.CudaSetupDownloadingUnknown, p.DoneBytes >> 20),
            TrtPhase.Extracting => Locale.CudaSetupExtracting,
            _ => Locale.CudaSetupDone,
        };
    }

    private void Recheck_Click(object sender, RoutedEventArgs e)
    {
        TensorRtRuntime.Invalidate();
        RefreshStatus();
    }

    private void OpenNvidia_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                TensorRtInstaller.VendorPage) { UseShellExecute = true });
        }
        catch { /* no browser available */ }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    private static Brush Frozen(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}
