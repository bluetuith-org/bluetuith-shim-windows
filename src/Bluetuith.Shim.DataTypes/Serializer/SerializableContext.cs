using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bluetuith.Shim.DataTypes;

internal class KebabCaseEnumConverter<TEnum>()
    : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.KebabCaseLower)
    where TEnum : struct, Enum;

[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(IConvertible))]
[JsonSerializable(typeof(Features))]
[JsonSerializable(typeof(PlatformInfo))]
[JsonSerializable(typeof(IAdapterEvent))]
[JsonSerializable(typeof(AuthenticationEvent.AuthenticationParameters))]
[JsonSerializable(typeof(AuthenticationEvent.AuthenticationEventType))]
[JsonSerializable(typeof(AuthenticationEvent.AuthenticationReplyMethod))]
[JsonSerializable(typeof(PairingAuthenticationEvent.PairingParameters))]
[JsonSerializable(typeof(OppAuthenticationEvent.OppParameters))]
[JsonSerializable(typeof(IDeviceEvent))]
[JsonSerializable(typeof(IFileTransferEvent))]
[JsonSerializable(typeof(IFileTransferEvent.TransferStatus))]
[JsonSerializable(typeof(MessageEvent))]
[JsonSerializable(typeof(IAdapter))]
[JsonSerializable(typeof(BMessageItem))]
[JsonSerializable(typeof(IDevice))]
[JsonSerializable(typeof(IFileTransfer))]
[JsonSerializable(typeof(MessageItem))]
[JsonSerializable(typeof(IVcard))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    UseStringEnumConverter = true,
    Converters = [
        typeof(KebabCaseEnumConverter<IFileTransferEvent.TransferStatus>),
        typeof(KebabCaseEnumConverter<AuthenticationEvent.AuthenticationEventType>),
        typeof(KebabCaseEnumConverter<AuthenticationEvent.AuthenticationReplyMethod>),
    ]
)]
public partial class SerializableContext : JsonSerializerContext
{
    public static readonly JsonEncodedText FeaturesPropertyName = JsonEncodedText.Encode(
        "features"
    );

    public static readonly JsonEncodedText PlatformPropertyNme = JsonEncodedText.Encode("platform");

    public static readonly JsonEncodedText AdapterPropertyName = JsonEncodedText.Encode("adapter");

    public static readonly JsonEncodedText BMessagePropertyName = JsonEncodedText.Encode(
        "bmessage_list"
    );

    public static readonly JsonEncodedText DevicePropertyName = JsonEncodedText.Encode("device");

    public static readonly JsonEncodedText FileTransferPropertyName = JsonEncodedText.Encode(
        "file_transfer"
    );
    public static readonly JsonEncodedText MessageListPropertyName = JsonEncodedText.Encode(
        "message_list"
    );

    public static readonly JsonEncodedText VcardPropertyName = JsonEncodedText.Encode("vcard");

    public static readonly JsonEncodedText AdapterEventPropertyName = JsonEncodedText.Encode(
        "adapter_event"
    );

    public static readonly JsonEncodedText PairingAuthEventPropertyName = JsonEncodedText.Encode(
        "pairing_auth_event"
    );

    public static readonly JsonEncodedText TransferAuthEventPropertyName = JsonEncodedText.Encode(
        "transfer_auth_event"
    );

    public static readonly JsonEncodedText DeviceEventPropertyName = JsonEncodedText.Encode(
        "device_event"
    );
    public static readonly JsonEncodedText FileTransferEventPropertyName = JsonEncodedText.Encode(
        "file_transfer_event"
    );

    public static readonly JsonEncodedText MessageHandleEventPropertyName = JsonEncodedText.Encode(
        "message_handle_event"
    );
}
