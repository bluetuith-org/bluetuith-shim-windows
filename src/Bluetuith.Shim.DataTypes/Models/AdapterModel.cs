﻿using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using InTheHand.Net.Bluetooth;

namespace Bluetuith.Shim.DataTypes;

public interface IAdapter : IAdapterEvent
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Name { get; set; }

    [JsonPropertyName("alias")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Alias { get; set; }

    [JsonPropertyName("unique_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string UniqueName { get; set; }

    [JsonPropertyName("uuids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid[] UUIDs { get; set; }
}

public abstract record class AdapterBaseModel : AdapterEventBaseModel, IAdapter
{
    public string Name { get; set; } = "";
    public string Alias { get; set; } = "";
    public string UniqueName { get; set; } = "";
    public Guid[] UUIDs { get; set; }

    protected void PrintProperties(ref StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"Name: {Name}");
        if (UUIDs != null && UUIDs.Length > 0)
        {
            stringBuilder.AppendLine("Profiles:");
            foreach (Guid uuid in UUIDs)
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

public record class AdapterModel : AdapterBaseModel, IResult
{
    public AdapterModel() { }

    public string ToConsoleString()
    {
        StringBuilder stringBuilder = new();

        PrintEventProperties(ref stringBuilder);
        PrintProperties(ref stringBuilder);

        return stringBuilder.ToString();
    }

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(SerializableContext.AdapterPropertyName);
        (this as IAdapter).SerializeAll(writer);
    }
}

public static class AdapterModelExtensions
{
    public static GenericResult<List<AdapterModel>> ToResult(
        this List<AdapterModel> adapters,
        string consoleObject,
        string jsonObject
    )
    {
        return new GenericResult<List<AdapterModel>>(
            consoleFunc: () =>
            {
                StringBuilder stringBuilder = new();

                stringBuilder.AppendLine(consoleObject);
                foreach (AdapterModel adapter in adapters)
                {
                    stringBuilder.AppendLine(adapter.ToConsoleString());
                }

                return stringBuilder.ToString();
            },
            jsonNodeFunc: (writer) =>
            {
                writer.WriteStartArray(jsonObject);
                foreach (AdapterModel adapter in adapters)
                {
                    (adapter as IAdapter).SerializeAll(writer);
                }
                writer.WriteEndArray();
            }
        );
    }
}
