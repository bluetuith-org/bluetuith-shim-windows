using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Bluetuith.Shim.DataTypes.IEvent;
using static Bluetuith.Shim.DataTypes.IFileTransferEvent;

namespace Bluetuith.Shim.DataTypes;

public interface IFileTransferEvent
{
    [JsonConverter(typeof(JsonStringEnumConverter<TransferStatus>))]
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

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(ModelEventSerializableContext.FileTransferEventPropertyName);
        (this as IFileTransferEvent).SerializeAll(writer, ModelEventSerializableContext.Default);
    }
}
