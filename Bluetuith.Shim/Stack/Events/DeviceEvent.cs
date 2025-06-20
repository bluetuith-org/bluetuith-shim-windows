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
    public string Address { get; set; }

    [JsonPropertyName("associated_adapter")]
    public string AssociatedAdapter { get; set; }

    [JsonPropertyName("connected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionConnected { get; set; }

    [JsonPropertyName("paired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionPaired { get; set; }

    [JsonPropertyName("blocked")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionBlocked { get; set; }

    [JsonPropertyName("bonded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<bool> OptionBonded { get; set; }

    [JsonPropertyName("rssi")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<short> OptionRSSI { get; set; }

    [JsonPropertyName("percentage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<int> OptionPercentage { get; set; }

    [JsonPropertyName("uuids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Optional<Guid[]> OptionUUIDs { get; set; }

    public void AppendEventProperties(ref StringBuilder stringBuilder);
}

public abstract record class DeviceEventBaseModel : IDeviceEvent
{
    public string Address { get; set; } = "";
    public string AssociatedAdapter { get; set; } = "";

    public Optional<bool> OptionConnected { get; set; }
    public Optional<bool> OptionPaired { get; set; }
    public Optional<bool> OptionBlocked { get; set; }
    public Optional<bool> OptionBonded { get; set; }
    public Optional<short> OptionRSSI { get; set; }
    public Optional<int> OptionPercentage { get; set; }
    public Optional<Guid[]> OptionUUIDs { get; set; }

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

public record class DeviceEvent : DeviceModel, IEvent
{
    EventType IEvent.Event => EventTypes.EventDevice;

    public EventAction Action { get; set; } = EventAction.Added;

    public DeviceEvent() { }

    public new string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        AppendEventProperties(ref stringBuilder);

        return stringBuilder.ToString();
    }

    public new (string, JsonNode) ToJsonNode()
    {
        if (Action == EventAction.Added)
            return ("device_event", (this as IDevice).SerializeAll());

        return ("device_event", (this as IDeviceEvent).SerializeSelected());
    }
}
