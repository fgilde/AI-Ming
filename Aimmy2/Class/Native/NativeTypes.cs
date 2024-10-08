using OpenCvSharp;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Aimmy2.Class.Native;

#region Structures

[StructLayout(LayoutKind.Sequential)]
internal struct MINPUT
{
    public MInputType type;
    public MInputUnion U;
    public static int Size => Marshal.SizeOf(typeof(INPUT));
}

[StructLayout(LayoutKind.Explicit)]
internal struct MInputUnion
{
    [FieldOffset(0)] public MOUSEINPUT mi;
}

internal enum MInputType : uint
{
    INPUT_MOUSE = 0,
}

[StructLayout(LayoutKind.Sequential)]
internal struct MMOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public MouseEventFlags dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct INPUT
{
    public uint type;
    public InputUnion u;
}

[StructLayout(LayoutKind.Explicit)]
internal struct InputUnion
{
    [FieldOffset(0)] public MOUSEINPUT mi;
    [FieldOffset(0)] public KEYBDINPUT ki;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MOUSEINPUT
{
    public int dx;
    public int dy;
    public uint mouseData;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[ComImport]
[Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComVisible(true)]
internal interface IInitializeWithWindow
{
    void Initialize(
        IntPtr hwnd);
}

[ComImport]
[Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[ComVisible(true)]
internal interface IGraphicsCaptureItemInterop
{
    IntPtr CreateForWindow(
        [In] IntPtr window,
        [In] ref Guid iid);

    IntPtr CreateForMonitor(
        [In] IntPtr monitor,
        [In] ref Guid iid);
}

[Serializable]
[StructLayout(LayoutKind.Sequential)]
public struct NativeRectangle(int left, int top, int right, int bottom)
{
    public int Left = left;
    public int Top = top;
    public int Right = right;
    public int Bottom = bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
public struct NativeMonitorInfo
{
    public int Size;
    public NativeRectangle Monitor;
    public NativeRectangle Work;
    public int Flags;
}

internal enum GetAncestorFlags
{
    // Retrieves the parent window. This does not include the owner, as it does with the GetParent function.
    GetParent = 1,

    // Retrieves the root window by walking the chain of parent windows.
    GetRoot = 2,

    // Retrieves the owned root window by walking the chain of parent and owner windows returned by GetParent.
    GetRootOwner = 3
}

internal enum GWL
{
    WNDPROC = -4,
    HINSTANCE = -6,
    HWNDPARENT = -8,
    STYLE = -16,
    EXSTYLE = -20,
    USERDATA = -21,
    ID = -12
}

internal enum DWMWINDOWATTRIBUTE : uint
{
    NCRenderingEnabled = 1,
    NCRenderingPolicy,
    TransitionsForceDisabled,
    AllowNCPaint,
    CaptionButtonBounds,
    NonClientRtlLayout,
    ForceIconicRepresentation,
    Flip3DPolicy,
    ExtendedFrameBounds,
    HasIconicBitmap,
    DisallowPeek,
    ExcludedFromPeek,
    Cloak,
    Cloaked,
    FreezeRepresentation
}

#endregion Structures

// Magnifier Window Styles
internal enum MagnifierStyle
{
    MS_SHOWMAGNIFIEDCURSOR = 0x0001,
    MS_CLIPAROUNDCURSOR = 0x0002,
    MS_INVERTCOLORS = 0x0004
}

// Filter Modes
internal enum FilterMode
{
    MW_FILTERMODE_EXCLUDE = 0,
    MW_FILTERMODE_INCLUDE = 1
}

[StructLayout(LayoutKind.Sequential)]
internal struct Transformation
{
    public float m00;
    public float m10;
    public float m20;
    public float m01;
    public float m11;
    public float m21;
    public float m02;
    public float m12;
    public float m22;

    public Transformation(float magnificationFactor)
        : this()
    {
        m00 = magnificationFactor;
        m11 = magnificationFactor;
        m22 = 1.0f;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct ColorEffect
{
    public float transform00;
    public float transform10;
    public float transform20;
    public float transform30;
    public float transform40;
    public float transform01;
    public float transform02;
    public float transform03;
    public float transform04;
    public float transform11;
    public float transform12;
    public float transform13;
    public float transform14;
    public float transform21;
    public float transform22;
    public float transform23;
    public float transform24;
    public float transform31;
    public float transform32;
    public float transform33;
    public float transform34;
    public float transform41;
    public float transform42;
    public float transform43;
    public float transform44;
}

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;

    public POINT()
    {
    }

    public POINT(int x, int y)
    {
        X = x;
        Y = y;
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public RECT()
    {
    }

    public Rectangle ToRectangle()
    {
        return new Rectangle(Left, Top, Right - Left, Bottom - Top);
    }

    public RECT(int left, int top, int right, int bottom)
    {
        Left = left;
        Right = right;
        Top = top;
        Bottom = bottom;
    }

    public RECT(int width, int height)
    {
        Left = 0;
        Top = 0;
        Right = width;
        Bottom = height;
    }

    public override bool Equals(object obj)
    {
        var r = (RECT)obj;
        return r.Left == Left && r.Right == Right && r.Top == Top && r.Bottom == Bottom;
    }

    public override int GetHashCode()
    {
        // Attempting a minor degree of "hash-ness" here
        return Left ^ Top ^ Right ^ Bottom;
    }

    public static bool operator ==(RECT a, RECT b)
    {
        return a.Left == b.Left && a.Right == b.Right && a.Top == b.Top && a.Bottom == b.Bottom;
    }

    public static bool operator !=(RECT a, RECT b)
    {
        return !(a == b);
    }
}

internal enum InputEventFlags
{
    INPUT_MOUSE = 0,
    INPUT_KEYBOARD = 1,
    KEYEVENTF_SCANCODE = 0x0008,
    MOUSEEVENTF_MOVE = 0x0001,
    MOUSEEVENTF_MOVE_NOCOALESCE = 0x2000,
    MOUSEEVENTF_LEFTDOWN = 0x0002,
    MOUSEEVENTF_LEFTUP = 0x0004,
    MOUSEEVENTF_RIGHTDOWN = 0x0008,
    MOUSEEVENTF_RIGHTUP = 0x0010,
    MOUSEEVENTF_MIDDLEDOWN = 0x0020,
    MOUSEEVENTF_MIDDLEUP = 0x0040,
    MOUSEEVENTF_WHEEL = 0x0800,
    KEYEVENTF_KEYUP = 0x0002,
    XBUTTON1 = 0x0001,
    XBUTTON2 = 0x0002,
    MOUSEEVENTF_XDOWN = 0x0080,
    MOUSEEVENTF_XUP = 0x0100
}

/// <summary>
///     Specifies the style of the window being created
/// </summary>
[Flags]
[Description("Specifies the style of the window being created")]
internal enum WindowStyles
{
    /// <summary>
    ///     Creates an overlapped window. An overlapped window has a title bar and a border
    /// </summary>
    WS_OVERLAPPED = 0x00000000,

    /// <summary>
    ///     Creates a pop-up window
    /// </summary>
    WS_POPUP = -2147483648,

    /// <summary>
    ///     Creates a child window. A window with this style cannot have a menu bar.
    ///     This style cannot be used with the WS_POPUP style.
    /// </summary>
    WS_CHILD = 0x40000000,

    /// <summary>
    ///     Creates a window that is initially minimized.
    ///     Same as the WS_ICONIC style.
    /// </summary>
    WS_MINIMIZE = 0x20000000,

    /// <summary>
    ///     Creates a window that is initially visible.
    /// </summary>
    WS_VISIBLE = 0x10000000,

    /// <summary>
    ///     Creates a window that is initially disabled.
    ///     A disabled window cannot receive input from the user
    /// </summary>
    WS_DISABLED = 0x08000000,

    /// <summary>
    ///     Clips child windows relative to each other; that is, when a particular child window
    ///     receives a WM_PAINT message, the WS_CLIPSIBLINGS style clips all other overlapping
    ///     child windows out of the region of the child window to be updated.
    ///     If WS_CLIPSIBLINGS is not specified and child windows overlap, it is possible,
    ///     when drawing within the client area of a child window, to draw within the client area
    ///     of a neighboring child window.
    /// </summary>
    WS_CLIPSIBLINGS = 0x04000000,

    /// <summary>
    ///     Excludes the area occupied by child windows when drawing occurs within the parent window.
    ///     This style is used when creating the parent window.
    /// </summary>
    WS_CLIPCHILDREN = 0x02000000,

    /// <summary>
    ///     Creates a window that is initially maximized.
    /// </summary>
    WS_MAXIMIZE = 0x01000000,

    /// <summary>
    ///     Creates a window that has a title bar (includes the WS_BORDER style).
    /// </summary>
    WS_CAPTION = 0x00C00000,

    /// <summary>
    ///     Creates a window that has a thin-line border.
    /// </summary>
    WS_BORDER = 0x00800000,

    /// <summary>
    ///     Creates a window that has a border of a style typically used with dialog boxes.
    ///     A window with this style cannot have a title bar.
    /// </summary>
    WS_DLGFRAME = 0x00400000,

    /// <summary>
    ///     Creates a window that has a vertical scroll bar.
    /// </summary>
    WS_VSCROLL = 0x00200000,

    /// <summary>
    ///     Creates a window that has a horizontal scroll bar.
    /// </summary>
    WS_HSCROLL = 0x00100000,

    /// <summary>
    ///     Creates a window that has a window menu on its title bar.
    ///     The WS_CAPTION style must also be specified.
    /// </summary>
    WS_SYSMENU = 0x00080000,

    /// <summary>
    ///     Creates a window that has a sizing border.
    ///     Same as the WS_SIZEBOX style.
    /// </summary>
    WS_THICKFRAME = 0x00040000,

    /// <summary>
    ///     Specifies the first control of a group of controls.
    ///     The group consists of this first control and all controls defined after it,
    ///     up to the next control with the WS_GROUP style. The first control in each group
    ///     usually has the WS_TABSTOP style so that the user can move from group to group.
    ///     The user can subsequently change the keyboard focus from one control in the group
    ///     to the next control in the group by using the direction keys.
    /// </summary>
    WS_GROUP = 0x00020000,

    /// <summary>
    ///     Specifies a control that can receive the keyboard focus when the user presses the TAB key.
    ///     Pressing the TAB key changes the keyboard focus to the next control with the
    ///     WS_TABSTOP style.
    /// </summary>
    WS_TABSTOP = 0x00010000,

    /// <summary>
    ///     Creates a window that has a minimize button. Cannot be combined with the WS_EX_CONTEXTHELP
    ///     style. The WS_SYSMENU style must also be specified.
    /// </summary>
    WS_MINIMIZEBOX = 0x00020000,

    /// <summary>
    ///     Creates a window that has a maximize button. Cannot be combined with the
    ///     WS_EX_CONTEXTHELP style. The WS_SYSMENU style must also be specified.
    /// </summary>
    WS_MAXIMIZEBOX = 0x00010000,


    WS_OVERLAPPEDWINDOW = WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU | WS_SIZEFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX,
    WS_POPUPWINDOW = WS_POPUP | WS_BORDER | WS_SYSMENU,
    WS_SIZEFRAME = 0x40000
}

/// <summary>
///     Common window styles
/// </summary>
[Description("Common window styles")]
internal enum CommonWindowStyles
{
    /// <summary>
    ///     Creates an overlapped window. An overlapped window has a title bar and a border. Same as the WS_OVERLAPPED style.
    /// </summary>
    WS_TILED = WindowStyles.WS_OVERLAPPED,

    /// <summary>
    ///     Creates a window that is initially minimized. Same as the WS_MINIMIZE style.
    /// </summary>
    WS_ICONIC = WindowStyles.WS_MINIMIZE,

    /// <summary>
    ///     Creates a window that has a sizing border. Same as the WS_THICKFRAME style.
    /// </summary>
    WS_SIZEBOX = WindowStyles.WS_THICKFRAME,

    /// <summary>
    ///     Creates an overlapped window with the WS_OVERLAPPED, WS_CAPTION, WS_SYSMENU, WS_THICKFRAME, WS_MINIMIZEBOX, and
    ///     WS_MAXIMIZEBOX styles. Same as the WS_TILEDWINDOW style.
    /// </summary>
    WS_OVERLAPPEDWINDOW = WindowStyles.WS_OVERLAPPED | WindowStyles.WS_CAPTION | WindowStyles.WS_SYSMENU |
                          WindowStyles.WS_THICKFRAME | WindowStyles.WS_MINIMIZEBOX | WindowStyles.WS_MAXIMIZEBOX,

    /// <summary>
    ///     Creates an overlapped window with the WS_OVERLAPPED, WS_CAPTION, WS_SYSMENU, WS_THICKFRAME, WS_MINIMIZEBOX, and
    ///     WS_MAXIMIZEBOX styles. Same as the WS_OVERLAPPEDWINDOW style.
    /// </summary>
    WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW,

    /// <summary>
    ///     Creates a pop-up window with WS_BORDER, WS_POPUP, and WS_SYSMENU styles. The WS_CAPTION and WS_POPUPWINDOW styles
    ///     must be combined to make the window menu visible.
    /// </summary>
    WS_POPUPWINDOW = WindowStyles.WS_POPUP |
                     WindowStyles.WS_BORDER |
                     WindowStyles.WS_SYSMENU,

    /// <summary>
    ///     Same as the WS_CHILD style.
    /// </summary>
    WS_CHILDWINDOW = WindowStyles.WS_CHILD
}

[Flags]
internal enum SetWindowPosFlags : uint
{
    /// <summary>
    ///     If the calling thread and the thread that owns the window are attached to different input queues,
    ///     the system posts the request to the thread that owns the window. This prevents the calling thread from
    ///     blocking its execution while other threads process the request.
    /// </summary>
    /// <remarks>SWP_ASYNCWINDOWPOS</remarks>
    AsynchronousWindowPosition = 0x4000,

    /// <summary>Prevents generation of the WM_SYNCPAINT message.</summary>
    /// <remarks>SWP_DEFERERASE</remarks>
    DeferErase = 0x2000,

    /// <summary>Draws a frame (defined in the window's class description) around the window.</summary>
    /// <remarks>SWP_DRAWFRAME</remarks>
    DrawFrame = 0x0020,

    /// <summary>
    ///     Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to
    ///     the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE
    ///     is sent only when the window's size is being changed.
    /// </summary>
    /// <remarks>SWP_FRAMECHANGED</remarks>
    FrameChanged = 0x0020,

    /// <summary>Hides the window.</summary>
    /// <remarks>SWP_HIDEWINDOW</remarks>
    HideWindow = 0x0080,

    /// <summary>
    ///     Does not activate the window. If this flag is not set, the window is activated and moved to the
    ///     top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter
    ///     parameter).
    /// </summary>
    /// <remarks>SWP_NOACTIVATE</remarks>
    DoNotActivate = 0x0010,

    /// <summary>
    ///     Discards the entire contents of the client area. If this flag is not specified, the valid
    ///     contents of the client area are saved and copied back into the client area after the window is sized or
    ///     repositioned.
    /// </summary>
    /// <remarks>SWP_NOCOPYBITS</remarks>
    DoNotCopyBits = 0x0100,

    /// <summary>Retains the current position (ignores X and Y parameters).</summary>
    /// <remarks>SWP_NOMOVE</remarks>
    IgnoreMove = 0x0002,

    /// <summary>Does not change the owner window's position in the Z order.</summary>
    /// <remarks>SWP_NOOWNERZORDER</remarks>
    DoNotChangeOwnerZOrder = 0x0200,

    /// <summary>
    ///     Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to
    ///     the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent
    ///     window uncovered as a result of the window being moved. When this flag is set, the application must
    ///     explicitly invalidate or redraw any parts of the window and parent window that need redrawing.
    /// </summary>
    /// <remarks>SWP_NOREDRAW</remarks>
    DoNotRedraw = 0x0008,

    /// <summary>Same as the SWP_NOOWNERZORDER flag.</summary>
    /// <remarks>SWP_NOREPOSITION</remarks>
    DoNotReposition = 0x0200,

    /// <summary>Prevents the window from receiving the WM_WINDOWPOSCHANGING message.</summary>
    /// <remarks>SWP_NOSENDCHANGING</remarks>
    DoNotSendChangingEvent = 0x0400,

    /// <summary>Retains the current size (ignores the cx and cy parameters).</summary>
    /// <remarks>SWP_NOSIZE</remarks>
    IgnoreResize = 0x0001,

    /// <summary>Retains the current Z order (ignores the hWndInsertAfter parameter).</summary>
    /// <remarks>SWP_NOZORDER</remarks>
    IgnoreZOrder = 0x0004,

    /// <summary>Displays the window.</summary>
    /// <remarks>SWP_SHOWWINDOW</remarks>
    ShowWindow = 0x0040,

    SWP_NOSIZE = 1,
    SWP_NOMOVE = 2,
    SWP_NOZORDER = 4,
    SWP_NOREDRAW = 8,
    SWP_NOACTIVATE = 0x10,
    SWP_FRAMECHANGED = 0x20,
    SWP_SHOWWINDOW = 0x40,
    SWP_HIDEWINDOW = 0x80,
    SWP_NOCOPYBITS = 0x100,
    SWP_NOOWNERZORDER = 0x200,
    SWP_NOSENDCHANGING = 0x400
}

/// <summary>
///     Specifies the extended style of the window
/// </summary>
[Flags]
[Description("Specifies the extended style of the window")]
internal enum ExtendedWindowStyles
{
    /// <summary>
    ///     Creates a window that has a double border; the window can, optionally,
    ///     be created with a title bar by specifying the WS_CAPTION style in the dwStyle parameter.
    /// </summary>
    WS_EX_DLGMODALFRAME = 0x00000001,

    /// <summary>
    ///     Specifies that a child window created with this style does not send
    ///     the WM_PARENTNOTIFY message to its parent window when it is created or destroyed.
    /// </summary>
    WS_EX_NOPARENTNOTIFY = 0x00000004,

    /// <summary>
    ///     Specifies that a window created with this style should be placed above all nontopmost
    ///     windows and stay above them even when the window is deactivated
    /// </summary>
    WS_EX_TOPMOST = 0x00000008,

    /// <summary>
    ///     Windows that can accept dragged objects must be created with this style so that
    ///     Windows can determine that the window will accept objects and can change the drag/drop
    ///     cursor as the user drags an object over the window.
    /// </summary>
    WS_EX_ACCEPTFILES = 0x00000010,

    /// <summary>
    ///     The WS_EX_TRANSPARENT style makes a window transparent; that is, the window can be seen through,
    ///     and anything under the window is still visible. Transparent windows are not transparent
    ///     to mouse or keyboard events. A transparent window receives paint messages when anything
    ///     under it changes. Transparent windows are useful for drawing drag handles on top of other
    ///     windows or for implementing "hot-spot" areas without having to hit test because the transparent
    ///     window receives click messages.
    /// </summary>
    WS_EX_TRANSPARENT = 0x00000020,

    WDA_NONE = 0x00000000,
    WDA_MONITOR = 0x00000001,
    WDA_EXCLUDEFROMCAPTURE = 0x00000011,

    /// <summary>
    ///     Creates an MDI child window.
    /// </summary>
    WS_EX_MDICHILD = 0x00000040,

    /// <summary>
    ///     Creates a tool window, which is a window intended to be used as a floating toolbar.
    ///     A tool window has a title bar that is shorter than a normal title bar, and the window title
    ///     is drawn using a smaller font. A tool window does not appear in the task bar or in the window
    ///     that appears when the user presses ALT+TAB.
    /// </summary>
    WS_EX_TOOLWINDOW = 0x00000080,

    /// <summary>
    ///     Specifies that a window has a border with a raised edge.
    /// </summary>
    WS_EX_WINDOWEDGE = 0x00000100,

    /// <summary>
    ///     Specifies that a window has a 3D look — that is, a border with a sunken edge.
    /// </summary>
    WS_EX_CLIENTEDGE = 0x00000200,

    /// <summary>
    ///     Includes a question mark in the title bar of the window.
    ///     When the user clicks the question mark, the cursor changes to a question mark with a pointer.
    ///     If the user then clicks a child window, the child receives a WM_HELP message.
    /// </summary>
    WS_EX_CONTEXTHELP = 0x00000400,

    /// <summary>
    ///     Gives a window generic right-aligned properties. This depends on the window class.
    /// </summary>
    WS_EX_RIGHT = 0x00001000,

    /// <summary>
    ///     Gives window generic left-aligned properties. This is the default.
    /// </summary>
    WS_EX_LEFT = 0x00000000,

    /// <summary>
    ///     Displays the window text using right-to-left reading order properties.
    /// </summary>
    WS_EX_RTLREADING = 0x00002000,

    /// <summary>
    ///     Displays the window text using left-to-right reading order properties. This is the default.
    /// </summary>
    WS_EX_LTRREADING = 0x00000000,

    /// <summary>
    ///     Places a vertical scroll bar to the left of the client area.
    /// </summary>
    WS_EX_LEFTSCROLLBAR = 0x00004000,

    /// <summary>
    ///     Places a vertical scroll bar (if present) to the right of the client area. This is the default.
    /// </summary>
    WS_EX_RIGHTSCROLLBAR = 0x00000000,

    /// <summary>
    ///     Allows the user to navigate among the child windows of the window by using the TAB key.
    /// </summary>
    WS_EX_CONTROLPARENT = 0x00010000,

    /// <summary>
    ///     Creates a window with a three-dimensional border style intended to be used for items that
    ///     do not accept user input.
    /// </summary>
    WS_EX_STATICEDGE = 0x00020000,

    /// <summary>
    ///     Forces a top-level window onto the taskbar when the window is visible.
    /// </summary>
    WS_EX_APPWINDOW = 0x00040000,

    /// <summary>
    ///     Creates a layered window. Note that this cannot be used for child windows
    /// </summary>
    WS_EX_LAYERED = 0x00080000,

    /// <summary>
    ///     A window created with this style does not pass its window layout to its child windows.
    /// </summary>
    WS_EX_NOINHERITLAYOUT = 0x00100000,

    /// <summary>
    ///     Creates a window whose horizontal origin is on the right edge.
    ///     Increasing horizontal values advance to the left.
    /// </summary>
    WS_EX_LAYOUTRTL = 0x00400000,

    /// <summary>
    ///     Paints all descendants of a window in bottom-to-top painting order using double-buffering.
    /// </summary>
    WS_EX_COMPOSITED = 0x02000000,

    /// <summary>
    ///     A top-level window created with this style does not become the foreground window when the user
    ///     clicks it. The system does not bring this window to the foreground when the user minimizes
    ///     or closes the foreground window.
    /// </summary>
    WS_EX_NOACTIVATE = 0x08000000
}

/// <summary>
///     Common extended window styles
/// </summary>
[Description("Common extended window styles")]
internal enum CommonExtendedWindowStyles
{
    /// <summary>
    ///     Combines the WS_EX_CLIENTEDGE and WS_EX_WINDOWEDGE styles.
    /// </summary>
    WS_EX_OVERLAPPEDWINDOW = ExtendedWindowStyles.WS_EX_WINDOWEDGE |
                             ExtendedWindowStyles.WS_EX_CLIENTEDGE,

    /// <summary>
    ///     Combines the WS_EX_WINDOWEDGE, WS_EX_TOOLWINDOW, and WS_EX_TOPMOST styles.
    /// </summary>
    WS_EX_PALETTEWINDOW = ExtendedWindowStyles.WS_EX_WINDOWEDGE |
                          ExtendedWindowStyles.WS_EX_TOOLWINDOW |
                          ExtendedWindowStyles.WS_EX_TOPMOST
}

/// <summary>
///     Layered window flags
/// </summary>
[Flags]
[Description("Layered window flags")]
internal enum LayeredWindowAttributeFlags
{
    /// <summary>
    ///     Use key as a transparency color
    /// </summary>
    LWA_COLORKEY = 0x00000001,

    /// <summary>
    ///     Use Alpha to determine the opacity of the layered window.
    /// </summary>
    LWA_ALPHA = 0x00000002
}

[Flags]
internal enum LayeredWindowUpdateFlags
{
    ULW_COLORKEY = 0x00000001,
    ULW_ALPHA = 0x00000002,
    ULW_OPAQUE = 0x00000004
}

[Flags]
internal enum BlendOperations : byte
{
    AC_SRC_OVER = 0x00,
    AC_SRC_ALPHA = 0x01
}

internal enum ShowWindowStyles : short
{
    SW_HIDE = 0,
    SW_SHOWNORMAL = 1,
    SW_NORMAL = 1,
    SW_SHOWMINIMIZED = 2,
    SW_SHOWMAXIMIZED = 3,
    SW_MAXIMIZE = 3,
    SW_SHOWNOACTIVATE = 4,
    SW_SHOW = 5,
    SW_MINIMIZE = 6,
    SW_SHOWMINNOACTIVE = 7,
    SW_SHOWNA = 8,
    SW_RESTORE = 9,
    SW_SHOWDEFAULT = 10,
    SW_FORCEMINIMIZE = 11,
    SW_MAX = 11
}

internal enum WindowMessage
{
    WM_CREATE = 0x0001,
    WM_DESTROY = 0x0002,
    WM_PAINT = 0x000F,
    WM_CLOSE = 0x0010,
    WM_QUERYENDSESSION = 0x0011,
    WM_QUIT = 0x0012,
    WM_ENDSESSION = 0x0016,
    WM_SETCURSOR = 0x0020,
    WM_MOVE = 0x0003,
    WM_SIZE = 0x0005,
    WM_MOUSEMOVE = 0x0200,
    WM_NCMOUSEMOVE = 0x00A0,
    WM_KEYDOWN = 0x0100,
    WM_SYSKEYDOWN = 0x0104,
    WM_KEYUP = 0x0101,
    WM_CHAR = 0x0102,
    WM_SYSCHAR = 0x0106,
    WM_LBUTTONDOWN = 0x0201,
    WM_LBUTTONUP = 0x0202,
    WM_LBUTTONDBLCLK = 0x0203,
    WM_RBUTTONDOWN = 0x0204,
    WM_RBUTTONUP = 0x0205,
    WM_RBUTTONDBLCLK = 0x0206,
    WM_MBUTTONDOWN = 0x0207,
    WM_MBUTTONUP = 0x0208,
    WM_MBUTTONDBLCLK = 0x0209,
    WM_MOUSEWHEEL = 0x020A,
    WM_MOUSEHOVER = 0x02A1,
    WM_MOUSELEAVE = 0x02A3,
    WM_NCLBUTTONDOWN = 0x00A1,
    WM_NCLBUTTONUP = 0x00A2,
    WM_NCLBUTTONDBLCLK = 0x00A3,
    WM_NCRBUTTONDOWN = 0x00A4,
    WM_NCRBUTTONUP = 0x00A5,
    WM_NCRBUTTONDBLCLK = 0x00A6,
    WM_NCMBUTTONDOWN = 0x00A7,
    WM_NCMBUTTONUP = 0x00A8,
    WM_NCMBUTTONDBLCLK = 0x00A9,
    WM_NCXBUTTONDOWN = 0x00AB,
    WM_NCXBUTTONUP = 0x00AC,
    WM_GETDLGCODE = 0x0087,
    WM_NCHITTEST = 0x0084,
    WM_WINDOWPOSCHANGING = 0x0046,
    WM_WINDOWPOSCHANGED = 0x0047,
    WM_KILLTIMER = 0x402,
    WM_TIMER = 0x113,
    WM_NCPAINT = 0x85,
    WM_ERASEBKGND = 20,
    WM_DROPFILES = 0x233,
    WM_MOUSEACTIVATE = 0x0021,
    WM_ACTIVATE = 0x0006,
    WM_ACTIVATEAPP = 0x001C,
    WM_KILLFOCUS = 8
}

internal enum WindowAffinity : uint
{
    WDA_NONE = 0x00000000,
    WDA_MONITOR = 0x00000001,
    WDA_EXCLUDEFROMCAPTURE = 0x00000011
}

internal struct NativeStruct
{
    public const string WC_MAGNIFIER = "Magnifier";
    public const int MONITOR_DEFAULTTOPRIMARY = 0x00000001;
    public const int MONITOR_DEFAULTTONEAREST = 0x00000002;
    public const int GW_HWNDPREV = 3;
    public static IntPtr HWND_TOP = new(0);
    public static IntPtr HWND_TOPMOST = new(-1);
    public static IntPtr HWND_NOTOPMOST = new(-2);
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int USER_TIMER_MINIMUM = 0x0000000A;
    public const int SM_ARRANGE = 0x38;
    public const int SM_CLEANBOOT = 0x43;
    public const int SM_CMONITORS = 80;
    public const int SM_CMOUSEBUTTONS = 0x2b;
    public const int SM_CXBORDER = 5;
    public const int SM_CXCURSOR = 13;
    public const int SM_CXDOUBLECLK = 0x24;
    public const int SM_CXDRAG = 0x44;
    public const int SM_CXEDGE = 0x2d;
    public const int SM_CXFIXEDFRAME = 7;
    public const int SM_CXFOCUSBORDER = 0x53;
    public const int SM_CXFRAME = 0x20;
    public const int SM_CXHSCROLL = 0x15;
    public const int SM_CXHTHUMB = 10;
    public const int SM_CXICON = 11;
    public const int SM_CXICONSPACING = 0x26;
    public const int SM_CXMAXIMIZED = 0x3d;
    public const int SM_CXMAXTRACK = 0x3b;
    public const int SM_CXMENUCHECK = 0x47;
    public const int SM_CXMENUSIZE = 0x36;
    public const int SM_CXMIN = 0x1c;
    public const int SM_CXMINIMIZED = 0x39;
    public const int SM_CXMINSPACING = 0x2f;
    public const int SM_CXMINTRACK = 0x22;
    public const int SM_CXSCREEN = 0;
    public const int SM_CXSIZE = 30;
    public const int SM_CXSIZEFRAME = 0x20;
    public const int SM_CXSMICON = 0x31;
    public const int SM_CXSMSIZE = 0x34;
    public const int SM_CXVIRTUALSCREEN = 0x4e;
    public const int SM_CXVSCROLL = 2;
    public const int SM_CYBORDER = 6;
    public const int SM_CYCAPTION = 4;
    public const int SM_CYCURSOR = 14;
    public const int SM_CYDOUBLECLK = 0x25;
    public const int SM_CYDRAG = 0x45;
    public const int SM_CYEDGE = 0x2e;
    public const int SM_CYFIXEDFRAME = 8;
    public const int SM_CYFOCUSBORDER = 0x54;
    public const int SM_CYFRAME = 0x21;
    public const int SM_CYHSCROLL = 3;
    public const int SM_CYICON = 12;
    public const int SM_CYICONSPACING = 0x27;
    public const int SM_CYKANJIWINDOW = 0x12;
    public const int SM_CYMAXIMIZED = 0x3e;
    public const int SM_CYMAXTRACK = 60;
    public const int SM_CYMENU = 15;
    public const int SM_CYMENUCHECK = 0x48;
    public const int SM_CYMENUSIZE = 0x37;
    public const int SM_CYMIN = 0x1d;
    public const int SM_CYMINIMIZED = 0x3a;
    public const int SM_CYMINSPACING = 0x30;
    public const int SM_CYMINTRACK = 0x23;
    public const int SM_CYSCREEN = 1;
    public const int SM_CYSIZE = 0x1f;
    public const int SM_CYSIZEFRAME = 0x21;
    public const int SM_CYSMCAPTION = 0x33;
    public const int SM_CYSMICON = 50;
    public const int SM_CYSMSIZE = 0x35;
    public const int SM_CYVIRTUALSCREEN = 0x4f;
    public const int SM_CYVSCROLL = 20;
    public const int SM_CYVTHUMB = 9;
    public const int SM_DBCSENABLED = 0x2a;
    public const int SM_DEBUG = 0x16;
    public const int SM_MENUDROPALIGNMENT = 40;
    public const int SM_MIDEASTENABLED = 0x4a;
    public const int SM_MOUSEPRESENT = 0x13;
    public const int SM_MOUSEWHEELPRESENT = 0x4b;
    public const int SM_NETWORK = 0x3f;
    public const int SM_PENWINDOWS = 0x29;
    public const int SM_REMOTESESSION = 0x1000;
    public const int SM_SAMEDISPLAYFORMAT = 0x51;
    public const int SM_SECURE = 0x2c;
    public const int SM_SHOWSOUNDS = 70;
    public const int SM_SWAPBUTTON = 0x17;
    public const int SM_XVIRTUALSCREEN = 0x4c;
    public const int SM_YVIRTUALSCREEN = 0x4d;
}