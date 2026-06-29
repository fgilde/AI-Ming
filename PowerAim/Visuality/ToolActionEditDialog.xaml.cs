using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using PowerAim.Config;
using PowerAim.Extensions;
using PowerAim.InputLogic;
using PowerAim.UILibrary;

namespace PowerAim.Visuality;

/// <summary>
///     Edits one <see cref="ToolAction"/>. Because the action type is polymorphic, a type dropdown at
///     the top swaps the concrete action instance (MoveMouse / Click / SendKeys / RunExe / Delay) and
///     the field area below is rebuilt for that type. Mirrors <c>AutoPlayActionEditDialog</c>'s
///     dynamic-UI approach. The (possibly swapped) instance is exposed as <see cref="Action"/> on save.
/// </summary>
public partial class ToolActionEditDialog : BaseDialog
{
    private enum Kind { MoveMouse, Click, SendKeys, RunExe, Delay }

    private ToolAction _action = new MoveMouseAction();

    public ToolActionEditDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    public ToolAction Action
    {
        get => _action;
        set
        {
            _action = value ?? new MoveMouseAction();
            _action.BeginEdit();
            BuildTypeDropdown();
            BuildFields();
        }
    }

    private static Kind KindOf(ToolAction a) => a switch
    {
        MoveMouseAction => Kind.MoveMouse,
        ClickAction => Kind.Click,
        SendKeysAction => Kind.SendKeys,
        RunExeAction => Kind.RunExe,
        DelayAction => Kind.Delay,
        _ => Kind.MoveMouse
    };

    private static ToolAction Create(Kind k) => k switch
    {
        Kind.MoveMouse => new MoveMouseAction(),
        Kind.Click => new ClickAction(),
        Kind.SendKeys => new SendKeysAction(),
        Kind.RunExe => new RunExeAction(),
        Kind.Delay => new DelayAction(),
        _ => new MoveMouseAction()
    };

    private void BuildTypeDropdown()
    {
        TypeHost.RemoveAll();
        TypeHost.AddDropdown(Locale.ToolActionType, KindOf(_action), kind =>
        {
            if (kind == KindOf(_action)) return;
            _action = Create(kind);
            _action.BeginEdit();
            BuildFields();
        }, dropdown => dropdown.BorderBrush = dropdown.Background = Brushes.Transparent);
    }

    private void BuildFields()
    {
        FieldsHost.RemoveAll();
        switch (_action)
        {
            case MoveMouseAction mv:
                AddBool(FieldsHost, Locale.ToolMoveRelative, mv, nameof(MoveMouseAction.Relative));
                AddText(FieldsHost, Locale.ToolMoveX, mv, nameof(MoveMouseAction.X));
                AddText(FieldsHost, Locale.ToolMoveY, mv, nameof(MoveMouseAction.Y));
                break;

            case ClickAction c:
                FieldsHost.AddDropdown(Locale.ToolClickButton, c.Button, v => c.Button = v);
                FieldsHost.AddDropdown(Locale.ToolPressMode, c.Mode, v => c.Mode = v);
                break;

            case SendKeysAction sk:
                // Same control as ActionTrigger's action keys: allow duplicates + record-sequence
                // (captures per-key timing into MinTime), plus the Sequential/Simultaneous choice.
                var mkc = FieldsHost.AddMultiKeyChanger(Locale.ToolSendKeysLabel, "", m =>
                {
                    m.AllowDuplicates = true;
                    m.CanRecordSequence = true;
                });
                mkc.Keys = sk.Keys;
                FieldsHost.AddDropdown(Locale.ToolSendExecutionMode, sk.ExecutionMode, v => sk.ExecutionMode = v);
                FieldsHost.AddDropdown(Locale.ToolPressMode, sk.Mode, v => sk.Mode = v);
                break;

            case RunExeAction r:
                AddText(FieldsHost, Locale.ToolExePath, r, nameof(RunExeAction.Path), browse: true);
                AddText(FieldsHost, Locale.ToolExeArgs, r, nameof(RunExeAction.Args));
                AddBool(FieldsHost, Locale.ToolExeAsAdmin, r, nameof(RunExeAction.AsAdmin));
                AddBool(FieldsHost, Locale.ToolExeWaitForExit, r, nameof(RunExeAction.WaitForExit));
                break;

            case DelayAction d:
                AddText(FieldsHost, Locale.ToolDelayMs, d, nameof(DelayAction.Milliseconds));
                break;
        }
    }

    private static void AddText(StackPanel host, string label, object source, string prop, bool browse = false)
    {
        host.Add<Label>(l =>
        {
            l.Content = label;
            l.FontSize = 13;
            l.Padding = new Thickness(0);
            l.Margin = new Thickness(2, 6, 0, 2);
            l.Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White);
        });

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var tb = new TextBox
        {
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 5, 6, 5),
            Background = Brushes.Transparent,
            BorderBrush = UIElementExtensions.LookupBrush("FluentStroke", Colors.Gray),
            Foreground = UIElementExtensions.LookupBrush("FluentTextPrimary", Colors.White)
        };
        tb.SetBinding(TextBox.TextProperty, new Binding(prop)
        {
            Source = source,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        Grid.SetColumn(tb, 0);
        row.Children.Add(tb);

        if (browse)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var b = new APButton("…") { Margin = new Thickness(6, 0, 0, 0), MinWidth = 40 };
            b.Reader.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog { CheckFileExists = true };
                if (dlg.ShowDialog() == true && source is RunExeAction r) r.Path = dlg.FileName;
            };
            Grid.SetColumn(b, 1);
            row.Children.Add(b);
        }

        host.Children.Add(row);
    }

    private static void AddBool(StackPanel host, string label, object source, string prop)
    {
        var tgl = new AToggle
        {
            Text = label,
            BorderBrush = Brushes.Transparent,
            Background = Brushes.Transparent,
            Margin = new Thickness(0, 8, 0, 0)
        };
        tgl.SetBinding(AToggle.CheckedProperty, new Binding(prop)
        {
            Source = source,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        });
        host.Children.Add(tgl);
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _action.CancelEdit();
        DialogResult = false;
        Close();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _action.EndEdit();
        DialogResult = true;
        Close();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
}
