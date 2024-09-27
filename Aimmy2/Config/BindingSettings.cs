using System.Collections.ObjectModel;
using System.Windows.Forms;
using Aimmy2.InputLogic;

namespace Aimmy2.Config;

public class BindingSettings : BaseSettings<StoredInputBinding>
{
    private ObservableCollection<StoredInputBinding> _aimKeyBindings = [MouseButtons.Right, Keys.LMenu];

    public ObservableCollection<StoredInputBinding> AimKeyBindings
    {
        get => _aimKeyBindings;
        set => SetField(ref _aimKeyBindings, value);
    }

    public StoredInputBinding DynamicFOVKeybind { get => Get(MouseButtons.Left); set => Set(value); }


    public StoredInputBinding MagnifierKeybind { get => Get(); set => Set(value); }
    public StoredInputBinding MagnifierZoomInKeybind { get => Get(Keys.Add); set => Set(value); }
    public StoredInputBinding MagnifierZoomOutKeybind { get => Get(Keys.Subtract); set => Set(value); }
    
    
    public StoredInputBinding ModelSwitchKeybind { get => Get(); set => Set(value); }

    public StoredInputBinding AntiRecoilKeybind { get => Get(MouseButtons.Left); set => Set(value); }

    public StoredInputBinding DisableAntiRecoilKeybind { get => Get(Keys.Oem6); set => Set(value); }

    public StoredInputBinding Gun1Key { get => Get(Keys.D1); set => Set(value); }

    public StoredInputBinding Gun2Key { get => Get(Keys.D2); set => Set(value); }
}