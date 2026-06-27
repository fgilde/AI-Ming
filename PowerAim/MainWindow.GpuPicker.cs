using Core;
using InputLogic;
using Microsoft.Xaml.Behaviors.Core;
using MouseMovementLibraries.ddxoftSupport;
using MouseMovementLibraries.RazerSupport;
using Nextended.Core;
using Nextended.Core.Extensions;
using Nextended.Core.Helper;
using Nextended.UI.Helper;
using Other;
using PowerAim.Class;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.InputLogic.HidHide;
using PowerAim.Localizations;
using PowerAim.Models;
using PowerAim.MouseMovementLibraries.GHubSupport;
using PowerAim.Other;
using PowerAim.Types;
using PowerAim.UILibrary;
using PowerAim.Visuality;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using UILibrary;
using Visuality;
using Application = System.Windows.Application;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Panel = System.Windows.Controls.Panel;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using TextBox = System.Windows.Controls.TextBox;

namespace PowerAim;

public partial class MainWindow
{
    // ============================================================== INFERENCE GPU PICKER ====
    //
    // Lets the user push ONNX inference onto a secondary GPU so detection workloads don't
    // compete with the game for cycles on the primary card. Only renders when DXGI reports
    // more than one usable adapter — otherwise the chip stays hidden.

    private void InitGpuPicker()
    {
        if (GpuPickerButton is null) return;
        try
        {
            var adapters = AILogic.GpuAdapterEnumerator.List();
            // Show the chip whenever we have at least one real adapter — even on a single-GPU
            // system the user benefits from seeing which card is in use, AND if enumeration only
            // returned one entry on a known multi-GPU rig the popup surfaces the diagnostic log.
            if (adapters.Count == 0)
            {
                GpuPickerButton.Visibility = Visibility.Collapsed;
                Debug.WriteLine("[GpuPicker] No adapters returned. Log: " + AILogic.GpuAdapterEnumerator.LastLog);
                return;
            }
            // Show the pill whenever at least one real adapter is detected. Single-GPU users still
            // benefit from confirming which card is being used; multi-GPU users can swap.
            GpuPickerButton.Visibility = Visibility.Visible;
            RefreshGpuPickerLabel(adapters);
        }
        catch (Exception ex)
        {
            GpuPickerButton.Visibility = Visibility.Collapsed;
            Debug.WriteLine($"[GpuPicker] InitGpuPicker threw: {ex.Message}");
        }
    }

    private void RefreshGpuPickerLabel(IReadOnlyList<AILogic.GpuAdapterEnumerator.GpuAdapter>? adapters = null)
    {
        if (GpuPickerLabel is null) return;
        adapters ??= AILogic.GpuAdapterEnumerator.List();
        int selected = AppConfig.Current?.AISettings?.InferenceGpuDeviceId ?? 0;
        var match = adapters.FirstOrDefault(a => a.DeviceId == selected);
        // Fall back to the description of whatever's currently first if the saved index points at
        // a GPU that no longer exists (driver removed, eGPU unplugged).
        if (match.Description is null && adapters.Count > 0) match = adapters[0];
        GpuPickerLabel.Text = string.IsNullOrEmpty(match.Description)
            ? "GPU"
            : ShortenGpuName(match.Description);
    }

    /// <summary>
    ///     Strip the vendor prefix from typical adapter names so the chip in the titlebar stays
    ///     compact: "NVIDIA GeForce RTX 4090" → "RTX 4090". Falls back to the original when no
    ///     known prefix matches.
    /// </summary>
    private static string ShortenGpuName(string full)
    {
        string[] prefixes = ["NVIDIA GeForce ", "NVIDIA ", "AMD Radeon(TM) ", "AMD Radeon ", "AMD ", "Intel(R) ", "Intel "];
        foreach (var p in prefixes)
            if (full.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                return full.Substring(p.Length).Trim();
        return full;
    }

    private void GpuPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (GpuPickerPopup is null || GpuPickerList is null) return;
        var adapters = AILogic.GpuAdapterEnumerator.List();
        int selectedId = AppConfig.Current?.AISettings?.InferenceGpuDeviceId ?? 0;

        GpuPickerList.Children.Clear();
        // No real adapters → tell the user where the diagnostic lives instead of leaving an empty
        // popup that looks broken.
        if (adapters.Count == 0)
        {
            var emptyMsg = new TextBlock
            {
                Text = "Keine GPU erkannt. Log:\n" + System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerAim_GpuEnum.log"),
                Padding = new Thickness(10, 8, 10, 8),
                Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
            };
            GpuPickerList.Children.Add(emptyMsg);
        }
        foreach (var adapter in adapters)
        {
            bool isCurrent = adapter.DeviceId == selectedId;
            var rowBtn = new Button
            {
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 1, 0, 1),
                Background = isCurrent
                    ? (System.Windows.Media.Brush)FindResource("FluentSurface3")
                    : Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = adapter.DeviceId,
            };
            var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            stack.Children.Add(new TextBlock
            {
                Text = isCurrent ? "" : string.Empty, // U+E73E = Fluent CheckMark glyph
                FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                FontSize = 12,
                Width = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("FluentAccent"),
                Margin = new Thickness(0, 0, 8, 0),
            });
            stack.Children.Add(new TextBlock
            {
                Text = adapter.DisplayLabel,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("FluentTextPrimary"),
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                FontSize = 13,
            });
            rowBtn.Content = stack;
            rowBtn.Click += GpuPickerRow_Click;
            GpuPickerList.Children.Add(rowBtn);
        }

        // Footer: refresh + open diagnostic log. Refresh re-enumerates (useful after a driver
        // install or eGPU plug-in); open-log surfaces enumeration details when the list looks wrong.
        var footer = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var refreshBtn = new Button
        {
            Content = "Refresh",
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 4, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontSize = 11,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        refreshBtn.Click += (_, _) =>
        {
            AILogic.GpuAdapterEnumerator.Invalidate();
            GpuPickerPopup.IsOpen = false;
            GpuPickerButton_Click(this, new RoutedEventArgs());
        };
        var openLogBtn = new Button
        {
            Content = "Open diagnostic log",
            Padding = new Thickness(8, 4, 8, 4),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = (System.Windows.Media.Brush)FindResource("FluentTextSecondary"),
            FontSize = 11,
            Cursor = System.Windows.Input.Cursors.Hand,
        };
        openLogBtn.Click += (_, _) =>
        {
            try
            {
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PowerAim_GpuEnum.log");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[GpuPicker] open log failed: {ex.Message}"); }
        };
        footer.Children.Add(refreshBtn);
        footer.Children.Add(openLogBtn);
        GpuPickerList.Children.Add(footer);

        GpuPickerPopup.IsOpen = true;
    }

    private void GpuPickerRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: int newDeviceId }) return;
        if (AppConfig.Current?.AISettings is null) return;
        if (AppConfig.Current.AISettings.InferenceGpuDeviceId == newDeviceId)
        {
            GpuPickerPopup.IsOpen = false;
            return;
        }
        AppConfig.Current.AISettings.InferenceGpuDeviceId = newDeviceId;
        AppConfig.Current.Save();
        GpuPickerPopup.IsOpen = false;
        RefreshGpuPickerLabel();
        // Reload the currently active model so the new device-id actually takes effect — without
        // this the change only kicks in on the next manual reload / app restart.
        try { LoadModel(); }
        catch (Exception ex) { Debug.WriteLine($"[GpuPicker] LoadModel after GPU change failed: {ex.Message}"); }
    }
}
