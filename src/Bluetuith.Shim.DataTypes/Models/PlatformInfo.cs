using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bluetuith.Shim.DataTypes;

public struct PlatformInfo : IResult
{
    [JsonPropertyName("stack")]
    public string Stack { get; set; }

    [JsonPropertyName("os_info")]
    public string OsInfo { get; set; }

    public readonly string ToConsoleString()
    {
        return $"Stack: {Stack} ({OsInfo})";
    }

    public readonly void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WritePropertyName(SerializableContext.PlatformPropertyNme);
        this.SerializeAll(writer);
    }
}
