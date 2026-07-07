namespace PowerAim.UILibrary
{
    /// <summary>
    /// Interaction logic for APButton.xaml
    /// </summary>
    public partial class APButton : System.Windows.Controls.UserControl
    {
        public APButton(string Text)
        {
            InitializeComponent();
            ButtonTitle.Content = Text;
        }

        /// <summary>The visible button caption. Lets callers flip it to a "busy" label while an async
        /// action runs (e.g. the benchmark), then restore it.</summary>
        public string Text
        {
            get => ButtonTitle.Content?.ToString() ?? "";
            set => ButtonTitle.Content = value;
        }
    }
}