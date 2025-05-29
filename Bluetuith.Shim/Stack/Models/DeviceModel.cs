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
    public string Name { get; }

    [JsonPropertyName("alias")]
    public string Alias { get; }

    [JsonPropertyName("class")]
    public uint Class { get; }

    [JsonPropertyName("legacy_pairing")]
    public bool LegacyPairing { get; }
}

public abstract record class DeviceBaseModel : DeviceEventBaseModel, IDevice
{
    public string Name { get; protected set; } = "";
    public string Alias { get; protected set; } = "";
    public uint Class { get; protected set; } = 0;
    public bool LegacyPairing { get; protected set; } = false;
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
        return new DeviceEvent(device, action);
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
