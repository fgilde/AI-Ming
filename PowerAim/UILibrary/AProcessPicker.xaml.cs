using System.ComponentModel;
using PowerAim.Models;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using PowerAim.Visuality;

namespace PowerAim.UILibrary
{
    public partial class AProcessPicker : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        public AProcessPicker()
        {
            InitializeComponent();
            DataContext = this;
        }


        public ProcessModel SelectedProcessModel
        {
            get;
            set
            {
                if (Equals(value, field)) return;
                field = value;
                OnPropertyChanged();
            }
        }

        private void ProcessPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var processDialog = new ProcessPickerDialog();
            if (processDialog.ShowDialog() == true)
            {
                var selectedProcess = processDialog.SelectedProcess;
                if (selectedProcess != null)
                {
                    SelectedProcessModel = new() { Process = selectedProcess };

                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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
    }
}