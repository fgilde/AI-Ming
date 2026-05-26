namespace PowerAim.Config;

public class DropdownState: BaseSettings
{
    public MovementPathType MovementPathType
    {
        get;
        set => SetField(ref field, value);
    } = MovementPathType.Bezier;

    public GamepadSendMode GamepadSendMode
    {
        get;
        set => SetField(ref field, value);
    } = GamepadSendMode.ViGEm;

    public string GamepadProcess
    {
        get;
        set => SetField(ref field, value);
    } = "";

    public OverlayDrawingMethod OverlayDrawingMethod
    {
        get;
        set => SetField(ref field, value);
    } = OverlayDrawingMethod.WpfWindowCanvas;

    public PredictionMethod PredictionMethod
    {
        get;
        set => SetField(ref field, value);
    } = PredictionMethod.KalmanFilter;

    public DetectionAreaType DetectionAreaType
    {
        get;
        set => SetField(ref field, value);
    } = DetectionAreaType.ClosestToCenter;

    public AimingBoundariesAlignment AimingBoundariesAlignment
    {
        get;
        set => SetField(ref field, value);
    } = AimingBoundariesAlignment.Top;

    public MouseMovementMethod MouseMovementMethod
    {
        get;
        set => SetField(ref field, value);
    } = MouseMovementMethod.MouseEvent;
}
