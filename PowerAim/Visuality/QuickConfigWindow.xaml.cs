using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Core;
using PowerAim.Class.Native;
using PowerAim.Config;
using PowerAim.UILibrary;

namespace PowerAim.Visuality
{
    /// <summary>
    ///     A small, cool config switcher shown in its own window under the floating config tab.
    ///     Lists the local configs in <c>bin\configs</c>; the active one is highlighted, clicking
    ///     another switches to it. A "Save current as…" row opens the save dialog.
    /// </summary>
    public partial class QuickConfigWindow : Window
    {
        private const string ConfigDir = Constants.ConfigBasePath;
        private readonly Window _owner;
        // Per-row key changers — disposed on close so their global keybind subscription doesn't leak.
        private readonly List<AKeyChanger> _keyChangers = new();

        public QuickConfigWindow(Window owner)
        {
            _owner = owner;
            InitializeComponent();
            Owner = owner;
            BuildList();
            Loaded += (_, _) => this.HideForCaptureIfEnabled();
        }

        private void BuildList()
        {
            ConfigList.Children.Clear();

            string activeFull = "";
            try
            {
                if (!string.IsNullOrEmpty(AppConfig.Current?.Path))
                    activeFull = Path.GetFullPath(AppConfig.Current.Path);
            }
            catch { /* ignore */ }

            string[] files;
            try { files = Directory.Exists(ConfigDir) ? Directory.GetFiles(ConfigDir, "*.cfg") : Array.Empty<string>(); }
            catch { files = Array.Empty<string>(); }

            foreach (var file in files.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            {
                bool active = activeFull.Length > 0 &&
                              string.Equals(SafeFullPath(file), activeFull, StringComparison.OrdinalIgnoreCase);
                ConfigList.Children.Add(BuildRow(file, active));
            }

            if (ConfigList.Children.Count == 0)
            {
                ConfigList.Children.Add(new TextBlock
                {
                    Text = "—",
                    Margin = new Thickness(10, 6, 10, 6),
                    FontSize = 13,
                    Foreground = Brush("FluentTextTertiary")
                });
            }
        }

        private Border BuildRow(string path, bool active)
        {
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 7,
                Height = 7,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Fill = active ? Brush("FluentAccent") : Brush("FluentTextTertiary")
            };

            var text = new TextBlock
            {
                Text = Path.GetFileNameWithoutExtension(path),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Segoe UI Variable Text"),
                FontSize = 13,
                Foreground = active ? Brush("FluentAccent") : Brush("FluentTextPrimary"),
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal
            };

            var nameStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(dot);
            nameStack.Children.Add(text);

            // Clickable name area = switch to this config. Kept separate from the key changer so
            // clicking the keybind doesn't also switch the config.
            var nameHost = new Border
            {
                Background = Brushes.Transparent,
                Child = nameStack,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = active ? Cursors.Arrow : Cursors.Hand,
            };
            if (!active)
                nameHost.MouseLeftButtonUp += (_, _) => SwitchTo(path);

            // Per-config keybind — reuses the SAME AKeyChanger control + storage as the Models &
            // Configs page (prefix "CONFIG" + the file name "X.cfg"), so setting it in either place
            // updates the one shared binding. Pressing it loads this config.
            var fileName = Path.GetFileName(path);
            var keyChanger = new AKeyChanger
            {
                KeyConfigName = fileName,
                KeyConfigPrefix = "CONFIG",
                Tag = fileName,
                BindingManager = PowerAim.MainWindow.Instance?.BindingManager,
                WithBorder = false,
                Text = "",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                IgnoreGlobalActiveGate = true,
            };
            keyChanger.GlobalKeyPressed += (_, _) => SwitchTo(path);
            _keyChangers.Add(keyChanger);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(nameHost, 0);
            Grid.SetColumn(keyChanger, 1);
            grid.Children.Add(nameHost);
            grid.Children.Add(keyChanger);

            return new Border { Style = (Style)FindResource("QcRow"), Child = grid };
        }

        private bool _closed;   // guards against re-entrant Close() from Deactivated during closing

        private void SwitchTo(string path)
        {
            _closed = true;
            Close();
            // Defer the (heavy, UI-rebuilding) config load until after this popup has fully closed.
            var mw = PowerAim.MainWindow.Instance;
            mw?.Dispatcher.BeginInvoke((Action)(() =>
            {
                try { mw.LoadConfig(path); } catch { /* surfaced by LoadConfig */ }
            }));
        }

        private void SaveCurrent_Click(object sender, MouseButtonEventArgs e)
        {
            _closed = true;
            var owner = _owner;
            Close();
            // Open the save dialog after this popup has closed, to avoid modal/focus re-entrancy.
            owner?.Dispatcher.BeginInvoke((Action)(() =>
            {
                try { new ConfigSaver { Owner = owner }.ShowDialog(); } catch { /* ignore */ }
            }));
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (_closed) return;
            // Don't dismiss while the user is mid-recording a keybind — capturing a mouse-button
            // binding moves focus off this window, which would otherwise close it before the bind lands.
            if (_keyChangers.Any(k => k.InUpdateMode)) return;
            _closed = true;
            try { Close(); } catch { /* ignore */ }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Dispose each AKeyChanger so its BindingManager.OnBindingPressed subscription is removed
            // (otherwise every open would leak a handler that keeps firing after the window is gone).
            foreach (var kc in _keyChangers)
            {
                try { kc.Dispose(); } catch { /* ignore */ }
            }
            _keyChangers.Clear();
            base.OnClosed(e);
        }

        /// <summary>Show the window centered just below the given anchor window (the floating tab).</summary>
        public void ShowBelow(Window anchor)
        {
            Opacity = 0;          // avoid a flash at the off-screen start position
            Show();
            UpdateLayout();
            Left = anchor.Left + (anchor.ActualWidth - ActualWidth) / 2.0;
            Top = anchor.Top + anchor.ActualHeight - 12;   // tuck just under the tab
            Opacity = 1;
            Activate();
        }

        private static string SafeFullPath(string p)
        {
            try { return Path.GetFullPath(p); }
            catch { return p; }
        }

        private Brush Brush(string key) => (Brush)FindResource(key);
    }
}
