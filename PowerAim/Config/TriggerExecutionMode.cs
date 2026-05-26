using System.ComponentModel;

namespace PowerAim.Config;

public enum TriggerExecutionMode
{
    [Description("Simultaneous execution")]
    Simultaneous,
    [Description("Sequential execution")]
    Sequential,
}

public enum KeyOperator
{
    [Description("And")]
    And,
    [Description("Or")]
    Or,
}