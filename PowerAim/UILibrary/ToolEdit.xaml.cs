using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.Visuality;

namespace PowerAim.UILibrary;

/// <summary>
///     In-window editor body for a <see cref="CustomTool"/> (hosted by MainWindow's ToolEditPage, the
///     same page pattern as AutoPlayProfileEdit / AntiRecoilProfileEdit). Edits the tool's name, its
///     user-defined <see cref="ToolOption"/>s (the <c>{token}</c> variables) and its ordered
///     <see cref="ToolAction"/> sequence. Per-action editing opens the modal
///     <see cref="ToolActionEditDialog"/>. The page (OpenToolEditor/CloseToolEditor) owns BeginEdit /
///     commit; this control just builds the UI bound to <see cref="Tool"/>.
/// </summary>
public partial class ToolEdit : UserControl
{
    // Segoe Fluent Icons glyphs as chars (keeps the source plain ASCII).
    private static readonly string GlyphDelete = ((char)0xE74D).ToString();
    private static readonly string GlyphUp = ((char)0xE70E).ToString();
    private static readonly string GlyphDown = ((char)0xE70D).ToString();
    private static readonly string GlyphEdit = ((char)0xEB7E).ToString();

    public ToolEdit()
    {
        InitializeComponent();
        DataContext = this;
    }

    public CustomTool Tool
    {
        get => (CustomTool)GetValue(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    public static readonly DependencyProperty ToolProperty =
        DependencyProperty.Register(nameof(Tool), typeof(CustomTool), typeof(ToolEdit),
            new PropertyMetadata(null, ToolChanged));

    private static void ToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ToolEdit)d;
        self.RebuildOptions();
        self.RebuildActions();
    }

    // ── Options ──────────────────────────────────────────────────────────────────────────────────

    private void RebuildOptions()
    {
        OptionsHost.Children.Clear();
        if (Tool == null) return;
        foreach (var opt in Tool.Options)
            OptionsHost.Children.Add(BuildOptionCard(opt));
    }

