using System.Windows;
using System.Windows.Controls;

namespace PowerAim.UILibrary
{
    /// <summary>
    ///     Empty-state card shown on the Aim page while no model is loaded. While a model load is in
    ///     flight (<see cref="IsLoading"/>) it shows a loading spinner so the "no model" message
    ///     doesn't flash at startup; once a load resolves with no model it explains the two
    ///     prerequisites (a detection model + a valid capture source) and offers a one-click
    ///     "load the bundled default model" action (see <see cref="MainWindow.LoadDefaultModelAsync"/>).
    /// </summary>
    public partial class NoModelCard : UserControl
    {
        public NoModelCard()
        {
            InitializeComponent();
            UpdatePanels();
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            UpdatePanels();
        }

        /// <summary>When true, shows the loading spinner instead of the "no model" message.</summary>
        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(NoModelCard),
                new PropertyMetadata(true, OnIsLoadingChanged));

        private static void OnIsLoadingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((NoModelCard)d).UpdatePanels();

        private void UpdatePanels()
        {
            LoadingPanel.Visibility = IsLoading ? Visibility.Visible : Visibility.Collapsed;
            EmptyPanel.Visibility = IsLoading ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void LoadDefault_Click(object sender, RoutedEventArgs e)
        {
            StatusText.Visibility = Visibility.Collapsed;
            LoadDefaultButton.IsEnabled = false;
            try
            {
                await MainWindow.Instance.LoadDefaultModelAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                StatusText.Visibility = Visibility.Visible;
            }
            finally
            {
                LoadDefaultButton.IsEnabled = true;
            }
        }
    }
}
