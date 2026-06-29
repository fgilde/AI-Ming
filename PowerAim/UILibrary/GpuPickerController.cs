using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.AILogic;
using PowerAim.Config;
using GpuAdapter = PowerAim.AILogic.GpuAdapterEnumerator.GpuAdapter;

namespace PowerAim.UILibrary;

/// <summary>
///     Owns the title-bar "inference GPU" picker: the chip label, the adapter drop-down, and
///     persisting the chosen adapter to <see cref="AISettings.InferenceGpuDeviceId"/>. Lets the
///     user push ONNX inference onto a secondary GPU so detection doesn't compete with the game on
///     the primary card. The host just hands over the four chrome elements and a "reload model"
///     callback; all behaviour lives here.
/// </summary>
internal sealed class GpuPickerController
{
    private readonly Button _button;
    private readonly TextBlock _label;
    private readonly Popup _popup;
    private readonly Panel _list;
    private readonly Action _reloadModel;

    public GpuPickerController(Button button, TextBlock label, Popup popup, Panel list, Action reloadModel)
    {
        _button = button;
        _label = label;
        _popup = popup;
        _list = list;
        _reloadModel = reloadModel;
        _button.Click += (_, _) => OpenDropDown();
    }

    /// <summary>Show/hide the chip based on detected adapters and refresh its label. Idempotent.</summary>
    public void Initialize()
    {
        try
        {
            var adapters = GpuAdapterEnumerator.List();
            if (adapters.Count == 0)
            {
                _button.Visibility = Visibility.Collapsed;
                Debug.WriteLine("[GpuPicker] No adapters returned. Log: " + GpuAdapterEnumerator.LastLog);
                return;
            }
            // Show the chip whenever at least one real adapter exists — single-GPU users still
            // benefit from confirming which card is used; multi-GPU users can swap.
            _button.Visibility = Visibility.Visible;
            RefreshLabel(adapters);
        }
        catch (Exception ex)
        {
            _button.Visibility = Visibility.Collapsed;
            Debug.WriteLine($"[GpuPicker] Initialize threw: {ex.Message}");
        }
    }

    /// <summary>Update the chip text to the active config's selected adapter.</summary>
    public void RefreshLabel(IReadOnlyList<GpuAdapter>? adapters = null)
    {
        adapters ??= GpuAdapterEnumerator.List();
        int selected = AppConfig.Current?.AISettings?.InferenceGpuDeviceId ?? 0;
        var match = adapters.FirstOrDefault(a => a.DeviceId == selected);
        // The saved index may point at a GPU that no longer exists (driver removed / eGPU unplugged).
        if (match.Description is null && adapters.Count > 0) match = adapters[0];
        _label.Text = string.IsNullOrEmpty(match.Description) ? "GPU" : GpuName.Shorten(match.Description);
    }

    private void OpenDropDown()
    {
        var adapters = GpuAdapterEnumerator.List();
        int selectedId = AppConfig.Current?.AISettings?.InferenceGpuDeviceId ?? 0;

        _list.Children.Clear();

        // No real adapters → point at the diagnostic log instead of an empty-looking popup.
        if (adapters.Count == 0)
            _list.Children.Add(new TextBlock
            {
                Text = "Keine GPU erkannt. Log:\n" + Path.Combine(Path.GetTempPath(), "PowerAim_GpuEnum.log"),
                Padding = new Thickness(10, 8, 10, 8),
                Foreground = Brush("FluentTextSecondary"),
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            });

        foreach (var adapter in adapters)
            _list.Children.Add(BuildAdapterRow(adapter, adapter.DeviceId == selectedId));

        _list.Children.Add(BuildFooter());
        _popup.IsOpen = true;
    }

    private Button BuildAdapterRow(GpuAdapter adapter, bool isCurrent)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(new TextBlock
        {
            Text = isCurrent ? "\uE73E" : string.Empty, // Fluent CheckMark
            FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
            FontSize = 12,
            Width = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("FluentAccent"),
            Margin = new Thickness(0, 0, 8, 0),
        });
        content.Children.Add(new TextBlock
        {
            Text = adapter.DisplayLabel,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("FluentTextPrimary"),
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            FontSize = 13,
        });

        var row = new Button
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 1, 0, 1),
            Background = isCurrent ? Brush("FluentSurface3") : Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            Tag = adapter.DeviceId,
            Content = content,
        };
        row.Click += (_, _) => SelectAdapter(adapter.DeviceId);
        return row;
    }

    // Footer: Refresh (re-enumerate after a driver install / eGPU plug-in) + open the diagnostic log.
    private StackPanel BuildFooter()
    {
        var footer = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        footer.Children.Add(FooterButton("Refresh", new Thickness(0, 0, 4, 0), () =>
        {
            GpuAdapterEnumerator.Invalidate();
            _popup.IsOpen = false;
            OpenDropDown();
        }));
        footer.Children.Add(FooterButton("Open diagnostic log", new Thickness(0), OpenDiagnosticLog));
        return footer;
    }

    private Button FooterButton(string text, Thickness margin, Action onClick)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = margin,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Brush("FluentTextSecondary"),
            FontSize = 11,
            Cursor = Cursors.Hand,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private static void OpenDiagnosticLog()
    {
        try
        {
            var logPath = Path.Combine(Path.GetTempPath(), "PowerAim_GpuEnum.log");
            Process.Start(new ProcessStartInfo { FileName = logPath, UseShellExecute = true });
        }
        catch (Exception ex) { Debug.WriteLine($"[GpuPicker] open log failed: {ex.Message}"); }
    }

    private void SelectAdapter(int deviceId)
    {
        if (AppConfig.Current?.AISettings is null) return;
        if (AppConfig.Current.AISettings.InferenceGpuDeviceId == deviceId)
        {
            _popup.IsOpen = false;
            return;
        }
        AppConfig.Current.AISettings.InferenceGpuDeviceId = deviceId;
        AppConfig.Current.Save();
        _popup.IsOpen = false;
        RefreshLabel();
        // Reload the active model so the new device id takes effect immediately (not just next launch).
        try { _reloadModel(); }
        catch (Exception ex) { Debug.WriteLine($"[GpuPicker] reload model after GPU change failed: {ex.Message}"); }
    }

    private Brush Brush(string key) => (Brush)_button.FindResource(key);
}
