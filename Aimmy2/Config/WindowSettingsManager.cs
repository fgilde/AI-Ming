using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Forms;
using Point = System.Drawing.Point;

namespace Aimmy2.Config;

public class WindowSettingsManager
{
    private readonly string _settingsFilePath;

    public WindowSettingsManager(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
    }

    public void SaveWindowSettings(Window window, bool shouldShow = true)
    {
        try
        {
            var screen = Screen.FromPoint(new Point((int)window.Left, (int)window.Top));
            var settings = new WindowSettings
            {
                ShouldShow = shouldShow,
                Width = window.Width,
                Height = window.Height,
                Top = window.Top,
                Left = window.Left,
                WindowState = window.WindowState,
                ScreenDeviceName = screen.DeviceName
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            
        }
    }

    public WindowSettings? LoadWindowSettings()
    {
        try
        {
            if (!File.Exists(_settingsFilePath)) return null;

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<WindowSettings>(json);
        }
        catch (Exception e)
        {
            return null;
        }
    }

    public void LoadWindowSettings(Window window)
    {
        try
        {
            var settings = LoadWindowSettings();

            if (settings != null)
            {
                var targetScreen = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == settings.ScreenDeviceName);

                if (targetScreen != null)
                {
                    var screenBounds = targetScreen.WorkingArea;
                    if (IsWithinBounds(screenBounds, settings))
                    {
                        window.WindowStartupLocation = WindowStartupLocation.Manual;
                        window.Left = settings.Left;
                        window.Top = settings.Top;
                        window.Width = settings.Width;
                        window.Height = settings.Height;
                        window.WindowState = settings.WindowState;
                        return;
                    }
                }

                // If screen is not available or position is out of bounds, center the window
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }
        catch (Exception e)
        {}
    }

    private bool IsWithinBounds(System.Drawing.Rectangle bounds, WindowSettings settings)
    {
        return settings.Left >= bounds.Left && settings.Top >= bounds.Top &&
               settings.Left + settings.Width <= bounds.Right &&
               settings.Top + settings.Height <= bounds.Bottom;
    }
}

public class WindowSettings
{
    public bool? ShouldShow { get; set; } = true;
    public double Width { get; set; }
    public double Height { get; set; }
    public double Top { get; set; }
    public double Left { get; set; }
    public WindowState WindowState { get; set; }
    public string ScreenDeviceName { get; set; }
}