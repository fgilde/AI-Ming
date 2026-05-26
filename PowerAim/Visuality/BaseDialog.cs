using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Shell;
using PowerAim.Class.Native;
using PowerAim.Config;

namespace PowerAim.Visuality;

public abstract class BaseDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public IDictionary<string, string> Texts => Locale.GetAll();
    protected virtual bool SaveRestorePosition => true;

    public WindowSettings Settings
    {
        get;
        protected set => SetField(ref field, value);
    }

    internal virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded += OnLoaded;
        EnsureWindowChrome();
        var settingsManager = new WindowSettingsManager(GetSettingsFilePath());
        Settings = settingsManager.LoadWindowSettings() ?? new WindowSettings();
        if (SaveRestorePosition)
        {
            settingsManager.LoadWindowSettings(this);
        }
    }

    private void EnsureWindowChrome()
    {
        if (WindowStyle == WindowStyle.None && WindowChrome.GetWindowChrome(this) is null && !AllowsTransparency)
        {
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = ResizeMode == ResizeMode.NoResize ? new Thickness(0) : new Thickness(6),
                GlassFrameThickness = new(-1),
                UseAeroCaptionButtons = false,
                CornerRadius = new(0)
            });
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        this.HideForCaptureIfEnabled();
        ApplyFluentBackdrop();
    }

    protected virtual void ApplyFluentBackdrop()
    {
        try
        {
            Wpf.Ui.Controls.WindowBackdrop.ApplyBackdrop(this, Wpf.Ui.Controls.WindowBackdropType.Mica);
        }
        catch
        {
            // older OS may not support Mica - fall back silently
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (SaveRestorePosition)
        {
            var settingsManager = new WindowSettingsManager(GetSettingsFilePath());
            settingsManager.SaveWindowSettings(this);
        }
    }

    protected WindowSettings? GetWindowSettings()
    {
        var settingsManager = new WindowSettingsManager(GetSettingsFilePath());
        return settingsManager.LoadWindowSettings();
    }

    protected string GetSettingsFilePath()
    {
        var dialogType = GetType().Name;
        var folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AI-M");
        Directory.CreateDirectory(folderPath);
        return Path.Combine(folderPath, $"{dialogType}_WindowSettings.bin");
    }
}