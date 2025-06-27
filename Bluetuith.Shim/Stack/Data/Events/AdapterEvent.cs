using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Data.Models;
using Bluetuith.Shim.Types;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Data.Events;

public interface IAdapterEvent
{
    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("powered")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<bool> OptionPowered { get; set; }

    [JsonPropertyName("discoverable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<bool> OptionDiscoverable { get; set; }

    [JsonPropertyName("pairable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<bool> OptionPairable { get; set; }

    [JsonPropertyName("discovering")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<bool> OptionDiscovering { get; set; }

    public void PrintEventProperties(ref StringBuilder stringBuilder);
}

public abstract record class AdapterEventBaseModel : IAdapterEvent
{
    public string Address { get; set; } = "";
    public Nullable<bool> OptionPowered { get; set; }
    public Nullable<bool> OptionDiscoverable { get; set; }
    public Nullable<bool> OptionPairable { get; set; }
    public Nullable<bool> OptionDiscovering { get; set; }

    public void PrintEventProperties(ref StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"Address: {Address}");

        OptionPowered.AppendString("Powered", ref stringBuilder);
        OptionDiscoverable.AppendString("Discoverable", ref stringBuilder);
        OptionPairable.AppendString("Pairable", ref stringBuilder);
        OptionDiscovering.AppendString("Discovering", ref stringBuilder);
    }
}

public record class AdapterEvent : AdapterModel, IEvent
{
    EventType IEvent.Event => EventTypes.EventAdapter;

    private EventAction _action = EventAction.Added;

    public EventAction Action
    {
        get => _action;
        set => _action = value;
    }

    public new string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        PrintEventProperties(ref stringBuilder);

        return stringBuilder.ToString();
    }

    public new void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(DataSerializableContext.AdapterEventPropertyName);
        if (_action == EventAction.Added)
        {
            (this as IAdapter).SerializeAll(writer, DataSerializableContext.Default);
            return;
        }

        (this as IAdapterEvent).SerializeSelected(writer, DataSerializableContext.Default);
    }
}
