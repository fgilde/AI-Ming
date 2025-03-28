﻿namespace Aimmy2.Config;

public class DropdownState: BaseSettings
{
    private string _gamepadProcess = "";
    private PredictionMethod _predictionMethod = PredictionMethod.KalmanFilter;
    private DetectionAreaType _detectionAreaType = DetectionAreaType.ClosestToCenter;
    private AimingBoundariesAlignment _aimingBoundariesAlignment = AimingBoundariesAlignment.Top;
    private MouseMovementMethod _mouseMovementMethod = MouseMovementMethod.MouseEvent;
    private OverlayDrawingMethod _overlayDrawingMethod = OverlayDrawingMethod.WpfWindowCanvas;
    private GamepadSendMode _gamepadSendMode = GamepadSendMode.ViGEm;
    
    public GamepadSendMode GamepadSendMode
    {
        get => _gamepadSendMode;
        set => SetField(ref _gamepadSendMode, value);
    }

    public string GamepadProcess
    {
        get => _gamepadProcess;
        set => SetField(ref _gamepadProcess, value);
    }
    
    public OverlayDrawingMethod OverlayDrawingMethod    
    {
        get => _overlayDrawingMethod;
        set => SetField(ref _overlayDrawingMethod, value);
    }

    public PredictionMethod PredictionMethod
    {
        get => _predictionMethod;
        set => SetField(ref _predictionMethod, value);
    }

    public DetectionAreaType DetectionAreaType
    {
        get => _detectionAreaType;
        set => SetField(ref _detectionAreaType, value);
    }

    public AimingBoundariesAlignment AimingBoundariesAlignment
    {
        get => _aimingBoundariesAlignment;
        set => SetField(ref _aimingBoundariesAlignment, value);
    }

    public MouseMovementMethod MouseMovementMethod
    {
        get => _mouseMovementMethod;
        set => SetField(ref _mouseMovementMethod, value);
    }
}