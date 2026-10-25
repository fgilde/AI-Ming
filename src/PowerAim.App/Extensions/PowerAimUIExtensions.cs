using PowerAim.UI.UILibrary;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using PowerAim.Config;
using Nextended.Core.Extensions;
using Nextended.Core.Helper;
using Microsoft.Xaml.Behaviors.Core;
using Nextended.UI.Helper;
using Nextended.UI.Input;
using Nextended.UI.WPF.Extensions;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;

namespace PowerAim.Extensions;

public static class PowerAimUIExtensions
{
    internal static SolidColorBrush LookupBrush(string key, Color fallback)
    {
        try
        {
            var app = Application.Current;
            if (app?.Resources?[key] is SolidColorBrush b)
                return b;
        }
        catch
        {
        }
        return new SolidColorBrush(fallback);
    }

    public static ICollection<MenuItem> ToMenuItems(this ListBox listBox, Action<MenuItem> onClick, Func<int, MenuItem, KeyGesture?> keyBindFn = null)
    {
        List<MenuItem> menuItems = [];
        var index = 0;
        foreach (object item in listBox.Items)
        {
            var menuItem = new MenuItem
            {
                Header = item?.ToString(),
                // No explicit foreground: inherit the themed MenuItem style (FluentTextPrimary) so
                // dropdown items stay readable in dark mode (hard-coded black was unreadable there).
                Tag = item,
            };
            menuItem.Command = new ActionCommand(() => onClick(menuItem));
            if (keyBindFn != null)
            {
                var gesture = keyBindFn(index, menuItem);
                if (gesture != null)
                {
                    MainWindow.Instance.InputBindings.Add(new InputBinding(menuItem.Command, gesture));
                    menuItem.InputGestureText = gesture.ConvertToString();
                }
            }
            menuItems.Add(menuItem);
            index++;
        }

        return menuItems;
    }

