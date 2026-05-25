using System.IO;
using System.Reflection;
using System.Windows;
using PowerAim.AILogic;
using PowerAim.Config;
using PowerAim.Theme;

namespace PowerAim
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeManager.Initialize();
            // Boot the OCR sampler — it ticks whenever OcrSettings.Enabled is true and
            // gracefully no-ops when no regions are configured or eng.traineddata is missing.
            OcrService.Instance.Start();
            // Best-effort load of the persisted AutoPlay learning model. Failures are silent —
            // the model just starts empty and the recorder fills it over time.
            try { AutoPlayLearningModel.Instance.Load(AppConfig.Current?.AutoPlayLearningSettings?.ModelPath); }
            catch { /* ignored */ }
            // Controller-mapping engine: KB↔Pad cross-mapping. Self-gates on profile presence,
            // so booting always is cheap. ViGEm bus driver is only required if the user enables
            // a profile with gamepad targets.
            PowerAim.InputLogic.Mapping.MappingEngine.Instance.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            OcrService.Instance.Stop();
            PowerAim.InputLogic.Mapping.MappingEngine.Instance.Dispose();
            base.OnExit(e);
        }

        public string ReadEmbeddedResource(string resourceName)
        {
            var assembly = typeof(App).Assembly;
            
            string defaultNamespace = typeof(App).Namespace;
            string fullResourceName = $"{defaultNamespace}.{resourceName}";

            using Stream stream = assembly.GetManifestResourceStream(fullResourceName);
            if (stream == null)
            {
                throw new Exception("Resource not found: " + fullResourceName);
            }

            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}