using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using DotNext;
using InTheHand.Net.Bluetooth;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Events;

public interface IDeviceEvent
{
    [JsonPropertyName("address")]
    public string Address { get; }

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
    private readonly EventAction _action = EventAction.Added;
    EventType IEvent.Event => EventTypes.EventDevice;
    EventAction IEvent.Action => _action;

    public DeviceEvent(EventAction action = EventAction.Added)
    {
        _action = action;
    }

    public DeviceEvent(DeviceEventBaseModel model, EventAction action = EventAction.Added)
        : base(model)
    {
        _action = action;
    }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        AppendEventProperties(ref stringBuilder);

        return stringBuilder.ToString();
    }

    public (string, JsonNode) ToJsonNode()
    {
        return ("device_event", (this as IDeviceEvent).SerializeSelected());
    }
}