    internal static AToggle AddToggleWithKeyBind<T>(this T owner, string title, string key, InputBindingManager bindingManager, Action<AToggle>? cfg = null, Action<Border>? containerCfg = null, Action<AKeyChanger>? changerCfg = null) where T : IAddChild, new()
    {
        var border = owner.Add<Border>(p =>
        {
            p.MinWidth = 220;
            p.Background = Brushes.Transparent;
            p.BorderBrush = Brushes.Transparent;
            p.BorderThickness = new Thickness(0);
            p.CornerRadius = new CornerRadius(0);
            p.Margin = new Thickness(0);
            p.Padding = new Thickness(0);
        }).InitWith(containerCfg);
        var grid = border.Add<Grid>(p =>
        {
            p.HorizontalAlignment = HorizontalAlignment.Stretch;
        });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleBlock = grid.Add<TextBlock>(t =>
        {
            t.Text = title;
            t.Foreground = LookupBrush("FluentTextPrimary", Colors.White);
            t.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text");
            t.FontSize = 13;
            t.Margin = new Thickness(10, 0, 8, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Left;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
        });
        Grid.SetColumn(titleBlock, 0);

        var panel = grid.Add<StackPanel>(p =>
        {
            p.Orientation = Orientation.Horizontal;
            p.VerticalAlignment = VerticalAlignment.Center;
            p.HorizontalAlignment = HorizontalAlignment.Right;
        });
        Grid.SetColumn(panel, 1);
        var toggle = new AToggle("");
        toggle.Background = toggle.BorderBrush = Brushes.Transparent;
        var code = AKeyChanger.CodeFor(key);

        var keyCodeValue = AppConfig.Current.BindingSettings[code];
        bool updating = false;
        var changer = panel.AddKeyChanger("", code, keyCodeValue, bindingManager);
        changer.WithBorder = false;
        changerCfg?.Invoke(changer);
        changer.GlobalKeyPressed += (_, _) =>
        {
            if (!updating && toggle.IsEnabled)
            {
                updating = true;
                toggle.ToggleState();
                PowerAim.Notifier.Notify(title, toggle.Checked);
                Task.Delay(300).ContinueWith(_ => updating = false);
            }
        };
        panel.Add(toggle, cfg);

        return toggle;
    }


    public static AToggle AddToggle(this IAddChild panel, string title, Action<AToggle>? cfg = null)
    {
        return panel.Add<AToggle>(toggle =>
        {
            toggle.Text = title;
            cfg?.Invoke(toggle);
        });
    }

    public static APButton AddButton(this IAddChild panel, string title, Action<APButton>? cfg = null)
    {
        return panel.Add(new APButton(title), button =>
        {
            cfg?.Invoke(button);
        });
    }

    internal static MultiKeyChanger AddMultiKeyChanger(this IAddChild panel, string title, string description = "", Action<MultiKeyChanger>? cfg = null)
    {
        var border = panel.Add<Border>(p =>
        {
            p.Background = Brushes.Transparent;
            p.BorderBrush = Brushes.Transparent;
            p.BorderThickness = new Thickness(0);
        });
        var spanel = border.Add<StackPanel>(p =>
        {
            p.Orientation = Orientation.Vertical;
            p.Margin = new Thickness(0, 5, 0, 10);
        });
        spanel.Add<Label>(l =>
        {
            l.Margin = new Thickness(2, 0, 0, 5);
            l.Content = title;
            l.FontSize = 13;
            l.Padding = new Thickness(0);
            l.Foreground = LookupBrush("FluentTextPrimary", Colors.White);
        });
        var res = spanel.Add(new MultiKeyChanger(), multiKeyChanger =>
        {
            multiKeyChanger.Margin = new Thickness(0, 5, 0, 5);
            cfg?.Invoke(multiKeyChanger);
        });
        if (!string.IsNullOrEmpty(description) && AppConfig.Current?.ToggleState?.ShowHelpTexts == true)
        {
            spanel.Add<TextBlock>(l =>
            {
                l.Margin = new Thickness(2, 0, 2, 5);
                l.Text = description;
                l.TextWrapping = TextWrapping.Wrap;
                l.FontSize = 11;
                l.Foreground = LookupBrush("FluentTextSecondary", ApplicationConstants.Foreground);
            });
        }

        return res;
    }
    
    internal static AKeyChanger AddKeyChanger(this IAddChild panel, string title, Func<StoredInputBinding> keybind,
        InputBindingManager? bindingManager = null, Action<AKeyChanger>? cfg = null) =>
        panel.AddKeyChanger(title, keybind(), bindingManager, cfg);

    internal static AKeyChanger AddKeyChanger(this IAddChild panel, string title, string code, Func<StoredInputBinding> keybind,
        InputBindingManager? bindingManager = null, Action<AKeyChanger>? cfg = null) =>
        panel.AddKeyChanger(title, code, keybind(), bindingManager, cfg);

    internal static AKeyChanger AddKeyChanger(this IAddChild panel, string title, string code, StoredInputBinding keybind, InputBindingManager? bindingManager = null, Action<AKeyChanger>? cfg = null)
    {
        // Id and manager are set up front so the hotkey works before the panel is first shown.
        var keyChanger = new AKeyChanger(title, keybind) { BindingManager = bindingManager, KeyConfigName = code };
        return panel.Add(keyChanger, cfg);
    }

    internal static AKeyChanger AddKeyChanger(this IAddChild panel, string title, StoredInputBinding keybind, InputBindingManager? bindingManager = null, Action<AKeyChanger>? cfg = null) 
        => AddKeyChanger(panel, title, title, keybind, bindingManager, cfg);

    public static AColorChanger AddColorChanger(this IAddChild panel, string title)
    {
        return panel.Add(new AColorChanger(title));
    }

    public static ASlider AddSlider(this IAddChild panel, string title, string label, double frequency, double buttonsteps, double min, double max, bool forAntiRecoil = false, int decimals = 2, bool snapToTick = true)
    {
        var slider = new ASlider(title, label, frequency)
        {
            Decimals = decimals,
            SnapToTick = snapToTick,
            Slider =
            {
                Minimum = min,
                Maximum = max,
                TickFrequency = frequency
            }
        };
        return panel.Add(slider);
    }

    public static ADropdown AddDropdown<T>(this IAddChild panel, string title, T value, IEnumerable<T> items, Action<T> onSelect, Action<ADropdown>? cfg = null, Func<T, string>? toStringFn = null)
    {
        toStringFn ??= v => v.ToString();
        var res = panel.Add<ADropdown>(new ADropdown(title), dropdown =>
        {
            cfg?.Invoke(dropdown);
        });
        foreach (var v in items)
        {
            res.AddDropdownItem(toStringFn(v), item =>
            {
                if (v.Equals(value))
                {
                    res.DropdownBox.SelectedItem = item;
                }

                item.Selected += (s, e) => { onSelect(v); };
            });
        }

        return res;
    }

    public static ADropdown AddDropdown<TEnum>(this IAddChild panel, string title, TEnum value, Action<TEnum> onSelect, Action<ADropdown>? cfg = null) where TEnum : struct, Enum
    {
        var res = panel.Add<ADropdown>(new ADropdown(title), dropdown =>
        {
            cfg?.Invoke(dropdown);
        });
        Enum<TEnum>.GetValues().Apply(v => res.AddDropdownItem(v.ToDescriptionString(), item =>
        {
            if (v.Equals(value))
            {
                res.DropdownBox.SelectedItem = item;
            }
            item.Selected += (s, e) =>
            {
                onSelect(v);
            };
        }));

        return res;
    }


    public static ComboBoxItem AddDropdownItem(this ADropdown dropdown, string title, Action<ComboBoxItem>? cfg = null)
    {
        var dropdownItem = new ComboBoxItem
        {
            Content = title
        };

        cfg?.Invoke(dropdownItem);
        dropdown.DropdownBox.Items.Add(dropdownItem);
        return dropdownItem;
    }

    public static AFileLocator AddFileLocator(this IAddChild panel, string title, string filter = "All files (*.*)|*.*", string dlExtension = "", Action<AFileLocator>? cfg = null)
    {
        string path = title;
        return panel.Add(new AFileLocator(title, path, filter, dlExtension), fileLocator =>
        {
            cfg?.Invoke(fileLocator);
        });
    }

    public static ATitle AddTitle(this IAddChild panel, string title, bool canMinimize = false, Action<ATitle>? cfg = null)
    {
        return panel.Add<ATitle>(new ATitle(title, canMinimize), atitle =>
        {
            cfg?.Invoke(atitle);
        });
    }

    public static ATitle AddSubTitle(this IAddChild panel, string title)
    {
        return panel.AddTitle(title, false, aTitle => aTitle.Border.CornerRadius = new CornerRadius(0));
    }

    public static void AddSeparator(this IAddChild panel)
    {
        panel.Add<ARectangleBottom>();
        panel.Add<ASpacer>();
    }

    public static void AddLine(this IAddChild panel)
    {
        panel.Add<Line>();
        panel.Add<ASpacer>();
    }

    public static ACredit AddCredit(this IAddChild panel, string name, string role, Action<ACredit>? cfg = null)
    {
        return panel.Add(new ACredit(name, role), credit =>
        {
            cfg?.Invoke(credit);
        });
    }

}