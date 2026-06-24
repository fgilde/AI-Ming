using System;
using System.Windows;
using System.Windows.Input;
using PowerAim.Class.Native;
using PowerAim.Config;

namespace Visuality
{
    /// <summary>
    ///     A small floating "tab" that shows the current config's label (<see cref="AppConfig.EffectiveConfigLabel"/>)
    ///     just above the main window's top edge. It is a separate, owner-tracked window so it renders
    ///     OUTSIDE the main window — the main window's own content/layout is untouched. Click the name
    ///     to rename the config inline.
    /// </summary>
    public partial class ConfigLabelOverlay : Window
    {
        private const double OverlapPx = 1;   // 1px tuck so the tab sits flush on the window's top edge (no gap)
        private readonly Window _owner;

        public ConfigLabelOverlay(Window owner)
        {
            _owner = owner;
            InitializeComponent();
            Owner = owner;
            DataContext = owner;                 // binds {Binding Config.EffectiveConfigLabel}
            SizeChanged += (_, _) => Reposition();
            Loaded += (_, _) =>
            {
                this.HideForCaptureIfEnabled();
                Reposition();
            };
        }

        /// <summary>Center the tab horizontally on the owner and sit it just above the owner's top edge.</summary>
        public void Reposition()
        {
            if (_owner == null) return;
            if (_owner.WindowState == WindowState.Minimized)
            {
                Visibility = Visibility.Collapsed;
                return;
            }
            Visibility = Visibility.Visible;

            UpdateLayout();
            double w = ActualWidth > 0 ? ActualWidth : DesiredSize.Width;
            double h = ActualHeight > 0 ? ActualHeight : DesiredSize.Height;

            double ownerLeft, ownerTop, ownerWidth;
            if (_owner.WindowState == WindowState.Maximized)
            {
                var wa = SystemParameters.WorkArea;
                ownerLeft = wa.Left;
                ownerTop = wa.Top;
                ownerWidth = wa.Width;
            }
            else
            {
                ownerLeft = _owner.Left;
                ownerTop = _owner.Top;
                ownerWidth = _owner.ActualWidth;
            }

            Left = ownerLeft + (ownerWidth - w) / 2.0;
            // Tuck the tab's bottom edge slightly into the window's top so it reads as part of it.
            double desiredTop = ownerTop - h + OverlapPx;
            // Never push the tab fully off the top of the virtual desktop.
            Top = Math.Max(desiredTop, SystemParameters.VirtualScreenTop);
        }

        private void ConfigLabel_BeginEdit(object sender, MouseButtonEventArgs e)
        {
            var cfg = AppConfig.Current;
            if (cfg == null) return;
            ConfigLabelEdit.Text = cfg.ConfigLabel ?? string.Empty;
            ConfigLabelText.Visibility = Visibility.Collapsed;
            ConfigLabelEdit.Visibility = Visibility.Visible;
            Activate();
            ConfigLabelEdit.Focus();
            ConfigLabelEdit.SelectAll();
        }

        private void ConfigLabelEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
            else if (e.Key == Key.Escape) { EndEdit(); e.Handled = true; }
        }

        private void ConfigLabelEdit_Commit(object sender, RoutedEventArgs e) => Commit();

        private void Commit()
        {
            if (ConfigLabelEdit.Visibility != Visibility.Visible) return;
            var cfg = AppConfig.Current;
            if (cfg != null)
            {
                var text = (ConfigLabelEdit.Text ?? string.Empty).Trim();
                // Typing the auto file-name back in means "no custom label" — keep the auto fallback.
                var fileName = string.IsNullOrEmpty(cfg.Path)
                    ? string.Empty
                    : System.IO.Path.GetFileNameWithoutExtension(cfg.Path);
                if (string.Equals(text, fileName, StringComparison.Ordinal)) text = string.Empty;
                if (cfg.ConfigLabel != text)
                {
                    cfg.ConfigLabel = text;
                    if (!string.IsNullOrEmpty(cfg.Path))
                    {
                        try { cfg.Save(); } catch { /* label is cosmetic; never fail on save */ }
                    }
                }
            }
            EndEdit();
            _owner?.Activate();
        }

        private void EndEdit()
        {
            ConfigLabelEdit.Visibility = Visibility.Collapsed;
            ConfigLabelText.Visibility = Visibility.Visible;
        }

        // The chevron opens the cool quick-config switcher in its own window, anchored under the tab.
        private QuickConfigWindow _quickConfig;

        private void ConfigChevron_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            try
            {
                _quickConfig = new QuickConfigWindow(_owner ?? this);
                _quickConfig.Closed += (_, _) => _quickConfig = null;
                _quickConfig.ShowBelow(this);
            }
            catch
            {
                _quickConfig = null;
            }
        }
    }
}
