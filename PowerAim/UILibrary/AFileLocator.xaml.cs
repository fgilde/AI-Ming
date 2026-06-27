using PowerAim.Class;
using PowerAim.Config;
using Microsoft.Win32;
using System.IO;
using PowerAim.Types;
using UserControl = System.Windows.Controls.UserControl;

namespace PowerAim.UILibrary
{
    /// <summary>
    /// Interaction logic for AFileLocator.xaml
    /// </summary>
    public partial class AFileLocator : UserControl
    {
        private readonly OpenFileDialog openFileDialog = new();
        private string main_dictionary_path { get; set; }
        private string OFDFilter = "All files (*.*)|*.*";
        private string DefaultLocationExtension = "";

        public event EventHandler<EventArgs<string>> FileSelected; 

        public AFileLocator(string title, string dictionary_path, string FileFilter = "All files (*.*)|*.*", string DLExtension = "")
        {
            InitializeComponent();
            DropdownTitle.Content = title;

            main_dictionary_path = dictionary_path;
            FileLocationTextbox.Text = AppConfig.Current.FileLocationState[main_dictionary_path]?.ToString();


            OFDFilter = FileFilter;
            DefaultLocationExtension = DLExtension;
        }

        private void OpenFileB_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.openfiledialog?view=windowsdesktop-8.0
            // Nori

            openFileDialog.InitialDirectory = DefaultLocationExtension.Contains(":") ? Path.GetDirectoryName(DefaultLocationExtension) : Directory.GetCurrentDirectory() + DefaultLocationExtension;
            openFileDialog.Filter = OFDFilter;

            if (openFileDialog.ShowDialog() == true)
            {
                FileLocationTextbox.Text = openFileDialog.FileName;
                AppConfig.Current.FileLocationState[main_dictionary_path] = openFileDialog.FileName;
                FileSelected?.Invoke(this, new(openFileDialog.FileName));
            }
        }
    }
}