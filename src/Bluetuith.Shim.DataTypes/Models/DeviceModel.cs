﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bluetuith.Shim.DataTypes;

public interface IDevice : IDeviceEvent
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Name { get; set; }

    [JsonPropertyName("alias")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Alias { get; set; }

    [JsonPropertyName("class")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint Class { get; set; }

    [JsonPropertyName("legacy_pairing")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(SerializableContext.DevicePropertyName);
        (this as IDevice).SerializeAll(writer);
    }
}

public static class DeviceModelExtensions
{
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
            jsonNodeFunc: (writer) =>
            {
                writer.WriteStartArray(jsonObject);
                foreach (DeviceModel device in devices)
                {
                    (device as IDevice).SerializeAll(writer);
                }
                writer.WriteEndArray();
            }
        );
    }
}
