using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using PowerAim.UILibrary;
using Class;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using PowerAim.Config;
using PowerAim.InputLogic;
using InputLogic;
using Nextended.Core.Extensions;
using Nextended.Core.Helper;
using UILibrary;
using Microsoft.Xaml.Behaviors.Core;
using Nextended.UI.Helper;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Accord.Diagnostics;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using PowerAim.Class.Native;
using Nextended.UI.WPF.Converters;

namespace PowerAim.Extensions;

public static class UIElementExtensions
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

    public static IntPtr GetHandleSafe(this Window window)
    {
        return !window.CheckAccess() 
            ? window.Dispatcher.Invoke(() => GetHandleSafe(window)) 
            : new WindowInteropHelper(window).Handle;
    }

    public static void EnsureRenderedAndInitialized(this UIElement uiElement)
    {
        uiElement.Measure(new Size(Double.PositiveInfinity, Double.PositiveInfinity));
        uiElement.Arrange(new Rect(uiElement.DesiredSize));
        uiElement.UpdateLayout();

        // Force WPF to recalculate the layout and sizes
        uiElement.InvalidateMeasure();
        uiElement.InvalidateArrange();
    }

    public static ItemCollection AddRange(this ItemCollection collection, IEnumerable<UIElement> elements)
    {
        foreach (var element in elements)
        {
            collection.Add(element);
        }

        return collection;
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
                Foreground = Brushes.Black,
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

    public static void Center(this Window window, System.Windows.Forms.Screen? screen = null)
    {
        window.Center(screen?.Bounds ?? System.Windows.Forms.Screen.PrimaryScreen.Bounds);
    }

    public static void Center(this Window window, System.Drawing.Rectangle r)
    {
        if (!window.CheckAccess())
        {
            window.Dispatcher.Invoke(() => window.Center(r));
            return;
        }
        var center = r.GetCenter();
        var rect = new RectangleF((float)(center.X - window.Width / 2), (float)(center.Y - window.Height / 2), (float)window.Width, (float)window.Height);
        MoveTo(window, rect);
    }

    public static void MoveTo(this Window window, System.Drawing.RectangleF r, Thickness? padding = null)
    {
        if (!window.CheckAccess())
        {
            window.Dispatcher.Invoke(() => window.MoveTo(r));
            return;
        }

        window.WindowState = WindowState.Normal;
        window.Left = r.Left;
        window.Top = r.Top;
        window.Width = r.Width;
        window.Height = r.Height;
        if (padding != null)
        {
            window.Left += padding.Value.Left;
            window.Top += padding.Value.Top;
            window.Width = Math.Max(window.Width - (padding.Value.Right + padding.Value.Right), 10);
            window.Height = Math.Max(window.Height - (padding.Value.Top + padding.Value.Bottom), 10);
        }
    }

    public static void MoveToScreen(this Window window, System.Windows.Forms.Screen screen)
    {
        window.MoveTo(screen.Bounds);
    }



    public static T InitWith<T>(this T component, Action<T>? cfg) where T : UIElement
    {
        if (cfg != null)
        {
            if (component.IsVisible)
            {
                cfg.Invoke(component);
            }
            else
            {
                DependencyPropertyChangedEventHandler visibleChangeHandler = null;
                visibleChangeHandler = (s, e) => component.Dispatcher.BeginInvoke(() =>
                {
                    component.IsVisibleChanged -= visibleChangeHandler;
                    cfg.Invoke(component);
                });
                component.IsVisibleChanged += visibleChangeHandler;
            }
        }

        return component;
    }

    public static T[] FindParents<T>(this UIElement element, Func<T, bool>? predicate = null) where T : UIElement
    {
        predicate ??= _ => true;
        List<T> parents = [];

        DependencyObject current = element;
        while (current != null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is T parent && predicate(parent))
            {
                parents.Add(parent);
            }
        }

        return parents.ToArray();
    }

    public static T[] FindChildren<T>(this UIElement element, Func<T, bool>? predicate = null) where T : UIElement
    {
        predicate ??= _ => true;
        List<T> children = [];

        int childCount = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if (child is T typedChild && predicate(typedChild))
            {
                children.Add(typedChild);
            }

            if (child is UIElement uiElement)
            {
                children.AddRange(uiElement.FindChildren(predicate));
            }
        }

        return children.ToArray();
    }



    public static T RemoveAll<T>(this T panel) where T : Panel
    {
        panel.Children.Clear();
        return panel;
    }

    public static T Add<T>(this IAddChild panel, Func<T> ctor, Action<T>? cfg = null) where T : UIElement => panel.Add<T>(ctor(), cfg);

    public static T Add<T>(this IAddChild panel, Action<T>? cfg = null) where T : UIElement, new() => panel.Add(new T(), cfg);

    public static T Add<T>(this IAddChild panel, T element, Action<T>? cfg = null) where T : UIElement
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            panel.AddChild(element);
        });
        if (cfg != null)
            element.InitWith(cfg);
        return element;
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
        var changer = panel.AddKeyChanger(code, () => keyCodeValue, bindingManager);
        changer.WithBorder = false;
        changer.KeyConfigName = code;
        changer.KeyBind = keyCodeValue;
        changer.ShowTitle = false;
        changer.BindingManager = bindingManager;
        if(changerCfg != null)
            changerCfg.Invoke(changer);
        changer.GlobalKeyPressed += (sender, args) =>
        {
            if (!updating && toggle.IsEnabled)
            {
                updating = true;
                toggle.ToggleState();
                if (AppConfig.Current.ToggleState.ShowToggleNotifications && !string.IsNullOrWhiteSpace(title))
                {
                    var fmt = toggle.Checked ? PowerAim.Locale.ToggleTurnedOnFormat : PowerAim.Locale.ToggleTurnedOffFormat;
                    try { new global::Visuality.NoticeBar(string.Format(fmt, title), 1800).Show(); }
                    catch { /* notice bar is best-effort feedback only */ }
                }
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
        var keyChanger = panel.Add(new AKeyChanger(title, keybind), keyChanger =>
        {
            cfg?.Invoke(keyChanger);
            keyChanger.BindingManager = bindingManager;
            keyChanger.KeyConfigName = code;
        });

        return keyChanger;
    }

    internal static AKeyChanger AddKeyChanger(this IAddChild panel, string title, StoredInputBinding keybind, InputBindingManager? bindingManager = null, Action<AKeyChanger>? cfg = null) 
        => AddKeyChanger(panel, title, title, keybind, bindingManager, cfg);

    public static AColorChanger AddColorChanger(this IAddChild panel, string title)
    {
        return panel.Add(new AColorChanger(title));
    }

    public static ASlider AddSlider(this IAddChild panel, string title, string label, double frequency, double buttonsteps, double min, double max, bool forAntiRecoil = false)
    {
        var slider = new ASlider(title, label, frequency)
        {
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
        string path = title; // TODO: DIe sind doch alle dumm
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