namespace Bluetuith.Shim.Types;

public interface IEvent : IResult
{
    public enum EventAction
    {
        None = 0,
        Added,
        Updated,
        Removed,
    }

    public EventType Event { get; }
    public EventAction Action { get; set; }
}

public record struct EventType
{
    public byte Value { get; }

    public EventType(byte code)
    {
        Value = code;
    }
}

public partial class EventTypes
{
    protected const byte None = 0,
        Error = 1;

    public static readonly EventType EventNone = new(None);
    public static readonly EventType EventError = new(Error);
}
