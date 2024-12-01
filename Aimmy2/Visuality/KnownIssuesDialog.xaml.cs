using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Aimmy2;
using Aimmy2.Config;
using Aimmy2.Extensions;

namespace Visuality
{
    public partial class KnownIssuesDialog
    {
        private string _markdown;
        protected override bool SaveRestorePosition => false;

        public static void ShowIf(Window? owner = null, bool force = false)
        {
            string markdown = string.Empty;
            try
            {
                markdown = (App.Current as App).ReadEmbeddedResource("KnownIssues.md");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                markdown = string.Empty;
            }
            markdown = Regex.Replace(markdown, "<!--(.*?)-->", string.Empty, RegexOptions.Singleline);
            if (force || !string.IsNullOrWhiteSpace(markdown.Replace(Environment.NewLine, "")))
            {
                var issues = new KnownIssuesDialog(markdown);
                var settings = issues.GetWindowSettings();
                if (!force && settings?.ShouldShow == false)
                    return;
                if (owner != null)
                {
                    issues.Owner = owner;
                }
                issues.Show();
            }
        }

        public string Markdown
        {
            get => _markdown;
            set => SetField(ref _markdown, value);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Settings.ShouldShow ??= true;
        }

        public KnownIssuesDialog(string markdown)
        {
            InitializeComponent();
            _markdown = markdown;
            DataContext = this;
            MainBorder.BindMouseGradientAngle(ShouldBindGradientMouse);
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }


        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToggleButton_OnChecked(object sender, RoutedEventArgs e)
        {
            var isChecked = (sender as CheckBox)?.IsChecked ?? false;
            var settingsManager = new WindowSettingsManager(GetSettingsFilePath());
            settingsManager.SaveWindowSettings(this, isChecked);
        }
    }

}
