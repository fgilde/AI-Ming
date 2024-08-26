using Aimmy2.Config;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace Visuality
{
    public partial class MagnifierDialog : IDisposable
    {
        private Magnifier magnifier;
        protected override bool SaveRestorePosition => false;

        const uint WDA_NONE = 0x00000000;
        const uint WDA_MONITOR = 0x00000001;

        [DllImport("user32.dll")]
        static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        public MagnifierDialog()
        {
            Loaded += DlgLoaded;
            Closed += (sender, args) => Dispose();
            InitializeComponent();
            //var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            //SetWindowDisplayAffinity(hwnd, WDA_MONITOR);
        }

        private void DlgLoaded(object sender, RoutedEventArgs e)
        {
            StartMagnification();
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


        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public void Dispose()
        {
            magnifier.Dispose();
        }
    }

}
