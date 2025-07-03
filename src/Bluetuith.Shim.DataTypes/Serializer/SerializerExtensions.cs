using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Bluetuith.Shim.DataTypes;

public static class SerializerExtensions
{
    private static readonly JsonSerializerOptions _options = new(
        SerializableContext.Default.Options
    )
    {
        TypeInfoResolver = SerializableContext.Default.WithAddedModifier(IgnoreEmptyStringModifier),
    };

#pragma warning disable IL2026,IL3050
    public static void SerializeAll<T>(this T result, Utf8JsonWriter writer)
    {
        JsonSerializer.Serialize(writer, result, _options);
    }

    public static void SerializeSelected<T>(this T result, Utf8JsonWriter writer)
    {
        JsonSerializer.Serialize(writer, result, _options);
    }
#pragma warning restore IL2026,IL3050

    private static void IgnoreEmptyStringModifier(JsonTypeInfo typeInfo)
    {
        if (typeInfo == null || typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in typeInfo.Properties)
        {
            if (property == null || property.PropertyType != typeof(string))
                continue;

            var ignoreAttr =
                (
                    property.AttributeProvider?.GetCustomAttributes(
                        typeof(JsonIgnoreAttribute),
                        false
                    )
                        is not object[] attr
                    || attr.Length == 0
                )
                    ? null
                    : (JsonIgnoreAttribute)attr[0];

            if (
                ignoreAttr is not null
                && ignoreAttr.Condition == JsonIgnoreCondition.WhenWritingDefault
            )
            {
                property.ShouldSerialize = (_, value) =>
                    value is string val && !string.IsNullOrEmpty(val);
            }
        }
    }
}
