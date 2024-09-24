using System.Windows.Forms;
using Aimmy2.InputLogic;
using Aimmy2.Types;

namespace Aimmy2.Config;

public class ActionTrigger : BaseSettings
{
    private string _name;
    private bool _enabled;
    private bool _chargeMode;
    private StoredInputBinding _action = MouseButtons.Left;
    private StoredInputBinding _triggerKey;
    private double _delay;
    private double _breakTime;
    private double _triggerKeyMin;
    private TriggerCheck _intersectionCheck;
    private RelativeRect _intersectionArea;

    /// <summary>
    /// Name of your trigger
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    /// <summary>
    /// Trigger is enabled
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    /// <summary>
    ///When charge mode is enabled the <see cref="Action"/> will we pressed down when a prediction is available and released when the prediction is intersecting based on your <see cref="IntersectionCheck"/>
    /// </summary>
    public bool ChargeMode
    {
        get => _chargeMode;
        set => SetField(ref _chargeMode, value);
    }

    /// <summary>
    /// Action to be triggered
    /// </summary>
    public StoredInputBinding Action
    {
        get => _action;
        set => SetField(ref _action, value);
    }

    /// <summary>
    /// Key that needs to be hold before trigger is executed
    /// </summary>
    public StoredInputBinding TriggerKey
    {
        get => _triggerKey;
        set => SetField(ref _triggerKey, value);
    }

    /// <summary>
    /// Delay before trigger is executed
    /// </summary>
    public double Delay
    {
        get => _delay;
        set => SetField(ref _delay, value);
    }

    /// <summary>
    /// If the trigger was executed it needs to wait for the break time before it will be executed again
    /// </summary>
    public double BreakTime
    {
        get => _breakTime;
        set => SetField(ref _breakTime, value);
    }

    /// <summary>
    /// Time the <see cref="TriggerKey"/> needs to be hold before trigger is executed
    /// </summary>
    public double TriggerKeyMin
    {
        get => _triggerKeyMin;
        set => SetField(ref _triggerKeyMin, value);
    }

    /// <summary>
    /// Intersection check for the detected object before trigger is executed
    /// </summary>
    public TriggerCheck IntersectionCheck
    {
        get => _intersectionCheck;
        set => SetField(ref _intersectionCheck, value);
    }

    /// <summary>
    /// Intersection area for the detected object before trigger is executed only if <see cref="IntersectionCheck"/> is set to <see cref="TriggerCheck.HeadIntersectingCenter"/>
    /// </summary>
    public RelativeRect IntersectionArea
    {
        get => _intersectionArea;
        set => SetField(ref _intersectionArea, value);
    }

}