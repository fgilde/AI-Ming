using System.ComponentModel;
using PowerAim.Config;
using System.Windows;
using System.Windows.Input;
using PowerAim.Extensions;
using PowerAim.Class;
using System.Windows.Interop;
using PowerAim.AILogic.Contracts;
using PowerAim.Class.Native;

namespace PowerAim.Visuality
{
    public partial class MagnifierDialog : IDisposable
    {
        private Magnifier? magnifier;
        private EnhancedMagnifier? enhanced;
        protected override bool SaveRestorePosition => false;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.MakeClickThrough();
        }

        public MagnifierDialog()
        {
            Loaded += DlgLoaded;
            Closed += (_, _) => Dispose();
            DataContext = AppConfig.Current.SliderSettings;
            AppConfig.Current.SliderSettings.PropertyChanged += OnConfigChange;
            InitializeComponent();
        }

        private void OnConfigChange(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SliderSettings.MagnificationFactor))
            {
                var f = AppConfig.Current.SliderSettings.MagnificationFactor;
                if (magnifier is not null) { magnifier.Magnification = f; magnifier.UpdateMagnifier(); }
                if (enhanced is not null) enhanced.Magnification = f;
            }
            else if (e.PropertyName == nameof(SliderSettings.MagnifierScaling))
            {
                // Switching scaling mode swaps the whole renderer (native API vs. custom bicubic).
                StopRenderers();
                StartMagnification();
            }
        }

        private void DlgLoaded(object sender, RoutedEventArgs e)
        {
            DoCenter();
            if(AIManager.Instance?.ImageCapture is not null)
                AIManager.Instance.ImageCapture.PropertyChanged += ImageCaptureOnPropertyChanged;
            StartMagnification();
            this.HideForCaptureIfEnabled();
        }

        private void DoCenter()
        {
            this.Center(AIManager.Instance.ImageCapture.CaptureArea);
        }


        private void ImageCaptureOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ICapture.CaptureArea))
            {
                DoCenter();
            }
        }

        private void StartMagnification()
        {
            var f = AppConfig.Current.SliderSettings.MagnificationFactor;
            if (AppConfig.Current.SliderSettings.MagnifierScaling == MagnifierScalingMode.Enhanced)
            {
                // The custom renderer screen-grabs the source region, so the magnifier window must be
                // excluded from capture (regardless of the global hide-from-capture setting), or it
                // would capture itself and feed back.
                this.HideForCapture();
                EnhancedImage.Visibility = Visibility.Visible;
                enhanced = new EnhancedMagnifier(this, EnhancedImage) { Magnification = f };
            }
            else
            {
                EnhancedImage.Visibility = Visibility.Collapsed;
                magnifier = new Magnifier(this) { Magnification = f };
                magnifier.UpdateMagnifier();
            }
        }

        private void StopRenderers()
        {
            magnifier?.Dispose();
            magnifier = null;
            enhanced?.Dispose();
            enhanced = null;
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        
        public void Dispose()
        {
            AppConfig.Current.SliderSettings.PropertyChanged -= OnConfigChange;
            StopRenderers();
        }

        private void MagnifierDialog_OnGotFocus(object sender, RoutedEventArgs e)
        {
        }

        private void MagnifierDialog_OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Center(AIManager.Instance.ImageCapture.CaptureArea);
        }
    }

}
