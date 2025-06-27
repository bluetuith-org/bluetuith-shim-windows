using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using GoodTimeStudio.MyPhone.OBEX;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Data.Events;

public record class MessageReceivedEvent : IEvent
{
    private string Address { get; set; }
    public MessageReceivedEventArgs MessageEventArgs { get; set; }

    EventType IEvent.Event => EventTypes.EventDevice;

    public EventAction _action = EventAction.Added;
    public EventAction Action
    {
        get => _action;
        set => _action = value;
    }

    public MessageReceivedEvent(string address, MessageReceivedEventArgs messageEventArgs)
    {
        Address = address;
        MessageEventArgs = messageEventArgs;
    }

    public string ToConsoleString()
    {
        return $"[+] New message event from device {Address} (handle {MessageEventArgs.MessageHandle}, folder {MessageEventArgs.Folder}){Environment.NewLine}";
    }

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(DataSerializableContext.MessageHandleEventPropertyName);
        new MessageEvent(MessageEventArgs, Address).SerializeSelected(
            writer,
            DataSerializableContext.Default
        );
    }
}

internal class MessageEvent(MessageReceivedEventArgs message, string Address)
{
    [JsonPropertyName("address")]
    public string Address { get; set; } = Address;

    [JsonPropertyName("folder")]
    public string Folder { get; set; } = message.Folder;

    [JsonPropertyName("handle")]
    public string Handle { get; set; } = message.MessageHandle;

    [JsonPropertyName("message_event_type")]
    public string MessageEventType { get; set; } = message.EventType;

    [JsonPropertyName("message_type")]
    public string MessageType { get; set; } = message.MessageType;
}
