using System.Text.Json.Serialization;

namespace Bluetuith.Shim.DataTypes;

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(IConvertible))]
/*[JsonSerializable(typeof(IStack.FeatureFlags))]
[JsonSerializable(typeof(IStack.PlatformInfo))]*/
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
internal partial class TypesSerializableContext : JsonSerializerContext { }
