using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Types;
using DotNext;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Events;

public interface IAdapterEvent
{
    [JsonPropertyName("address")]
    public string Address { get; }

    [JsonPropertyName("powered")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionPowered { get; }

    [JsonPropertyName("discoverable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionDiscoverable { get; }

    [JsonPropertyName("pairable")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionPairable { get; }

    [JsonPropertyName("discovering")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionDiscovering { get; }

    public void PrintEventProperties(ref StringBuilder stringBuilder);
}

public abstract record class AdapterEventBaseModel : IAdapterEvent
{
    public string Address { get; set; } = "";
    public Optional<bool> OptionPowered { get; set; }
    public Optional<bool> OptionDiscoverable { get; set; }
    public Optional<bool> OptionPairable { get; set; }
    public Optional<bool> OptionDiscovering { get; set; }

    public void PrintEventProperties(ref StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"Address: {Address}");

        OptionPowered.AppendString("Powered", ref stringBuilder);
        OptionDiscoverable.AppendString("Discoverable", ref stringBuilder);
        OptionPairable.AppendString("Pairable", ref stringBuilder);
        OptionDiscovering.AppendString("Discovering", ref stringBuilder);
    }
}

public record class AdapterEvent : AdapterEventBaseModel, IEvent
{
    private readonly AdapterBaseModel _model;

    EventType IEvent.Event => EventTypes.EventAdapter;

    private EventAction _action = EventAction.Added;

    public EventAction Action
    {
        get => _action;
        set => _action = value;
    }

    public AdapterEvent(AdapterBaseModel model, EventAction action)
        : base(model)
    {
        _model = model;
        _action = action;
    }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        PrintEventProperties(ref stringBuilder);

        return stringBuilder.ToString();
    }

    public (string, JsonNode) ToJsonNode()
    {
        if (_action == EventAction.Added)
        {
            return ("adapter_event", (_model as IAdapter).SerializeAll());
        }

        return ("adapter_event", (_model as IAdapterEvent).SerializeSelected());
    }
}
