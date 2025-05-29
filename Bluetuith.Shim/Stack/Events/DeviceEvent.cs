using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Types;
using DotNext;
using InTheHand.Net.Bluetooth;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Events;

public interface IDeviceEvent
{
    [JsonPropertyName("address")]
    public string Address { get; }

    [JsonPropertyName("associated_adapter")]
    public string AssociatedAdapter { get; }

    [JsonPropertyName("connected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionConnected { get; }

    [JsonPropertyName("paired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionPaired { get; }

    [JsonPropertyName("blocked")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionBlocked { get; }

    [JsonPropertyName("bonded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionBonded { get; }

    [JsonPropertyName("rssi")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<short> OptionRSSI { get; }

    [JsonPropertyName("percentage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int> OptionPercentage { get; }

    [JsonPropertyName("uuids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<Guid[]> OptionUUIDs { get; }

    public void AppendEventProperties(ref StringBuilder stringBuilder);
}

public abstract record class DeviceEventBaseModel : IDeviceEvent
{
    public string Address { get; protected set; } = "";
    public string AssociatedAdapter { get; protected set; } = "";

    public Optional<bool> OptionConnected { get; protected set; }
    public Optional<bool> OptionPaired { get; protected set; }
    public Optional<bool> OptionBlocked { get; protected set; }
    public Optional<bool> OptionBonded { get; protected set; }
    public Optional<short> OptionRSSI { get; protected set; }
    public Optional<int> OptionPercentage { get; protected set; }
    public Optional<Guid[]> OptionUUIDs { get; protected set; }

    public void AppendEventProperties(ref StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"Address: {Address}");
        stringBuilder.AppendLine($"Adapter: {AssociatedAdapter}");

        OptionConnected.AppendString("Connected", ref stringBuilder);
        OptionPaired.AppendString("Paired", ref stringBuilder);
        OptionBlocked.AppendString("Blocked", ref stringBuilder);
        OptionBonded.AppendString("Bonded", ref stringBuilder);
        OptionRSSI.AppendString("RSSI", ref stringBuilder);
        OptionPercentage.AppendString("Percentage", ref stringBuilder);

        if (OptionUUIDs.TryGet(out var uuids))
        {
            if (uuids.Length > 0)
            {
                stringBuilder.AppendLine("Profiles:");
                foreach (Guid uuid in uuids)
                {
                    var serviceName = BluetoothService.GetName(uuid);
                    if (string.IsNullOrEmpty(serviceName))
                    {
                        serviceName = "Unknown";
                    }
                    stringBuilder.AppendLine($"{serviceName} = {uuid}");
                }
            }
        }
    }
}

public record class DeviceEvent : DeviceEventBaseModel, IEvent
{
    private readonly DeviceBaseModel _device;

    EventType IEvent.Event => EventTypes.EventDevice;

    private EventAction _action = EventAction.Added;
    public EventAction Action
    {
        get => _action;
        set => _action = value;
    }

    public DeviceEvent(DeviceBaseModel model, EventAction action)
        : base(model)
    {
        _device = model;
        Action = action;
    }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        AppendEventProperties(ref stringBuilder);

        return stringBuilder.ToString();
    }

    public (string, JsonNode) ToJsonNode()
    {
        if (_action == EventAction.Added)
        {
            return ("device_event", (_device as IDevice).SerializeAll());
        }

        return ("device_event", (_device as IDeviceEvent).SerializeSelected());
    }
}
