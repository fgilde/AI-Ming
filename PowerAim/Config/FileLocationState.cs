using PowerAim.InputLogic.HidHide;

namespace PowerAim.Config;

public class FileLocationState : BaseSettings
{
    public string HidHidePath
    {
        get;
        set => SetField(ref field, value);
    } = HidHideHelper.GetHidHideDefaultPath();

    public string DdxoftDLLLocation
    {
        get;
        set => SetField(ref field, value);
    } = "";

    public string Gun1Config
    {
        get;
        set => SetField(ref field, value);
    } = "";

    public string Gun2Config
    {
        get;
        set => SetField(ref field, value);
    } = "";
}
