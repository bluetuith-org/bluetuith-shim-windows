using System.Text.Json;

namespace Bluetuith.Shim.DataTypes;

public readonly struct Features(Features.FeatureFlags flags) : IResult
{
    public enum FeatureFlags : uint
    {
        FeatureConnection = 1 << 1,
        FeaturePairing = 1 << 2,
        FeatureSendFile = 1 << 3,
        FeatureReceiveFile = 1 << 4,
    }

    private readonly FeatureFlags _flags = flags;

    public string ToConsoleString()
    {
        return $"Features: {_flags}";
    }

    public void WriteJsonToStream(Utf8JsonWriter writer)
    {
        writer.WriteNumber(SerializableContext.FeaturesPropertyName, (int)_flags);
    }
}
