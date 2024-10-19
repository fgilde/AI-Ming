using System.ComponentModel;

namespace Aimmy2.Config;

public enum OverlayDrawingMethod
{
    [Description("WPF Canvas Overlay")]
    WpfWindowCanvas,
    [Description("Media Drawing Context VisualHost")]
    DrawingContextVisualHost,
    [Description("Desktop Graphic Context GDI Draw")]
    DesktopDC,
    [Description("Overlay form GDI Context")]
    OverlayFormGDI
}