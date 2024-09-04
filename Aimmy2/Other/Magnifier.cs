using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Aimmy2.Class.Native;
using Aimmy2.Extensions;

public class Magnifier : IDisposable
{
    private Window form;
    private IntPtr hwndMag;
    private float magnification;
    private bool initialized;
    private RECT magWindowRect = new RECT();
    private System.Windows.Forms.Timer timer;

    public Magnifier(Window form)
    {
        if (form == null)
            throw new ArgumentNullException("form");

        magnification = 2.0f;
        this.form = form;
        this.form.SizeChanged += form_Resize;
        this.form.Closing += form_FormClosing;

        timer = new System.Windows.Forms.Timer();
        timer.Tick += new EventHandler(timer_Tick);

        initialized = NativeAPIMethods.MagInitialize();
        if (initialized)
        {
            SetupMagnifier();
            timer.Interval = NativeStruct.USER_TIMER_MINIMUM;
            timer.Enabled = true;
        }
    }

    private IntPtr? handle;
    IntPtr GetHandle()
    {
        if(handle == null)
            handle = new WindowInteropHelper(form).Handle;
        return handle.Value;
    }

    void form_FormClosing(object? sender, CancelEventArgs cancelEventArgs)
    {
        if(timer != null)
            timer.Enabled = false;
    }

    void timer_Tick(object sender, EventArgs e)
    {
        UpdateMagnifier();
    }

    void form_Resize(object sender, EventArgs e)
    {
        ResizeMagnifier();
    }

    ~Magnifier()
    {
        Dispose(false);
    }

    protected virtual void ResizeMagnifier()
    {
        if (initialized && (hwndMag != IntPtr.Zero))
        {
            NativeAPIMethods.GetClientRect(GetHandle(), ref magWindowRect);
            // Resize the control to fill the window.
            NativeAPIMethods.SetWindowPos(hwndMag, IntPtr.Zero,
                magWindowRect.Left, magWindowRect.Top, magWindowRect.Right, magWindowRect.Bottom, 0);

            //
        }
    }

    public virtual void UpdateMagnifier()
    {
        if ((!initialized) || (hwndMag == IntPtr.Zero))
            return;

        POINT mousePoint = new POINT();
        RECT sourceRect = new RECT();

        NativeAPIMethods.GetCursorPos(out mousePoint);

        int width = (int)((magWindowRect.Right - magWindowRect.Left) / magnification);
        int height = (int)((magWindowRect.Bottom - magWindowRect.Top) / magnification);

        sourceRect.Left = mousePoint.X - width / 2;
        sourceRect.Top = mousePoint.Y - height / 2;


        // Don't scroll outside desktop area.
        if (sourceRect.Left < 0)
        {
            sourceRect.Left = 0;
        }
        if (sourceRect.Left > NativeAPIMethods.GetSystemMetrics(NativeStruct.SM_CXSCREEN) - width)
        {
            sourceRect.Left = NativeAPIMethods.GetSystemMetrics(NativeStruct.SM_CXSCREEN) - width;
        }
        sourceRect.Right = sourceRect.Left + width;

        if (sourceRect.Top < 0)
        {
            sourceRect.Top = 0;
        }
        if (sourceRect.Top > NativeAPIMethods.GetSystemMetrics(NativeStruct.SM_CYSCREEN) - height)
        {
            sourceRect.Top = NativeAPIMethods.GetSystemMetrics(NativeStruct.SM_CYSCREEN) - height;
        }
        sourceRect.Bottom = sourceRect.Top + height;

        if (this.form == null)
        {
            timer.Enabled = false;
            return;
        }

        // Set the source rectangle for the magnifier control.
        NativeAPIMethods.MagSetWindowSource(hwndMag, sourceRect);

        // Reclaim topmost status, to prevent unmagnified menus from remaining in view. 
        NativeAPIMethods.SetWindowPos(GetHandle(), NativeStruct.HWND_TOPMOST, 0, 0, 0, 0,
            (int)SetWindowPosFlags.SWP_NOACTIVATE | (int)SetWindowPosFlags.SWP_NOMOVE | (int)SetWindowPosFlags.SWP_NOSIZE);

        // Force redraw.
        NativeAPIMethods.InvalidateRect(hwndMag, IntPtr.Zero, true);
    }

    public float Magnification
    {
        get { return magnification; }
        set
        {
            if (magnification != value)
            {
                magnification = value;
                // Set the magnification factor.
                Transformation matrix = new Transformation(magnification);
                NativeAPIMethods.MagSetWindowTransform(hwndMag, ref matrix);
            }
        }
    }

    protected void SetupMagnifier()
    {
        if (!initialized)
            return;

        var hInst = NativeAPIMethods.GetModuleHandle(null);

       
        form.Opacity = 255;

        // Create a magnifier control that fills the client area.
        NativeAPIMethods.GetClientRect(GetHandle(), ref magWindowRect);
        hwndMag = NativeAPIMethods.CreateWindow(
             (int)(ExtendedWindowStyles.WS_EX_CLIENTEDGE | ExtendedWindowStyles.WS_EX_TRANSPARENT | ExtendedWindowStyles.WS_EX_TOPMOST | ExtendedWindowStyles.WDA_EXCLUDEFROMCAPTURE), 
            NativeStruct.WC_MAGNIFIER,
            "MagnifierWindow", (int)WindowStyles.WS_CHILD 
                               //| (int)MagnifierStyle.MS_SHOWMAGNIFIEDCURSOR
                               | (int)WindowStyles.WS_VISIBLE
                               ,  
            magWindowRect.Left, magWindowRect.Top, magWindowRect.Right, magWindowRect.Bottom, GetHandle(), IntPtr.Zero, hInst, IntPtr.Zero);

        if (hwndMag == IntPtr.Zero)
        {
            return;
        }
        // Set the magnification factor.
        Transformation matrix = new Transformation(magnification);
        NativeAPIMethods.MagSetWindowTransform(hwndMag, ref matrix);
        NativeAPIMethods.HideForCapture(hwndMag);

    }

    protected void RemoveMagnifier()
    {
        if (initialized)
            NativeAPIMethods.MagUninitialize();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (timer != null)
        {
            timer.Enabled = false;
            if (disposing)
                timer.Dispose();
        }

        timer = null;
        form.SizeChanged -= form_Resize;
        RemoveMagnifier();
    }

    #region IDisposable Members

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}


