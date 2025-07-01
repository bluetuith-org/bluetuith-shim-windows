using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bluetuith.Shim.DataTypes;

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