    private FrameworkElement BuildOptionCard(ToolOption opt)
    {
        var stack = new StackPanel();

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var nameBox = MakeTextBox(opt, nameof(ToolOption.Name), Locale.ToolOptionNamePlaceholder);
        Grid.SetColumn(nameBox, 0);
        header.Children.Add(nameBox);
        var del = IconButton(GlyphDelete, Locale.ToolRemoveOption, () =>
        {
            Tool.Options.Remove(opt);
            RebuildOptions();
        });
        Grid.SetColumn(del, 1);
        header.Children.Add(del);
        stack.Children.Add(header);

        stack.AddDropdown(Locale.ToolOptionType2, opt.Type, v =>
        {
            if (v == opt.Type) return;
            opt.Type = v;
            // Defer: rebuilding OptionsHost here would tear down the very dropdown firing this event
            // (re-entrancy) and crash. Let the selection event finish first.
            Dispatcher.BeginInvoke(new Action(RebuildOptions));
        }, dd => dd.BorderBrush = dd.Background = Brushes.Transparent);

        // Default value (typed plainly; Bool expects true/false)
        Label(stack, Locale.ToolOptionDefault);
        stack.Children.Add(MakeTextBox(opt, nameof(ToolOption.DefaultValue)));

        if (opt.Type == ToolOptionType.Enum)
        {
            Label(stack, Locale.ToolOptionChoices);
            var choices = new TextBox
            {
                Text = string.Join(", ", opt.EnumValues),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 5, 6, 5),
                Background = Brushes.Transparent,
                BorderBrush = UIElementExtensions.LookupBrush("FluentStroke", Colors.Gray),
                Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White)
            };
            choices.TextChanged += (_, _) =>
            {
                opt.EnumValues.Clear();
                foreach (var c in choices.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    opt.EnumValues.Add(c);
            };
            stack.Children.Add(choices);
        }
        else if (opt.Type == ToolOptionType.Number)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
            for (var i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            AddNumberCell(grid, 0, Locale.ToolOptionMin, opt, nameof(ToolOption.Min));
            AddNumberCell(grid, 1, Locale.ToolOptionMax, opt, nameof(ToolOption.Max));
            AddNumberCell(grid, 2, Locale.ToolOptionStep, opt, nameof(ToolOption.Step));
            stack.Children.Add(grid);
        }

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(6),
            Background = UIElementExtensions.LookupBrush("FluentSurface2", Colors.Transparent),
            BorderBrush = UIElementExtensions.LookupBrush("FluentStroke", Colors.Gray),
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }

    private static void AddNumberCell(Grid grid, int col, string label, object source, string prop)
    {
        var cell = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 4, 0, col == 2 ? 0 : 4, 0) };
        Label(cell, label);
        var tb = new TextBox
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 5, 6, 5),
            Background = Brushes.Transparent,
            BorderBrush = UIElementExtensions.LookupBrush("FluentStroke", Colors.Gray),
            Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White)
        };
        tb.SetBinding(TextBox.TextProperty, new Binding(prop) { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        cell.Children.Add(tb);
        Grid.SetColumn(cell, col);
        grid.Children.Add(cell);
    }

    private void AddOption_Click(object sender, RoutedEventArgs e)
    {
        if (Tool == null) return;
        var opt = new ToolOption { Name = "var" + (Tool.Options.Count + 1) };
        Tool.Options.Add(opt);
        RebuildOptions();
    }

    // ── Actions ──────────────────────────────────────────────────────────────────────────────────

    private void RebuildActions()
    {
        ActionsHost.Children.Clear();
        if (Tool == null) return;
        for (var i = 0; i < Tool.Actions.Count; i++)
            ActionsHost.Children.Add(BuildActionRow(Tool.Actions[i], i));
    }

    private FrameworkElement BuildActionRow(ToolAction action, int index)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock
        {
            Text = $"{index + 1}. {action.DisplayText}",
            FontFamily = new FontFamily("Segoe UI Variable Text"),
            Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White),
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(IconButton(GlyphUp, Locale.ToolMoveUp, () =>
        {
            if (index > 0) { Tool.Actions.Move(index, index - 1); RebuildActions(); }
        }));
        buttons.Children.Add(IconButton(GlyphDown, Locale.ToolMoveDown, () =>
        {
            if (index < Tool.Actions.Count - 1) { Tool.Actions.Move(index, index + 1); RebuildActions(); }
        }));
        buttons.Children.Add(IconButton(GlyphEdit, Locale.EditAction, () =>
        {
            var dlg = new ToolActionEditDialog { Title = Locale.ToolActionEdit, Action = action, Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() ?? false)
            {
                Tool.Actions[index] = dlg.Action;   // replace (handles a type swap)
                RebuildActions();
            }
        }));
        buttons.Children.Add(IconButton(GlyphDelete, Locale.DeleteAction, () =>
        {
            Tool.Actions.RemoveAt(index);
            RebuildActions();
        }));
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 4),
            Padding = new Thickness(10, 6, 6, 6),
            CornerRadius = new CornerRadius(6),
            Background = UIElementExtensions.LookupBrush("FluentSurface2", Colors.Transparent),
            BorderBrush = UIElementExtensions.LookupBrush("FluentStroke", Colors.Gray),
            BorderThickness = new Thickness(1),
            Child = grid
        };
    }

    private void AddAction_Click(object sender, RoutedEventArgs e)
    {
        if (Tool == null) return;
        var dlg = new ToolActionEditDialog { Title = Locale.ToolActionAdd, Action = new MoveMouseAction(), Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() ?? false)
        {
            Tool.Actions.Add(dlg.Action);
            RebuildActions();
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static void Label(StackPanel host, string text) => host.Add<Label>(l =>
    {
        l.Content = text;
        l.FontSize = 12;
        l.Padding = new Thickness(0);
        l.Margin = new Thickness(2, 6, 0, 2);
        l.Foreground = UIElementExtensions.LookupBrush("FluentTextSecondary", ApplicationConstants.Foreground);
    });

    private static TextBox MakeTextBox(object source, string prop, string? placeholder = null)
    {
        var tb = new TextBox
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 5, 6, 5),
            Margin = new Thickness(0, 2, 0, 2),
            Background = Brushes.Transparent,
            BorderBrush = UIElementExtensions.LookupBrush("FluentStroke", Colors.Gray),
            Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White)
        };
        if (placeholder != null)
        {
            tb.Tag = placeholder;
            if (Application.Current.TryFindResource("placeHolder") is Style ph) tb.Style = ph;
        }
        tb.SetBinding(TextBox.TextProperty, new Binding(prop) { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        return tb;
    }

    private Button IconButton(string glyph, string tooltip, Action onClick)
    {
        var b = new Button
        {
            Style = (Style)FindResource("FluentIconButton"),
            Content = glyph,
            ToolTip = tooltip,
            Padding = new Thickness(0),
            FontSize = 16,
            Width = 36,
            Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White)
        };
        b.Click += (_, _) => onClick();
        return b;
    }
}
