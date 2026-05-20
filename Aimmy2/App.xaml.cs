using System.IO;
using System.Reflection;
using System.Windows;
using Aimmy2.Config;
using Aimmy2.Theme;

namespace Aimmy2
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