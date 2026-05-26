using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace PowerAim.Visuality;

/// <summary>
///     Fluent-styled, in-app message dialog. Use the static <see cref="Show"/> overloads instead of
///     <see cref="MessageBox"/>. When an <c>owner</c> is supplied and its content root is a Grid, the
///     dialog slides down from the top of that window. Otherwise it appears as a centered modal window.
/// </summary>
public partial class MessageDialog : UserControl
{
    public enum DialogButtons { OK, OKCancel, YesNo, YesNoCancel }
    public enum DialogIcon { None, Info, Success, Warning, Error, Question }
    public enum DialogResult { None, OK, Cancel, Yes, No }

    private DispatcherFrame? _frame;
    private DialogResult _result = DialogResult.None;
    private Grid? _overlayHost;
    private Border? _dimmer;
    private Window? _standalone;
    private TranslateTransform? _translate;
    private DialogResult _defaultResult = DialogResult.None;
    private DialogResult _escapeResult = DialogResult.None;

    public MessageDialog()
    {
        InitializeComponent();
    }

    // ============================ Public API ============================

    /// <summary>
    ///     Show a fluent message dialog. Blocks the calling thread (via DispatcherFrame) until the user
    ///     answers, then returns their choice.
    /// </summary>
    /// <param name="message">Body text — supports wrapping.</param>
    /// <param name="title">Optional title; falls back to <c>Locale.Title</c>.</param>
    /// <param name="buttons">Button set to show.</param>
    /// <param name="icon">Icon variant; affects color too.</param>
    /// <param name="owner">Parent window — when its content is a Grid, the dialog slides down from the top of that window.</param>
    /// <param name="defaultResult">Which button is the accent/default. Also returned when Esc is pressed (unless Cancel/No is present).</param>
    public static DialogResult Show(
        string message,
        string? title = null,
        DialogButtons buttons = DialogButtons.OK,
        DialogIcon icon = DialogIcon.Info,
        Window? owner = null,
        DialogResult defaultResult = DialogResult.None)
    {
        if (!Application.Current.Dispatcher.CheckAccess())
        {
            return Application.Current.Dispatcher.Invoke(
                () => Show(message, title, buttons, icon, owner, defaultResult));
        }

        owner ??= TryGetActiveWindow();

        var dialog = new MessageDialog();
        dialog.SetupContent(title, message, icon, buttons, defaultResult);

        if (owner is not null && owner.Content is Grid hostGrid && PresentationSource.FromVisual(owner) is not null)
        {
            return dialog.ShowOverlay(owner, hostGrid);
        }
        return dialog.ShowAsWindow(owner);
    }

    /// <summary>Convenience: info dialog with an OK button.</summary>
    public static DialogResult Info(string message, string? title = null, Window? owner = null)
        => Show(message, title, DialogButtons.OK, DialogIcon.Info, owner);

    /// <summary>Convenience: warning dialog with an OK button.</summary>
    public static DialogResult Warn(string message, string? title = null, Window? owner = null)
        => Show(message, title, DialogButtons.OK, DialogIcon.Warning, owner);

    /// <summary>Convenience: error dialog with an OK button.</summary>
    public static DialogResult Error(string message, string? title = null, Window? owner = null)
        => Show(message, title, DialogButtons.OK, DialogIcon.Error, owner);

    /// <summary>Convenience: Yes/No confirmation. Returns true when Yes was clicked.</summary>
    public static bool Confirm(string message, string? title = null, Window? owner = null,
        DialogIcon icon = DialogIcon.Question, DialogResult defaultResult = DialogResult.Yes)
        => Show(message, title, DialogButtons.YesNo, icon, owner, defaultResult) == DialogResult.Yes;

    // ============================ Setup ============================

    private void SetupContent(string? title, string message, DialogIcon icon, DialogButtons buttons, DialogResult defaultResult)
    {
        TitleText.Text = string.IsNullOrWhiteSpace(title) ? Locale.Title : title!;
        MessageText.Text = message ?? "";
        _defaultResult = defaultResult;
        _escapeResult = ResolveEscapeResult(buttons, defaultResult);

        ApplyIcon(icon);
        BuildButtons(buttons, defaultResult);
    }

