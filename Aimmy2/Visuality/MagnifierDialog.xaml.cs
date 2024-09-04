using System.ComponentModel;
using Aimmy2.Config;
using System.Windows;
using System.Windows.Input;
using Aimmy2.Extensions;
using Aimmy2.Class;
using System.Windows.Interop;
using Aimmy2.AILogic.Contracts;

namespace Visuality
{
    public partial class MagnifierDialog : IDisposable
    {
        private Magnifier? magnifier;
        protected override bool SaveRestorePosition => false;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            ClickThroughOverlay.MakeClickThrough(new WindowInteropHelper(this).Handle);
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
            if(e.PropertyName == nameof(SliderSettings.MagnificationFactor) && magnifier != null)
            {
                magnifier.Magnification = AppConfig.Current.SliderSettings.MagnificationFactor;
                magnifier.UpdateMagnifier();
            }
        }

        private void DlgLoaded(object sender, RoutedEventArgs e)
        {
            Center();
            if(AIManager.Instance?.ImageCapture != null)
                AIManager.Instance.ImageCapture.PropertyChanged += ImageCaptureOnPropertyChanged;
            StartMagnification();
        }

        private void Center()
        {
            this.Center(AIManager.Instance.ImageCapture.CaptureArea);
        }


        private void ImageCaptureOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ICapture.CaptureArea))
            {
                Center();
            }
        }

        private void StartMagnification()
        {
            magnifier = new Magnifier(this);
            magnifier.Magnification = AppConfig.Current.SliderSettings.MagnificationFactor;
            magnifier.UpdateMagnifier();
        }


        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        
        public void Dispose()
        {
            AppConfig.Current.SliderSettings.PropertyChanged -= OnConfigChange;
            magnifier.Dispose();
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
