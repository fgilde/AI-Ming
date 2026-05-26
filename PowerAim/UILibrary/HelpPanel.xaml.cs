using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;

namespace PowerAim.UILibrary;

/// <summary>
///     Embedded documentation viewer. Hosts a WebView2 control pointed at
///     <see cref="ApplicationConstants.DocsUrl"/> with a small toolbar (back / forward / reload /
///     open in system browser / pop out) on top so the user can read the docs without leaving the
///     app, and detach the view into its own non-modal window when they need both PowerAim and the
///     docs visible at the same time.
///     <para>
///     Same UserControl is instantiated both as a navigation page inside <c>MainWindow</c> and
///     inside <see cref="PowerAim.Visuality.HelpWindow"/> — the pop-out button hides itself when
///     it's already in a stand-alone window so users can't trigger a recursive pop-out from there.
///     </para>
/// </summary>
public partial class HelpPanel : UserControl
{
    /// <summary>Fired when the embedded Back button is clicked — only relevant when hosted in MainWindow.</summary>
    public event EventHandler? BackRequested;

    /// <summary>Hide the Back/PopOut buttons when hosted in a stand-alone window.</summary>
    public bool IsHostedInWindow
    {
        get;
        set
        {
            field = value;
            if (BackBtn  != null) BackBtn.Visibility  = value ? Visibility.Collapsed : Visibility.Visible;
            if (PopOutBtn != null) PopOutBtn.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public HelpPanel()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeWebViewAsync();
        Unloaded += (_, _) =>
        {
            // Don't dispose the WebView itself — the WPF host re-creates it on reload — but stop
            // any pending navigations so the next time we open the page we get a clean state.
            try { Web?.Stop(); } catch { /* ignored */ }
        };
    }

    /// <summary>
    ///     One-shot WebView2 bootstrap. Creates a per-app user-data folder so cookies / cache live
    ///     under %LocalAppData% (not the install dir, which may be read-only or rotated by the
    ///     build-script's AssemblyName randomiser), then navigates to <see cref="ApplicationConstants.DocsUrl"/>.
    /// </summary>
    private async System.Threading.Tasks.Task InitializeWebViewAsync()
    {
        try
        {
            var userDataFolder = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PowerAim", "WebView2");
            System.IO.Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await Web.EnsureCoreWebView2Async(env);

            Web.CoreWebView2.SourceChanged += (_, _) =>
                Dispatcher.BeginInvoke(new Action(() => UrlBox.Text = Web.Source?.ToString() ?? ""));
            Web.CoreWebView2.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                    ShowStatus($"Navigation failed (HTTP {args.HttpStatusCode}). Try Refresh, or use 'Open in browser'.");
                else
                    HideStatus();
            };

            Web.Source = new Uri(ApplicationConstants.DocsUrl);
            UrlBox.Text = ApplicationConstants.DocsUrl;
        }
        catch (Exception ex)
        {
            // Most common failure: Microsoft Edge WebView2 Runtime isn't installed on this machine.
            // Surface that clearly so the user can click "Open in browser" as a workaround.
            ShowStatus(
                "Could not initialize embedded browser (WebView2). " +
                "Either install the WebView2 Runtime from https://go.microsoft.com/fwlink/p/?LinkId=2124703 " +
                $"or use 'Open in browser' above. Details: {ex.Message}");
        }
    }

    private void ShowStatus(string text)
    {
        StatusBar.Text = text;
        StatusBar.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        StatusBar.Visibility = Visibility.Collapsed;
    }

    private void Back_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        try { if (Web?.CoreWebView2?.CanGoForward == true) Web.GoForward(); }
        catch { /* ignored */ }
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        try { Web?.Reload(); } catch { /* ignored */ }
    }

    private void OpenInBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = Web?.Source?.ToString();
            if (string.IsNullOrEmpty(url)) url = ApplicationConstants.DocsUrl;
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex) { ShowStatus($"Could not launch browser: {ex.Message}"); }
    }

    private void PopOut_Click(object sender, RoutedEventArgs e)
    {
        // Re-use the existing window if open, otherwise spawn one.
        if (PowerAim.Visuality.HelpWindow.Current is { } existing)
        {
            existing.Activate();
            return;
        }
        var win = new PowerAim.Visuality.HelpWindow();
        win.Show();
    }
}
