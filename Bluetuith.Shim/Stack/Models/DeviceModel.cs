using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Types;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Models;

public interface IDevice : IDeviceEvent
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("alias")]
    public string Alias { get; set; }

    [JsonPropertyName("class")]
    public uint Class { get; set; }

    [JsonPropertyName("legacy_pairing")]
    public bool LegacyPairing { get; set; }
}

public abstract record class DeviceBaseModel : DeviceEventBaseModel, IDevice
{
    public string Name { get; set; } = "";
    public string Alias { get; set; } = "";
    public uint Class { get; set; } = 0;
    public bool LegacyPairing { get; set; } = false;
}

public record class DeviceModel : DeviceBaseModel, IResult
{
    public DeviceModel() { }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();
        stringBuilder.AppendLine($"Name: {Name}");

        AppendEventProperties(ref stringBuilder);

        return stringBuilder.ToString();
    }

    public (string, JsonNode) ToJsonNode()
    {
        return ("device", (this as IDevice).SerializeAll());
    }
}

public static class DeviceModelExtensions
{
    public static DeviceEvent ToEvent(
        this DeviceModel device,
        EventAction action = EventAction.Added
    )
    {
        return (device as DeviceEvent) with { Action = action };
    }

    public static GenericResult<List<DeviceModel>> ToResult(
        this List<DeviceModel> devices,
        string consoleObject,
        string jsonObject
    )
    {
        return new GenericResult<List<DeviceModel>>(
            consoleFunc: () =>
            {
                StringBuilder stringBuilder = new();

                stringBuilder.AppendLine(consoleObject);
                foreach (DeviceModel device in devices)
                {
                    stringBuilder.AppendLine(device.ToConsoleString());
                }

                return stringBuilder.ToString();
            },
            jsonNodeFunc: () =>
            {
                JsonArray array = [];
                foreach (DeviceModel device in devices)
                {
                    var (_, node) = device.ToJsonNode();
                    array.Add(node);
                }

                return (jsonObject, array.SerializeAll());
            }
        );
    }
}
