using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using PowerAim.AI.AutoPlay;
using PowerAim.AI.Decisions;
using PowerAim.Config;

namespace PowerAim.UI.UILibrary
{
    /// <summary>Edits <see cref="AppConfig.DecisionModels"/> in place: add, tune, test, delete (not built-ins).</summary>
    public partial class DecisionModelList : UserControl
    {
        /// <summary>One dropdown entry: the enum value plus the text the user reads.</summary>
        public sealed record KindChoice(DecisionModelKind Value, string Label);

        private readonly ConditionalWeakTable<DecisionModelDefinition, TextBlock> _status = new();

        public ObservableCollection<DecisionModelDefinition> Models => AppConfig.Current.DecisionModels;

        public IReadOnlyList<KindChoice> Kinds { get; } =
        [
            new(DecisionModelKind.Heuristic, Locale.DecisionModelKindHeuristic),
            new(DecisionModelKind.Jev, Locale.DecisionModelKindJev),
            new(DecisionModelKind.OllamaText, Locale.DecisionModelKindOllamaText),
            new(DecisionModelKind.OllamaVision, Locale.DecisionModelKindOllamaVision),
            new(DecisionModelKind.Learned, Locale.DecisionModelKindLearned),
        ];

        public DecisionModelList()
        {
            InitializeComponent();
            DataContext = this;
            AppConfig.ConfigLoaded += (_, _) => Dispatcher.BeginInvoke(() => ModelsList.ItemsSource = Models);
        }

        private void StatusText_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock { Tag: DecisionModelDefinition model } text)
            {
                _status.AddOrUpdate(model, text);
                text.Text = Hint(model);
            }
        }

        private static string Hint(DecisionModelDefinition model) => model.Kind switch
        {
            DecisionModelKind.Learned => string.Format(Locale.LearnedSamplesFormat, DemonstrationStore.Instance.Count),
            DecisionModelKind.Heuristic => Locale.DecisionModelKindHeuristicHint,
            DecisionModelKind.Jev => Locale.DecisionModelKindJevHint,
            DecisionModelKind.OllamaText => Locale.DecisionModelKindOllamaTextHint,
            _ => Locale.DecisionModelKindOllamaVisionHint,
        };

        private async void Test_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not DecisionModelDefinition model) return;
            _status.TryGetValue(model, out var text);
            void Show(string s) { if (text != null) text.Text = s; }

            var runtime = DecisionModelFactory.Create(model);
            if (runtime == null) { Show(Locale.DecisionModelReady); return; }
            Show(Locale.DecisionModelTesting);
            bool ok;
            try { ok = await runtime.IsAvailableAsync(CancellationToken.None); }
            catch (Exception ex) { Show(string.Format(Locale.DecisionModelUnavailableFormat, ex.Message)); return; }
            Show(ok ? (model.Kind == DecisionModelKind.Learned ? Hint(model) : Locale.DecisionModelReady)
                    : string.Format(Locale.DecisionModelUnavailableFormat, runtime.LastError ?? "?"));
        }

        private void Add_Click(object sender, RoutedEventArgs e)
            => Models.Add(new DecisionModelDefinition { Name = Locale.NewDecisionModel, Kind = DecisionModelKind.OllamaText, Model = "qwen2.5:1.5b" });

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is not DecisionModelDefinition { IsBuiltIn: false } model) return;
            // Profiles pointing at it fall back to their legacy backend via DecisionModelRegistry.Resolve.
            Models.Remove(model);
        }
    }
}
