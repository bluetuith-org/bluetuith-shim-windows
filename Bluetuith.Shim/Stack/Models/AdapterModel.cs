using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Types;
using InTheHand.Net.Bluetooth;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Models;

public interface IAdapter : IAdapterEvent
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("alias")]
    public string Alias { get; set; }

    [JsonPropertyName("unique_name")]
    public string UniqueName { get; set; }

    [JsonPropertyName("uuids")]
    public Guid[] UUIDs { get; set; }
}

public abstract record class AdapterBaseModel : AdapterEventBaseModel, IAdapter
{
    public string Name { get; set; } = "";
    public string Alias { get; set; } = "";
    public string UniqueName { get; set; } = "";
    public Guid[] UUIDs { get; set; } = [];

    protected void PrintProperties(ref StringBuilder stringBuilder)
    {
        stringBuilder.AppendLine($"Name: {Name}");
        if (UUIDs.Length > 0)
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

    public (string, JsonNode) ToJsonNode()
    {
        return ("adapter", (this as IAdapter).SerializeAll());
    }
}

public static class AdapterModelExtensions
{
    public static IEvent ToEvent(this AdapterModel adapter, EventAction action)
    {
        return adapter as AdapterEvent;
    }

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
            jsonNodeFunc: () =>
            {
                JsonArray array = [];
                foreach (AdapterModel adapter in adapters)
                {
                    var (_, node) = adapter.ToJsonNode();
                    array.Add(node);
                }

                return (jsonObject, array.SerializeAll());
            }
        );
    }
}
