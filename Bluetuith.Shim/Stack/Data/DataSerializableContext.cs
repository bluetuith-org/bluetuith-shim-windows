using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Stack.Data.Events;
using Bluetuith.Shim.Stack.Data.Models;

namespace Bluetuith.Shim.Stack.Data;

internal class KebabCaseEnumConverter<TEnum>()
    : JsonStringEnumConverter<TEnum>(JsonNamingPolicy.KebabCaseLower)
    where TEnum : struct, Enum;

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
[JsonSerializable(typeof(Message))]
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
internal partial class DataSerializableContext : JsonSerializerContext
{
    internal static readonly JsonEncodedText AdapterPropertyName = JsonEncodedText.Encode(
        "adapter"
    );

    internal static readonly JsonEncodedText BMessagePropertyName = JsonEncodedText.Encode(
        "bmessage_list"
    );

    internal static readonly JsonEncodedText DevicePropertyName = JsonEncodedText.Encode("device");

    internal static readonly JsonEncodedText FileTransferPropertyName = JsonEncodedText.Encode(
        "file_transfer"
    );
    internal static readonly JsonEncodedText MessageListPropertyName = JsonEncodedText.Encode(
        "message_list"
    );

    internal static readonly JsonEncodedText VcardPropertyName = JsonEncodedText.Encode("vcard");

    internal static readonly JsonEncodedText AdapterEventPropertyName = JsonEncodedText.Encode(
        "adapter_event"
    );

    internal static readonly JsonEncodedText PairingAuthEventPropertyName = JsonEncodedText.Encode(
        "pairing_auth_event"
    );

    internal static readonly JsonEncodedText TransferAuthEventPropertyName = JsonEncodedText.Encode(
        "transfer_auth_event"
    );

    internal static readonly JsonEncodedText DeviceEventPropertyName = JsonEncodedText.Encode(
        "device_event"
    );
    internal static readonly JsonEncodedText FileTransferEventPropertyName = JsonEncodedText.Encode(
        "file_transfer_event"
    );

    internal static readonly JsonEncodedText MessageHandleEventPropertyName =
        JsonEncodedText.Encode("message_handle_event");
}
