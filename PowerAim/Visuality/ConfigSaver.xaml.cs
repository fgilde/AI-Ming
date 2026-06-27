using PowerAim.Class;
using Class;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Core;
using PowerAim;
using PowerAim.Config;
using PowerAim.Extensions;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using MessageBox = System.Windows.MessageBox;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for ConfigSaver.xaml
    /// </summary>
    public partial class ConfigSaver 
    {
        private static Color DisableColor = (Color)ColorConverter.ConvertFromString("#FFFFFFFF");
        private static TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(500);

        // Always center on the owner (MainWindow) instead of restoring a free-floating position.
        protected override bool SaveRestorePosition => false;

        public void SetColorAnimation(Color fromColor, Color toColor, TimeSpan duration)
        {
            ColorAnimation animation = new(fromColor, toColor, duration);
            SwitchMoving.Background.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        private string ExtraStrings = string.Empty;

        public ConfigSaver()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void WriteJSON()
        {
            var path = $"{Constants.ConfigBasePath}\\{ConfigNameTextbox.Text}.cfg";
            AppConfig.Current.Save(path);
            new NoticeBar("Config has been saved to bin/configs.", 4000).Show();
            Close();
            MainWindow.Instance?.LoadConfig(path);
        }

        private void DownloadableModelChecker_Click(object sender, RoutedEventArgs e)
        {
            if (ExtraStrings == string.Empty)
            {
                ExtraStrings = " (Found in Downloadable Model menu)";
                SetColorAnimation((Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty), ApplicationConstants.AccentColor, AnimationDuration);
                Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new(0, 0, -1, 0));
            }
            else
            {
                ExtraStrings = "";
                SetColorAnimation((Color)SwitchMoving.Background.GetValue(SolidColorBrush.ColorProperty), DisableColor, AnimationDuration);
                Animator.ObjectShift(AnimationDuration, SwitchMoving, SwitchMoving.Margin, new(0, 0, 16, 0));
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists($"bin/configs/{ConfigNameTextbox.Text}.cfg") ||
                PowerAim.Visuality.MessageDialog.Show("A config already exists with the same name, would you like to overwrite it?",
                    $"{Title} - Configuration Saver", PowerAim.Visuality.MessageDialog.DialogButtons.YesNo, PowerAim.Visuality.MessageDialog.DialogIcon.Question, owner: this) == PowerAim.Visuality.MessageDialog.DialogResult.Yes)
            {
                WriteJSON();
            }
        }

        #region Window Controls

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        
        #endregion Window Controls
    }
}