    private static DialogResult ResolveEscapeResult(DialogButtons buttons, DialogResult defaultResult)
    {
        return buttons switch
        {
            DialogButtons.OK => DialogResult.OK,
            DialogButtons.OKCancel => DialogResult.Cancel,
            DialogButtons.YesNo => defaultResult == DialogResult.Yes ? DialogResult.None : DialogResult.No,
            DialogButtons.YesNoCancel => DialogResult.Cancel,
            _ => DialogResult.None
        };
    }

    private void ApplyIcon(DialogIcon icon)
    {
        if (icon == DialogIcon.None)
        {
            IconHolder.Visibility = Visibility.Collapsed;
            return;
        }

        IconHolder.Visibility = Visibility.Visible;
        var (glyph, brush) = icon switch
        {
            DialogIcon.Info     => ("", (Brush?)null),                                  // Info → accent
            DialogIcon.Success  => ("", new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))),
            DialogIcon.Warning  => ("", new SolidColorBrush(Color.FromRgb(0xF2, 0xA8, 0x2E))),
            DialogIcon.Error    => ("", new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x38))),
            DialogIcon.Question => ("", (Brush?)null),                                  // Question → accent
            _                   => ("", null)
        };
        IconGlyph.Text = glyph;
        if (brush is not null)
        {
            brush.Freeze();
            IconHolder.Background = brush;
            IconGlyph.Foreground = Brushes.White;
        }
        else
        {
            IconHolder.SetResourceReference(BackgroundProperty, "FluentAccent");
            IconGlyph.SetResourceReference(ForegroundProperty, "FluentAccentForeground");
        }
    }

    private void BuildButtons(DialogButtons buttons, DialogResult defaultResult)
    {
        ButtonsPanel.Children.Clear();
        switch (buttons)
        {
            case DialogButtons.OK:
                AddButton(Locale.Ok, DialogResult.OK, accent: true);
                break;
            case DialogButtons.OKCancel:
                AddButton(Locale.Cancel, DialogResult.Cancel, accent: defaultResult == DialogResult.Cancel);
                AddButton(Locale.Ok, DialogResult.OK, accent: defaultResult != DialogResult.Cancel);
                break;
            case DialogButtons.YesNo:
                AddButton(Locale.No, DialogResult.No, accent: defaultResult == DialogResult.No);
                AddButton(Locale.Yes, DialogResult.Yes, accent: defaultResult != DialogResult.No);
                break;
            case DialogButtons.YesNoCancel:
                AddButton(Locale.Cancel, DialogResult.Cancel, accent: defaultResult == DialogResult.Cancel);
                AddButton(Locale.No, DialogResult.No, accent: defaultResult == DialogResult.No);
                AddButton(Locale.Yes, DialogResult.Yes,
                    accent: defaultResult is DialogResult.Yes or DialogResult.None);
                break;
        }
    }

    private void AddButton(string text, DialogResult result, bool accent)
    {
        var btn = new Button
        {
            Content = text,
            MinWidth = 96,
            MinHeight = 34,
            Margin = new(8, 0, 0, 0),
            Padding = new(14, 6, 14, 6)
        };
        btn.SetResourceReference(StyleProperty, accent ? "FluentAccentButton" : "FluentStandardButton");
        btn.Click += (_, _) => Complete(result);
        if (result == _defaultResult || (_defaultResult == DialogResult.None && accent))
        {
            btn.IsDefault = true;
        }
        if (result == _escapeResult)
        {
            btn.IsCancel = true;
        }
        ButtonsPanel.Children.Add(btn);
    }

    // ============================ Show modes ============================

    private static Window? TryGetActiveWindow()
    {
        var app = Application.Current;
        if (app is null) return null;
        foreach (Window w in app.Windows)
        {
            if (w.IsActive) return w;
        }
        return app.MainWindow;
    }

    private DialogResult ShowOverlay(Window owner, Grid hostGrid)
    {
        // Build the overlay host covering the entire content grid
        _overlayHost = new() { ClipToBounds = true, IsHitTestVisible = true };

        var rowCount = Math.Max(hostGrid.RowDefinitions.Count, 1);
        var colCount = Math.Max(hostGrid.ColumnDefinitions.Count, 1);
        Grid.SetRowSpan(_overlayHost, rowCount);
        Grid.SetColumnSpan(_overlayHost, colCount);

        _dimmer = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)),
            Opacity = 0
        };
        // Clicking the dimmer dismisses with the escape result (Cancel/No or last button).
        _dimmer.MouseLeftButtonDown += (_, _) => Complete(_escapeResult);
        _overlayHost.Children.Add(_dimmer);

        _translate = new(0, -200);
        RenderTransform = _translate;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        Opacity = 0;
        Margin = new(0);
        _overlayHost.Children.Add(this);

        hostGrid.Children.Add(_overlayHost);
        Panel.SetZIndex(_overlayHost, 10000);

        // Esc dismiss handler at window level
        KeyEventHandler keyHandler = (_, e) =>
        {
            if (e.Key == Key.Escape) { Complete(_escapeResult); e.Handled = true; }
            else if (e.Key == Key.Enter) { Complete(_defaultResult != DialogResult.None ? _defaultResult : DialogResult.OK); e.Handled = true; }
        };
        owner.PreviewKeyDown += keyHandler;

        // Animate
        UpdateLayout();
        var initialY = -Math.Max(60, ActualHeight + 24);
        _translate.Y = initialY;
        var slide = new DoubleAnimation
        {
            From = initialY,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var fade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(200) };
        var dimFade = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(240) };
        _translate.BeginAnimation(TranslateTransform.YProperty, slide);
        BeginAnimation(OpacityProperty, fade);
        _dimmer.BeginAnimation(OpacityProperty, dimFade);

        // Focus the default button
        Loaded += (_, _) => Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var child in ButtonsPanel.Children)
            {
                if (child is Button b && b.IsDefault) { b.Focus(); break; }
            }
        }), DispatcherPriority.Input);

        // Block until result
        _frame = new();
        try
        {
            Dispatcher.PushFrame(_frame);
        }
        finally
        {
            owner.PreviewKeyDown -= keyHandler;
        }
        return _result;
    }

    private DialogResult ShowAsWindow(Window? owner)
    {
        _standalone = new()
        {
            Content = this,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = new SolidColorBrush(Colors.Transparent),
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStartupLocation = owner is not null
                ? WindowStartupLocation.CenterOwner
                : WindowStartupLocation.CenterScreen,
            Owner = owner,
            Topmost = true
        };
        Margin = new(16);
        _standalone.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Complete(_escapeResult); e.Handled = true; }
        };
        _standalone.ShowDialog();
        return _result;
    }

    // ============================ Completion ============================

    private bool _completing;

    private void Complete(DialogResult result)
    {
        if (_completing) return;
        if (result == DialogResult.None) return;
        _completing = true;
        _result = result;

        if (_standalone is not null)
        {
            _standalone.Close();
            return;
        }

        // Animate out
        if (_overlayHost?.Parent is Panel parent && _translate is not null)
        {
            var slide = new DoubleAnimation
            {
                From = _translate.Y,
                To = -Math.Max(60, ActualHeight + 24),
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fade = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(180) };
            var dimFade = new DoubleAnimation { From = 1, To = 0, Duration = TimeSpan.FromMilliseconds(200) };
            slide.Completed += (_, _) =>
            {
                parent.Children.Remove(_overlayHost);
                if (_frame is not null) _frame.Continue = false;
            };
            _translate.BeginAnimation(TranslateTransform.YProperty, slide);
            BeginAnimation(OpacityProperty, fade);
            if (_dimmer is not null) _dimmer.BeginAnimation(OpacityProperty, dimFade);
        }
        else
        {
            if (_frame is not null) _frame.Continue = false;
        }
    }
}
