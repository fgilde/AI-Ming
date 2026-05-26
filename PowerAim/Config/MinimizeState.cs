namespace PowerAim.Config;

// TODO: Remove and just store hashed values for minimized boxes
public class MinimizeState: BaseSettings
{
    public List<string> Minimized
    {
        get;
        set => SetField(ref field, value);
    } = new();

    public bool IsMinimized(string boxName) => Minimized.Contains(PrepareName(boxName));
    public void SetMinimized(string boxName, bool minimized)
    {
        boxName = PrepareName(boxName);
        switch (minimized)
        {
            case true when !Minimized.Contains(boxName):
                Minimized.Add(boxName);
                break;
            case false when Minimized.Contains(boxName):
                Minimized.Remove(boxName);
                break;
        }
    }
}
