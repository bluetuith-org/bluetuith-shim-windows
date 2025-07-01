using System.Text.Json;

namespace Bluetuith.Shim.DataTypes;

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

public static class ActionEnumExtensions
{
    private static readonly JsonEncodedText noneAction = JsonEncodedText.Encode("none");
    private static readonly JsonEncodedText addedAction = JsonEncodedText.Encode("added");
    private static readonly JsonEncodedText updatedAction = JsonEncodedText.Encode("updated");
    private static readonly JsonEncodedText removedAction = JsonEncodedText.Encode("removed");

    public static JsonEncodedText ToJsonEncodedText(this IEvent.EventAction action)
    {
        return action switch
        {
            IEvent.EventAction.Added => addedAction,
            IEvent.EventAction.Updated => updatedAction,
            IEvent.EventAction.Removed => removedAction,
            _ => noneAction,
        };
    }
}

public readonly record struct EventType
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
