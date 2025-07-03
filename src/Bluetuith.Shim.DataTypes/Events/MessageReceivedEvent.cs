using System.Text.Json;
using System.Text.Json.Serialization;
using static Bluetuith.Shim.DataTypes.IEvent;

namespace Bluetuith.Shim.DataTypes;

public record class MessageReceivedEvent : MessageEvent, IEvent
{
    EventType IEvent.Event => EventTypes.EventDevice;

    public EventAction _action = EventAction.Added;
    public EventAction Action
    {
        get => _action;
        set => _action = value;
    }

    public MessageReceivedEvent() { }

    public string ToConsoleString()
    {
        return $"[+] New message event from device {Address} (folder {Folder}){Environment.NewLine}";
    }

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(SerializableContext.MessageHandleEventPropertyName);
        (this as MessageEvent).SerializeSelected(writer);
    }
}

public record class MessageEvent
{
    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("folder")]
    public string Folder { get; set; }

    [JsonPropertyName("handle")]
    public string Handle { get; set; }

    [JsonPropertyName("message_event_type")]
    public string MessageEventType { get; set; }

    [JsonPropertyName("message_type")]
    public string MessageType { get; set; }
}
