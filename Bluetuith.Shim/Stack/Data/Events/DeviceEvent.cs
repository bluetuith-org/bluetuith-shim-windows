using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Data.Models;
using Bluetuith.Shim.Types;
using InTheHand.Net.Bluetooth;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Data.Events;

public interface IDeviceEvent
{
    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("associated_adapter")]
    public string AssociatedAdapter { get; set; }

    [JsonPropertyName("connected")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<bool> OptionConnected { get; set; }

    [JsonPropertyName("paired")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<bool> OptionPaired { get; set; }

    [JsonPropertyName("blocked")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<bool> OptionBlocked { get; set; }

    [JsonPropertyName("bonded")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<bool> OptionBonded { get; set; }

    [JsonPropertyName("rssi")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<short> OptionRSSI { get; set; }

    [JsonPropertyName("percentage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Nullable<int> OptionPercentage { get; set; }

    [JsonPropertyName("uuids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid[] OptionUUIDs { get; set; }

    public void AppendEventProperties(ref StringBuilder stringBuilder);
}

public abstract record class DeviceEventBaseModel : IDeviceEvent
{
    public string Address { get; set; } = "";
    public string AssociatedAdapter { get; set; } = "";

    public Nullable<bool> OptionConnected { get; set; }
    public Nullable<bool> OptionPaired { get; set; }
    public Nullable<bool> OptionBlocked { get; set; }
    public Nullable<bool> OptionBonded { get; set; }
    public Nullable<short> OptionRSSI { get; set; }
    public Nullable<int> OptionPercentage { get; set; }
    public Guid[] OptionUUIDs { get; set; }

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

        if (OptionUUIDs != null && OptionUUIDs.Length > 0)
        {
            stringBuilder.AppendLine("Profiles:");
            foreach (var uuid in OptionUUIDs)
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

    public new void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(DataSerializableContext.DeviceEventPropertyName);
        if (Action == EventAction.Added)
        {
            (this as IDevice).SerializeAll(writer, DataSerializableContext.Default);
            return;
        }

        (this as IDeviceEvent).SerializeSelected(writer, DataSerializableContext.Default);
    }
}
