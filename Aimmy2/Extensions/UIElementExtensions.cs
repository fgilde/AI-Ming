using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using Aimmy2.UILibrary;
using Class;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Aimmy2.Config;
using Aimmy2.InputLogic;
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
using Aimmy2.Class.Native;
using Nextended.UI.WPF.Converters;

namespace Aimmy2.Extensions;

public static class UIElementExtensions
{
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
        var menuItems = new List<MenuItem>();
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
        var parents = new List<T>();

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
        var children = new List<T>();

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



    public static void BindMouseGradientAngle(this Border sender, Func<bool>? condition = null)
    {

        LinearGradientBrush linearGradientBrush = null;

        if (sender.Background is LinearGradientBrush originalBrush)
        {
            linearGradientBrush = originalBrush.Clone();
            sender.Background = linearGradientBrush;
        }

        if (linearGradientBrush == null)
        {
            var resourceRotateTransform = sender.TryFindResource("RotaryGradient") as RotateTransform ??
                                          App.Current.TryFindResource("RotaryGradient") as RotateTransform;
            if (resourceRotateTransform != null)
            {
                var clonedRotateTransform = resourceRotateTransform.Clone();

                linearGradientBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0.5, 1),
                    RelativeTransform = new TransformGroup
                    {
                        Children = new TransformCollection
                        {
                            new ScaleTransform { CenterX = 0.5, CenterY = 0.5 },
                            new SkewTransform { CenterX = 0.5, CenterY = 0.5 },
                            clonedRotateTransform,
                            new TranslateTransform()
                        }
                    }
                };
                sender.Background = linearGradientBrush;
            }
        }

        if (linearGradientBrush?.RelativeTransform is TransformGroup newTransformGroup)
        {
            foreach (var transform in newTransformGroup.Children)
            {
                if (transform is RotateTransform rotateTransform)
                {
                    sender.BindMouseGradientAngle(rotateTransform, condition);
                    return;
                }
            }
        }

    }

    public static void BindMouseGradientAngle(this FrameworkElement sender, RotateTransform? transform, Func<bool>? condition)
    {
        if (transform == null)
            return;

        double currentGradientAngle = 0;
        sender.MouseMove += (s, e) =>
        {
            if (condition != null && !condition.Invoke())
            {
                transform.Angle = 0;
                return;
            }

            var currentMousePos = NativeAPIMethods.GetCursorPosition();
            var translatedMousePos = sender.PointFromScreen(new Point(currentMousePos.X, currentMousePos.Y));
            double targetAngle = Math.Atan2(translatedMousePos.Y - (sender.ActualHeight * 0.5), translatedMousePos.X - (sender.ActualWidth * 0.5)) * (180 / Math.PI);

            double angleDifference = (targetAngle - currentGradientAngle + 360) % 360;
            if (angleDifference > 180)
            {
                angleDifference -= 360;
            }

            angleDifference = Math.Max(Math.Min(angleDifference, 1), -1); // Clamp the angle difference between -1 and 1 (smoothing)
            currentGradientAngle = (currentGradientAngle + angleDifference + 360) % 360;
            transform.Angle = currentGradientAngle;
        };
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
            p.Background = new SolidColorBrush(Color.FromArgb(63, 60, 60, 60));
            p.BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255));
            p.BorderThickness = new Thickness(1, 0, 1, 0);
        }).InitWith(containerCfg);
        var grid = border.Add<Grid>(p =>
        {
            p.HorizontalAlignment = HorizontalAlignment.Stretch;
        });
        var innergrid = grid.Add<Grid>(p => p.HorizontalAlignment = HorizontalAlignment.Right);
        var panel = innergrid.Add<StackPanel>(p => p.Orientation = Orientation.Horizontal);
        grid.Add<TextBlock>(t =>
        {
            t.Text = title;
            t.Foreground = Brushes.White;
            t.Margin = new Thickness(10, 0, 5, 0);
            t.VerticalAlignment = VerticalAlignment.Center;
            t.HorizontalAlignment = HorizontalAlignment.Left;
        });
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
            p.Background = new SolidColorBrush(Color.FromArgb(63, 60, 60, 60));
            p.BorderBrush = new SolidColorBrush(Color.FromArgb(63, 255, 255, 255));
            p.BorderThickness = new Thickness(1, 0, 1, 0);
        });
        var spanel = border.Add<StackPanel>(p =>
        {
            p.Orientation = Orientation.Vertical;
            p.Margin = new Thickness(0, 5, 0, 10);
        });
        spanel.Add<Label>(l =>
        {
            l.Margin = new Thickness(5, 0, 0, 5);
            l.Content = title;
            l.FontSize = 13;
            l.Foreground = new SolidColorBrush(ApplicationConstants.Foreground);
        });
        var res = spanel.Add(new MultiKeyChanger(), multiKeyChanger =>
        {
            multiKeyChanger.Margin = new Thickness(10, 5, 10, 5);
            cfg?.Invoke(multiKeyChanger);
        });
        if (!string.IsNullOrEmpty(description) && AppConfig.Current?.ToggleState?.ShowHelpTexts == true)
        {
            spanel.Add<TextBlock>(l =>
            {
                l.Margin = new Thickness(10, 0, 10, 5);
                l.Text = description;
                l.TextWrapping = TextWrapping.Wrap;
                l.FontSize = 11;
                l.Foreground = new SolidColorBrush(ApplicationConstants.Foreground);
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
        var fontName = "Atkinson Hyperlegible";
        var fontFamily = (dropdown.TryFindResource(fontName) ?? Application.Current.TryFindResource(fontName) ?? Application.Current.MainWindow?.TryFindResource(fontName)) as FontFamily;
        var dropdownItem = new ComboBoxItem
        {
            Content = title,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
            FontFamily = fontFamily
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