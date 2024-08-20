using System.IO;
using System.Reflection;

namespace Aimmy2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
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