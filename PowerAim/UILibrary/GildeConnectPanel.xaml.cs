using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using PowerAim.Theme;

namespace PowerAim.UILibrary;

/// <summary>
///     Hosts a GildeConnect widget (contact form / support options) inside the app.
///     <para>
///     The widget is a web component from <c>connect.gilde.org</c>, so it needs a browser — but it does
///     NOT need a hosted page: the markup is handed to WebView2 directly via <c>NavigateToString</c>.
///     The widget API answers with <c>Access-Control-Allow-Origin: *</c>, which is what makes that work
///     from the string-navigation origin, and it saves the app from depending on poweraim.de shipping a
///     matching embed page.
///     </para>
///     <para>
///     The accent follows the app: whatever <see cref="ApplicationConstants.Theme"/> currently uses,
///     which is the active accent colour while the app is globally active and the normal one otherwise.
///     <c>accent</c> and <c>theme</c> are observed attributes of the web component, so a theme change is
///     pushed into the live widget instead of reloading it.
///     </para>
/// </summary>
public partial class GildeConnectPanel : UserControl
{
    private const string ScriptUrl = "https://connect.gilde.org/widgets/v1.js";
    private const string Project = "fgilde/AI-Ming";

    private bool _initialized;

    public GildeConnectPanel()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            ThemeManager.Applied -= OnThemeApplied;
            ThemeManager.Applied += OnThemeApplied;
            await InitializeAsync();
        };
        Unloaded += (_, _) => ThemeManager.Applied -= OnThemeApplied;
    }

    /// <summary>Which widget to render: <c>contact</c> or <c>support</c>.</summary>
    public string Widget
    {
        get => (string)GetValue(WidgetProperty);
        set => SetValue(WidgetProperty, value);
    }

    public static readonly DependencyProperty WidgetProperty =
        DependencyProperty.Register(nameof(Widget), typeof(string), typeof(GildeConnectPanel),
            new PropertyMetadata("contact"));

    /// <summary>Heading the widget shows above its content.</summary>
    public string WidgetTitle
    {
        get => (string)GetValue(WidgetTitleProperty);
        set => SetValue(WidgetTitleProperty, value);
    }

    public static readonly DependencyProperty WidgetTitleProperty =
        DependencyProperty.Register(nameof(WidgetTitle), typeof(string), typeof(GildeConnectPanel),
            new PropertyMetadata(""));

    private bool IsSupport => string.Equals(Widget, "support", StringComparison.OrdinalIgnoreCase);

    private static string AccentHex
    {
        get
        {
            var c = ApplicationConstants.Theme?.AccentColor ?? Colors.MediumPurple;
            return $"#{c.R:x2}{c.G:x2}{c.B:x2}";
        }
    }

    private async System.Threading.Tasks.Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;
        try
        {
            // Same user-data folder as the docs viewer: one cache, and it lives under %LocalAppData%
            // rather than the install dir, which the build script's name randomiser rotates.
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PowerAim", "WebView2");
            System.IO.Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await Web.EnsureCoreWebView2Async(env);

            Web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            Web.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // The widget reports its rendered height; the control is sized to match so the About page
            // scrolls as one piece instead of embedding a scrollable browser box inside a scroll view.
            Web.CoreWebView2.WebMessageReceived += (_, args) =>
            {
                if (double.TryParse(args.TryGetWebMessageAsString(), NumberStyles.Any,
                        CultureInfo.InvariantCulture, out var height) && height > 0)
                    Dispatcher.BeginInvoke(new Action(() => Web.Height = height));
            };

            // Anything that wants to leave the widget — a support provider, the homepage link — belongs
            // in the user's real browser, not in this frameless panel with no address bar or back button.
            Web.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                OpenExternally(args.Uri);
            };
            Web.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (args.IsUserInitiated && !args.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                    OpenExternally(args.Uri);
                }
            };

            Web.NavigateToString(BuildHtml());
        }
        catch (Exception)
        {
            // Usually a missing WebView2 runtime. Nothing to show, so offer the web page instead.
            ShowFallback();
        }
    }

    private void OnThemeApplied(object? sender, EventArgs e)
    {
        if (Web?.CoreWebView2 == null) return;
        var theme = (ApplicationConstants.Theme?.IsLight ?? false) ? "light" : "dark";
        // Both attributes are observed by the web component, so this re-styles the widget in place.
        _ = Web.CoreWebView2.ExecuteScriptAsync(
            $"(()=>{{const w=document.getElementById('w');if(!w)return;" +
            $"w.setAttribute('accent','{AccentHex}');w.setAttribute('theme','{theme}');}})()");
    }

    private string BuildHtml()
    {
        var tag = IsSupport ? "gilde-support" : "gilde-contact";
        var widget = IsSupport ? "support" : "contact";
        var theme = (ApplicationConstants.Theme?.IsLight ?? false) ? "light" : "dark";
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var title = System.Net.WebUtility.HtmlEncode(WidgetTitle ?? "");
        // Support options carry their own explanation; the contact form does not need one.
        var showDescription = IsSupport ? "true" : "false";

        // The support widget draws a QR code next to every provider. Below ~480px of widget width that
        // squeezes each label to one character per line, so the codes are dropped while the panel is
        // narrow — which happens as soon as the user makes the window small.
        var narrowRule = IsSupport
            ? """
                  const widget = document.getElementById('w');
                  let lastQr = null;
                  const applyQr = () => {
                    const width = widget.getBoundingClientRect().width;
                    if (!width) return;                     // not upgraded yet
                    const value = width >= 480 ? 'true' : 'false';
                    if (value === lastQr) return;           // setting it re-renders, which would loop
                    lastQr = value;
                    widget.setAttribute('show-support-qr', value);
                  };
                  addEventListener('load', applyQr);
                  addEventListener('resize', applyQr);
                  new ResizeObserver(applyQr).observe(widget);
                  applyQr();
              """
            : "";

        return $$"""
            <!doctype html>
            <html lang="{{language}}">
            <head>
              <meta charset="utf-8" />
              <style>
                html, body { margin:0; padding:0; background:transparent; overflow:hidden; }
                /* The widget fills its container, so cap it here — the host can be wider. */
                #w { max-width:560px; }
              </style>
            </head>
            <body>
              <script type="module" src="{{ScriptUrl}}"></script>
              <{{tag}} id="w"
                 project="{{Project}}" widget="{{widget}}" inline
                 theme="{{theme}}" accent="{{AccentHex}}" language="{{language}}"
                 title="{{title}}" width="560" radius="18" padding="28"
                 show-logo="true" show-description="{{showDescription}}" show-homepage="true"
                 show-preview-notice="false" show-footer="false"
                 support-layout="rows" show-support-icons="true" show-support-qr="true">{{title}}</{{tag}}>
              <script>
                // Report the rendered height to the host so the WPF control can size itself. The widget
                // grows and shrinks (validation errors, provider list), hence an observer, not a one-shot.
                const report = () => window.chrome?.webview?.postMessage(
                  String(Math.ceil(document.body.getBoundingClientRect().height)));
                new ResizeObserver(report).observe(document.body);
                addEventListener('load', report);
                {{narrowRule}}
              </script>
            </body>
            </html>
            """;
    }

    private void ShowFallback()
    {
        FallbackRun.Text = Locale.AboutConnectFallback;
        Fallback.Visibility = Visibility.Visible;
        Web.Visibility = Visibility.Collapsed;
    }

    private void FallbackLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        => OpenExternally(ApplicationConstants.ContactUrl);

    private static void OpenExternally(string url)
    {
        try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
        catch { /* no browser, nothing sensible left to do */ }
    }
}
