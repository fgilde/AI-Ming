using Aimmy2;
using Aimmy2.Class;
using AimmyWPF.Class;
using InputLogic;
using System.Windows;
using System.Windows.Threading;
using Aimmy2.Class.Native;
using Aimmy2.Config;
using Aimmy2.Extensions;
using Nextended.Core.Extensions;

namespace Visuality
{
    /// <summary>
    /// Interaction logic for SetAntiRecoil.xaml
    /// </summary>
    public partial class SetAntiRecoil : Window
    {
        private MainWindow MainWin { get; set; }
        private DispatcherTimer HoldDownTimer = new DispatcherTimer();
        private DateTime LastClickTime;
        private int FireRate;
        private int ChangingFireRate;
        public IDictionary<string, string> Texts => Locale.GetAll();

        public SetAntiRecoil(MainWindow MW)
        {
            InitializeComponent();
            DataContext = this;

            MW.WindowState = WindowState.Minimized;

            MainWin = MW;

            BulletBorder.Opacity = 0;
            BulletBorder.Margin = new Thickness(0, 0, 0, -140);

            HoldDownTimer.Tick += HoldDownTimerTicker;
            HoldDownTimer.Interval = TimeSpan.FromMilliseconds(1);
            HoldDownTimer.Start();
            Loaded += OnLoaded;
            ChangingFireRate = AppConfig.Current.AntiRecoilSettings.FireRate;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            this.HideForCapture();
        }

        private void HoldDownTimerTicker(object? sender, EventArgs e)
        {
            if (InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind)))
            {
                GetReading();
                HoldDownTimer.Stop();
            }
        }

        private async void GetReading()
        {
            LastClickTime = DateTime.Now;
            while (InputBindingManager.IsHoldingBinding(nameof(AppConfig.Current.BindingSettings.AntiRecoilKeybind)))
            {
                await Task.Delay(1);
            }
            FireRate = (int)(DateTime.Now - LastClickTime).TotalMilliseconds;

            Animator.Fade(BulletBorder);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(350), BulletBorder, BulletBorder.Margin, new Thickness(0, 0, 0, 100));

            UpdateFireRate();
        }

        private void BulletNumberTextbox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (BulletBorder.Opacity == 1 && BulletBorder.Margin == new Thickness(0, 0, 0, 100))
            {
                UpdateFireRate();
            }
        }

        private void UpdateFireRate()
        {
            if (BulletNumberTextbox.Text != null && BulletNumberTextbox.Text.Any(char.IsDigit))
            {
                ChangingFireRate = (int)(FireRate / Convert.ToInt64(BulletNumberTextbox.Text));
            }
            else
            {
                ChangingFireRate = FireRate;
            }

            SettingLabel.Content = Locale.FireRateSet.FormatWith(ChangingFireRate);
        }

        private void ConfirmB_Click(object sender, RoutedEventArgs e)
        {
            AppConfig.Current.AntiRecoilSettings.FireRate = ChangingFireRate;
            new NoticeBar(Locale.FireRateSetSuccessfully, 5000).Show();

            ExitAndClose();
        }

        private void TryAgainB_Click(object sender, RoutedEventArgs e)
        {
            SettingLabel.Content = Locale.AntiRecoilHelp;

            Animator.FadeOut(BulletBorder);
            Animator.ObjectShift(TimeSpan.FromMilliseconds(350), BulletBorder, BulletBorder.Margin, new Thickness(0, 0, 0, -140));

            HoldDownTimer.Start();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            ExitAndClose();
        }

        private void ExitAndClose()
        {
            MainWin.WindowState = WindowState.Normal;
            Close();
        }
    }
}