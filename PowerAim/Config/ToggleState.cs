namespace PowerAim.Config;

public class ToggleState : BaseSettings
{
    private bool _hideUIFromCapture = true;

    public bool AutoPlay
    {
        get;
        set => SetField(ref field, value);
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