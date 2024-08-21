using Class;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Aimmy2.Class;
using Aimmy2.Config;
using System.IO;
using Accord.Math;
using Aimmy2;

namespace Visuality;

public abstract class BaseDialog : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual bool SaveRestorePosition => true;
    protected Func<bool> ShouldBindGradientMouse = () => AppConfig.Current.ToggleState.MouseBackgroundEffect;
    private WindowSettings _settings;

    public WindowSettings Settings
    {
        get => _settings;
        protected set => SetField(ref _settings, value);
    }

    internal virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
        var settingsManager = new WindowSettingsManager(GetSettingsFilePath());
        Settings = settingsManager.LoadWindowSettings() ?? new WindowSettings();
        if (SaveRestorePosition)
        {
            settingsManager.LoadWindowSettings(this);
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