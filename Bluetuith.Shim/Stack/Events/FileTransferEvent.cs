using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using static Bluetuith.Shim.Stack.Events.IFileTransferEvent;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Events;

public interface IFileTransferEvent
{
    public enum TransferStatus
    {
        Queued,
        Active,
        Suspended,
        Complete,
        Error,
    }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("filename")]
    public string FileName { get; set; }

    [JsonPropertyName("size")]
    public long FileSize { get; set; }

    [JsonPropertyName("transferred")]
    public long BytesTransferred { get; set; }

    [JsonPropertyName("status")]
    public TransferStatus Status { get; set; }

    public void AppendEventProperties(ref StringBuilder stringBuilder);
}

public abstract record class FileTransferEventBaseModel : IFileTransferEvent
{
    public string Address { get; set; } = "";

    public string Name { get; set; } = "";

    public string FileName { get; set; } = "";

    public long FileSize { get; set; }

    public long BytesTransferred { get; set; }

    public TransferStatus Status { get; set; }

    public void AppendEventProperties(ref StringBuilder stringBuilder)
    {
        stringBuilder.Append($"Name: {Name}, ");
        stringBuilder.Append($"File: {FileName}, ");
        stringBuilder.Append($"Address: {Address}, ");
        stringBuilder.Append($"Progress: {BytesTransferred} b/{FileSize} b");
    }
}

public record class FileTransferEvent : FileTransferEventBaseModel, IEvent
{
    EventType IEvent.Event => EventTypes.EventFileTransfer;

    private EventAction _action = EventAction.Added;
    public EventAction Action
    {
        get => _action;
        set => _action = value;
    }

    public FileTransferEvent() { }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();

        AppendEventProperties(ref stringBuilder);
        stringBuilder.AppendLine();

        return stringBuilder.ToString();
    }

    public (string, JsonNode) ToJsonNode()
    {
        return ("file_transfer_event", (this as IFileTransferEvent).SerializeAll());
    }
}
