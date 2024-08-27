using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Aimmy2.InputLogic;
using Aimmy2.InputLogic.Contracts;

namespace Aimmy2.Config;

public class BindingSettings : BaseSettings<StoredInputBinding>
{
    public StoredInputBinding DynamicFOVKeybind { get => Get(MouseButtons.Left); set => Set(value); }

    public StoredInputBinding RapidFireKey { get => Get(); set => Set(value); }

    public StoredInputBinding TriggerAdditionalCommandKey { get => Get(GamepadSlider.LeftTrigger); set => Set(value); }

    public StoredInputBinding TriggerKey { get => Get(GamepadSlider.LeftTrigger); set => Set(value); }

    public StoredInputBinding TriggerAdditionalSend { get => Get(MouseButtons.Middle); set => Set(value); }

    public StoredInputBinding AimKeybind { get => Get(MouseButtons.Right); set => Set(value); }
    public StoredInputBinding MagnifierKeybind { get => Get(); set => Set(value); }
    public StoredInputBinding MagnifierZoomInKeybind { get => Get(Keys.Add); set => Set(value); }
    public StoredInputBinding MagnifierZoomOutKeybind { get => Get(Keys.Subtract); set => Set(value); }
    
    public StoredInputBinding SecondAimKeybind { get => Get(Keys.LMenu); set => Set(value); }
    
    public StoredInputBinding ModelSwitchKeybind { get => Get(); set => Set(value); }

    public StoredInputBinding AntiRecoilKeybind { get => Get(MouseButtons.Left); set => Set(value); }

    public StoredInputBinding DisableAntiRecoilKeybind { get => Get(Keys.Oem6); set => Set(value); }

    public StoredInputBinding Gun1Key { get => Get(Keys.D1); set => Set(value); }

    public StoredInputBinding Gun2Key { get => Get(Keys.D2); set => Set(value); }
}