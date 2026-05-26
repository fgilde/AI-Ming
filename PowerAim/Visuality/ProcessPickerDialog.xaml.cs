using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Windows.Controls;
using PowerAim.Extensions;

namespace Visuality
{
    public partial class ProcessPickerDialog
    {
        public Process? SelectedProcess { get; private set; }

        public ProcessPickerDialog()
        {
            InitializeComponent();
            DataContext = this;
            LoadProcesses();
        }
        private void LoadProcesses()
        {
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                .OrderBy(p => p.MainWindowTitle)
                .ToList();
            ProcessListBox.ItemsSource = processes;
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void ProcessListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ProcessListBox.SelectedItem is Process selectedProcess)
            {
                SelectedProcess = selectedProcess;
                DialogResult = true;
                Close();
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void ProcessListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedProcess = e.AddedItems.Count > 0 ? e.AddedItems[0] as Process : null;
            ApplyButton.IsEnabled = SelectedProcess is not null;

            // Highlight + foreground the picked process so the user can see exactly which window
            // they're about to wire into PowerAim. HideOverlay fires from OnClosed.
            if (SelectedProcess is not null)
            {
                try
                {
                    var hwnd = SelectedProcess.MainWindowHandle;
                    if (hwnd != IntPtr.Zero)
                        PowerAim.Visuality.CaptureHighlightOverlay.ShowFor(hwnd, bringToFront: true);
                }
                catch { /* the process may have died between enumeration and click */ }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            PowerAim.Visuality.CaptureHighlightOverlay.HideOverlay();
            base.OnClosed(e);
        }
    }

}
