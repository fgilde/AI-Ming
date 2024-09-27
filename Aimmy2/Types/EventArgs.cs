namespace Aimmy2.Types;

public class EventArgs<T> : EventArgs
{
    public EventArgs(T value)
    {
        Value = value;
    }
    public T Value { get; set; }
}

public class CancelableEventArgs<T>: EventArgs<T>
{
    public CancelableEventArgs(T value) : base(value)
    {
    }

    public bool Cancel { get; set; }
}