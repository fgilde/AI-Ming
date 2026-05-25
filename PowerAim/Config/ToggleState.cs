namespace PowerAim.Config;

public class ToggleState : BaseSettings
{
    private bool _hideUIFromCapture = true;

    public bool AutoPlay
    {
        get;
        set => SetField(ref field, value);
    }

    /// <summary>Show the topmost <see cref="Visuality.DebugOverlay"/> with live stats.</summary>
    public bool ShowDebugOverlay
    {
        get;
        set
        {
            if (SetField(ref field, value))
            {
                // Toggle the overlay on/off. Dispatched to UI thread because the WPF Window
                // constructor needs to run there.
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    PowerAim.Visuality.DebugOverlay.ShowOrHide(value)));
            }
        }
    }

    /// <summary>Show the topmost <see cref="Visuality.CrosshairOverlay"/>.</summary>
    public bool ShowCrosshairOverlay
    {
        get;
        set
        {
            if (SetField(ref field, value))
            {
                System.Windows.Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    PowerAim.Visuality.CrosshairOverlay.ShowOrHide(value)));
            }
        }
    }

    public bool EnsureCaptureForeground
    {
        get;
        set => SetField(ref field, value);
    }

    public bool ShowCapturedArea
    {
        get;
        set
        {
            if (SetField(ref field, value))
                AppConfig.Current?.ColorState?.OnPropertyChanged(nameof(ColorState.ActiveCapturedAreaBorderBrush));
        }
    } = true;

    public bool AutoHideController
    {
        get;
        set => SetField(ref field, value);
    }

    public bool GlobalActive
    {
        get;
        set => SetField(ref field, value);
    } = false;

    public bool AimAssist
    {
        get;
        set => SetField(ref field, value);
    }

    public bool Predictions
    {
        get;
        set => SetField(ref field, value);
    }

    public bool EMASmoothening
    {
        get;
        set => SetField(ref field, value);
    }

    public bool EnableGunSwitchingKeybind
    {
        get;
        set => SetField(ref field, value);
    }

    public bool AutoTrigger
    {
        get;
        set => SetField(ref field, value);
    } = true;

    /// <summary>
    ///     When true, the aim/move pipeline drives the virtual Xbox controller's right-stick
    ///     instead of synthesising mouse motion. Only effective when <see cref="PowerAim.InputLogic.GamepadManager.CanSend"/>
    ///     is true (i.e. the user has set up the ViGEm bus and a sender is connected). Reflected
    ///     in the AimConfig UI with auto-gating so the toggle stays disabled until the user has
    ///     a working gamepad sender configured.
    /// </summary>
    public bool UseControllerForAim
    {
        get;
        set => SetField(ref field, value);
    } = false;

    /// <summary>
    ///     Master switch for the controller/keyboard mapping engine. When false, the engine
    ///     resolves no profile (no input is read or synthesized). Bound to a toggle on the
    ///     Mapping page that supports a global hotkey — so the user can flip mapping on/off
    ///     mid-game without alt-tabbing to PowerAim.
    /// </summary>
    public bool MappingActive
    {
        get;
        set => SetField(ref field, value);
    } = false;

    public bool AntiRecoil
    {
        get;
        set => SetField(ref field, value);
    }

    public bool FOV
    {
        get;
        set => SetField(ref field, value);
    }

    public bool DynamicFOV
    {
        get;
        set => SetField(ref field, value);
    }

    public bool Masking
    {
        get;
        set => SetField(ref field, value);
    }

    public bool ShowDetectedPlayer
    {
        get;
        set => SetField(ref field, value);
    }

    public bool ShowSizes
    {
        get;
        set => SetField(ref field, value);
    }

    public bool ShowTriggerHeadArea
    {
        get;
        set => SetField(ref field, value);
    } = true;

    public bool ShowAIConfidence
    {
        get;
        set => SetField(ref field, value);
    } = true;

    public bool ShowTracers
    {
        get;
        set => SetField(ref field, value);
    } = true;

    public bool CollectDataWhilePlaying
    {
        get;
        set => SetField(ref field, value);
    }

    public bool AutoLabelData
    {
        get;
        set => SetField(ref field, value);
    }

    public bool LGHubMouseMovement
    {
        get;
        set => SetField(ref field, value);
    }

    public bool HideUIFromCapture
    {
        get => _hideUIFromCapture;
        set => SetField(ref _hideUIFromCapture, value);
    }

    public bool ShowHelpTexts
    {
        get;
        set => SetField(ref field, value);
    } = true;

    public bool UITopMost
    {
        get;
        set => SetField(ref field, value);
    } = true;

    public bool XAxisPercentageAdjustment
    {
        get;
        set => SetField(ref field, value);
    }

    public bool YAxisPercentageAdjustment
    {
        get;
        set => SetField(ref field, value);
    } = true;
}