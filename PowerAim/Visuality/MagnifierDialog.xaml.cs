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
            if(e.PropertyName == nameof(SliderSettings.MagnificationFactor) && magnifier is not null)
            {
                magnifier.Magnification = AppConfig.Current.SliderSettings.MagnificationFactor;
                magnifier.UpdateMagnifier();
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
            magnifier = new Magnifier(this)
            {
                Magnification = AppConfig.Current.SliderSettings.MagnificationFactor
            };
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
