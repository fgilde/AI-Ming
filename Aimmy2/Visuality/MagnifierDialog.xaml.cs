using System.ComponentModel;
using Aimmy2.Config;
using System.Windows;
using System.Windows.Input;
using Aimmy2.Extensions;
using Aimmy2.Class;
using System.Windows.Interop;
using Aimmy2.AILogic.Contracts;
using Aimmy2.Class.Native;

namespace Visuality
{
    public partial class MagnifierDialog : IDisposable
    {
        private Magnifier? magnifier;
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
            if(e.PropertyName == nameof(SliderSettings.MagnificationFactor) && magnifier != null)
            {
                magnifier.Magnification = AppConfig.Current.SliderSettings.MagnificationFactor;
                magnifier.UpdateMagnifier();
            }
        }

        private void DlgLoaded(object sender, RoutedEventArgs e)
        {
            DoCenter();
            if(AIManager.Instance?.ImageCapture != null)
                AIManager.Instance.ImageCapture.PropertyChanged += ImageCaptureOnPropertyChanged;
            StartMagnification();
            this.HideForCapture();
        }

        private void DoCenter()
        {
            this.Center(AIManager.Instance.ImageCapture.CaptureArea);
            //if(AIManager.Instance?.ImageCapture != null && AIManager.Instance.IsRunning)
            //    this.Center(AIManager.Instance.ImageCapture.CaptureArea);
            //this.Center(AIManager.Instance?.ImageCapture?.Screen);
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
