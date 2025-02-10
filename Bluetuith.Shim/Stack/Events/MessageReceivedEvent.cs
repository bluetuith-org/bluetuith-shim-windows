using System.Text.Json.Nodes;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using GoodTimeStudio.MyPhone.OBEX;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Events;

public record class MessageReceivedEvent : IEvent
{
    private string Address { get; set; }
    public MessageReceivedEventArgs MessageEventArgs { get; set; }

    EventType IEvent.Event => EventTypes.EventDevice;
    EventAction IEvent.Action => EventAction.Added;

    public MessageReceivedEvent(string address, MessageReceivedEventArgs messageEventArgs)
    {
        Address = address;
        MessageEventArgs = messageEventArgs;
    }

    public string ToConsoleString()
    {
        return $"[+] New message event from device {Address} (handle {MessageEventArgs.MessageHandle}, folder {MessageEventArgs.Folder}){Environment.NewLine}";
    }

    public (string, JsonNode) ToJsonNode()
    {
        return (
            "message_handle_event",
            new JsonObject()
            {
                ["address"] = Address,
                ["folder"] = MessageEventArgs.Folder,
                ["handle"] = MessageEventArgs.MessageHandle,
                ["event_type"] = MessageEventArgs.EventType,
                ["message_type"] = MessageEventArgs.MessageType,
            }.SerializeAll()
        );
    }
}
