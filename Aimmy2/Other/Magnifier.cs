using WinformsReplacement;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Aimmy2.WinformsReplacement;

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

        initialized = NativeMethods.MagInitialize();
        if (initialized)
        {
            SetupMagnifier();
            timer.Interval = NativeMethods.USER_TIMER_MINIMUM;
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
            NativeMethods.GetClientRect(GetHandle(), ref magWindowRect);
            // Resize the control to fill the window.
            NativeMethods.SetWindowPos(hwndMag, IntPtr.Zero,
                magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, 0);
        }
    }

    public virtual void UpdateMagnifier()
    {
        if ((!initialized) || (hwndMag == IntPtr.Zero))
            return;

        POINT mousePoint = new POINT();
        RECT sourceRect = new RECT();

        NativeMethods.GetCursorPos(ref mousePoint);

        int width = (int)((magWindowRect.right - magWindowRect.left) / magnification);
        int height = (int)((magWindowRect.bottom - magWindowRect.top) / magnification);

        sourceRect.left = mousePoint.x - width / 2;
        sourceRect.top = mousePoint.y - height / 2;


        // Don't scroll outside desktop area.
        if (sourceRect.left < 0)
        {
            sourceRect.left = 0;
        }
        if (sourceRect.left > NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) - width)
        {
            sourceRect.left = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN) - width;
        }
        sourceRect.right = sourceRect.left + width;

        if (sourceRect.top < 0)
        {
            sourceRect.top = 0;
        }
        if (sourceRect.top > NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN) - height)
        {
            sourceRect.top = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN) - height;
        }
        sourceRect.bottom = sourceRect.top + height;

        if (this.form == null)
        {
            timer.Enabled = false;
            return;
        }



        // Set the source rectangle for the magnifier control.
        NativeMethods.MagSetWindowSource(hwndMag, sourceRect);

        // Reclaim topmost status, to prevent unmagnified menus from remaining in view. 
        NativeMethods.SetWindowPos(GetHandle(), NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            (int)SetWindowPosFlags.SWP_NOACTIVATE | (int)SetWindowPosFlags.SWP_NOMOVE | (int)SetWindowPosFlags.SWP_NOSIZE);

        // Force redraw.
        NativeMethods.InvalidateRect(hwndMag, IntPtr.Zero, true);
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
                NativeMethods.MagSetWindowTransform(hwndMag, ref matrix);
            }
        }
    }

    protected void SetupMagnifier()
    {
        if (!initialized)
            return;

        IntPtr hInst;

        hInst = NativeMethods.GetModuleHandle(null);

        // Make the window opaque.
        //form.Background = System.Windows.Media.Brushes.Transparent;
        form.Opacity = 255;

        // Create a magnifier control that fills the client area.
        NativeMethods.GetClientRect(GetHandle(), ref magWindowRect);
        hwndMag = NativeMethods.CreateWindow((int)ExtendedWindowStyles.WS_EX_CLIENTEDGE, NativeMethods.WC_MAGNIFIER,
            "MagnifierWindow", (int)WindowStyles.WS_CHILD | (int)MagnifierStyle.MS_SHOWMAGNIFIEDCURSOR |
            (int)WindowStyles.WS_VISIBLE,
            magWindowRect.left, magWindowRect.top, magWindowRect.right, magWindowRect.bottom, GetHandle(), IntPtr.Zero, hInst, IntPtr.Zero);

        if (hwndMag == IntPtr.Zero)
        {
            return;
        }

        // Set the magnification factor.
        Transformation matrix = new Transformation(magnification);
        NativeMethods.MagSetWindowTransform(hwndMag, ref matrix);
    }

    protected void RemoveMagnifier()
    {
        if (initialized)
            NativeMethods.MagUninitialize();
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


