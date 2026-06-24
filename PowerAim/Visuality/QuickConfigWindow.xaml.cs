using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PowerAim.Class.Native;
using PowerAim.Config;

namespace Visuality
{
    /// <summary>
    ///     A small, cool config switcher shown in its own window under the floating config tab.
    ///     Lists the local configs in <c>bin\configs</c>; the active one is highlighted, clicking
    ///     another switches to it. A "Save current as…" row opens the save dialog.
    /// </summary>
    public partial class QuickConfigWindow : Window
    {
        private const string ConfigDir = "bin\\configs";
        private readonly Window _owner;

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

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(dot);
            sp.Children.Add(text);

            var border = new Border { Style = (Style)FindResource("QcRow"), Child = sp };
            if (!active)
                border.MouseLeftButtonUp += (_, _) => SwitchTo(path);
            return border;
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
            _closed = true;
            try { Close(); } catch { /* ignore */ }
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
