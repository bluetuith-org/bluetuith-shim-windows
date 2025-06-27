using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Stack;

namespace Bluetuith.Shim.Extensions;

public static class SerializerExtensions
{
    public static void SerializeAll<T>(
        this T result,
        Utf8JsonWriter writer,
        JsonSerializerContext context
    )
    {
        JsonSerializer.Serialize(writer, result, typeof(T), context);
    }

    public static void SerializeSelected<T>(
        this T result,
        Utf8JsonWriter writer,
        JsonSerializerContext context
    )
    {
        JsonSerializer.Serialize(writer, result, typeof(T), context);
    }
}

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(IConvertible))]
[JsonSerializable(typeof(IStack.FeatureFlags))]
[JsonSerializable(typeof(IStack.PlatformInfo))]
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class SerializableContext : JsonSerializerContext { }
