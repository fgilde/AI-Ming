using System.ComponentModel;

namespace PowerAim.Config;

public enum PredictionMethod
{
    [Description("Kalman Filter")]
    KalmanFilter,

    [Description("Shall0e's Prediction")]
    Shall0,

    [Description("wisethef0x's EMA Prediction")]
    WiseThef0x,
}