using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DotNext.Text.Json;

namespace Bluetuith.Shim.Extensions;

public static class SerializerExtensions
{
    public static JsonSerializerOptions DefaultOptions { get; } =
        new JsonSerializerOptions()
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower),
                new OptionalConverterFactory(),
            },
        };

    public static JsonSerializerOptions SelectOptions { get; } =
        new JsonSerializerOptions()
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower),
                new OptionalConverterFactory(),
            },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        };

    public static JsonNode SerializeAll<T>(this T result)
    {
        return JsonSerializer.SerializeToNode(result, DefaultOptions) ?? (JsonObject)[];
    }

    public static JsonNode SerializeSelected<T>(this T result)
    {
        return JsonSerializer.SerializeToNode(result, SelectOptions) ?? (JsonObject)[];
    }
}
