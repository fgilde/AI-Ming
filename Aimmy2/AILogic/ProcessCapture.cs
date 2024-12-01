using System.ComponentModel;
using Aimmy2.AILogic.Contracts;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Aimmy2.Class.Native;
using Aimmy2.Config;
using Nextended.Core;
using Visuality;

namespace Aimmy2.AILogic;

public class ProcessCapture : ICapture
{
    private readonly int _processId;
    private Rectangle _captureArea;
    private bool _isTopMostSet = false;

    public ProcessCapture(Process? process) : this(process?.Id ?? 0)
    { }

    public ProcessCapture(int processId)
    {
        if (processId == 0)
        {
            throw new ArgumentException("Process not running");
        }
        _processId = processId;
        Screen = GetProcessTargetScreen();
        CaptureArea = GetCaptureArea();
    }

    public Rectangle CaptureArea
    {
        get => _captureArea;
        private set => SetField(ref _captureArea, value);
    }

    public Bitmap Capture(Rectangle detectionBox)
    {
        IntPtr handle = GetProcessWindowHandle();

        EnsureForegroundIf(handle);

        Screen = GetProcessTargetScreen(handle);
        CaptureArea = GetCaptureArea();

        // Get window dimensions
        var windowRect = GetWindowRectangle(handle);

        // Adjust detectionBox to window dimensions
        detectionBox.X += windowRect.Left;
        detectionBox.Y += windowRect.Top;

        if (detectionBox.Height <= 0)
            detectionBox.Height = windowRect.Bottom - windowRect.Top;
        if (detectionBox.Width <= 0)
            detectionBox.Width = windowRect.Right - windowRect.Left;

        LastCapture = new Bitmap(detectionBox.Width, detectionBox.Height, PixelFormat.Format32bppArgb);
        using Graphics graphics = Graphics.FromImage(LastCapture);
        graphics.CopyFromScreen(detectionBox.Left, detectionBox.Top, 0, 0, detectionBox.Size, CopyPixelOperation.SourceCopy);
        return LastCapture;
    }

    public Bitmap LastCapture { get; private set; }

    public Task OnPause()
    {
        Check.TryCatch<Exception>(() => RemoveTopMostIf());
        return Task.CompletedTask;
    }

    public Task OnResume()
    {
        return Task.CompletedTask;
    }

    private void EnsureForegroundIf(IntPtr handle)
    {
        if (AppConfig.Current?.ToggleState?.EnsureCaptureForeground ?? false)
        {
            SetTopMostIf(handle);
            FOV.Instance?.SetTopMost();
            DetectedPlayerWindow.Instance?.SetTopMost();
            //if (AppConfig.Current?.ToggleState?.UITopMost ?? false)
            //    MainWindow.Instance?.SetTopMost();
        }
        else
        {
            RemoveTopMostIf(handle);
        }
    }

    private bool SetTopMostIf(IntPtr? handle = null)
    {
        handle ??= GetProcessWindowHandle();
        if (!_isTopMostSet && !NativeAPIMethods.IsWindowTopMost(handle.Value))
        {
            
            NativeAPIMethods.SetTopMost(handle.Value);

            _isTopMostSet = true;
            return true;
        }
        return false;
    }

    private bool RemoveTopMostIf(IntPtr? handle = null)
    {
        handle ??= GetProcessWindowHandle();
        if (_isTopMostSet)
        {
            NativeAPIMethods.RemoveTopMost(handle.Value);

            _isTopMostSet = false;
            return true;
        }
        return false;
    }


    public Rectangle GetCaptureArea()
    {
        IntPtr handle = GetProcessWindowHandle();

        // Get window dimensions
        var windowRect = GetWindowRectangle(handle);

        return new Rectangle(windowRect.Left, windowRect.Top, windowRect.Right - windowRect.Left, windowRect.Bottom - windowRect.Top);
    }

    private Screen GetProcessTargetScreen(IntPtr? handle = null)
    {
        handle ??= GetProcessWindowHandle();
        return Screen.FromHandle(handle.Value);
    }

    private IntPtr GetProcessWindowHandle()
    {
        var process = Process.GetProcessById(_processId);
        return process.MainWindowHandle;
    }

    // New helper method to avoid duplication
    private Rectangle GetWindowRectangle(IntPtr handle)
    {
        return NativeAPIMethods.GetWindowRectangle(handle).ToRectangle();
    }

    public Screen Screen { get; private set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public void Dispose()
    {
        Check.TryCatch<Exception>(() =>
        {
            RemoveTopMostIf();
        });

    }
}
