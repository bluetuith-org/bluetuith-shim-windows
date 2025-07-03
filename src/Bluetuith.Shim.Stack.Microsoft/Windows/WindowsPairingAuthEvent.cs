using System.Text.Json;
using Bluetuith.Shim.DataTypes;
using Windows.Devices.Enumeration;

namespace Bluetuith.Shim.Stack.Microsoft;

internal record class WindowsPairingAuthEvent : PairingAuthenticationEvent
{
    private readonly DevicePairingKinds _devicePairingKinds;

    internal WindowsPairingAuthEvent(
        string address,
        string pin,
        int timeout,
        DevicePairingKinds pairingKind,
        OperationToken token
    )
        : base(address, pin, timeout, GetReplyMethod(pairingKind), token)
    {
        _devicePairingKinds = pairingKind;
    }

    private static AuthenticationReplyMethod GetReplyMethod(DevicePairingKinds pairingKinds) =>
        pairingKinds switch
        {
            DevicePairingKinds.DisplayPin => AuthenticationReplyMethod.ReplyNone,
            DevicePairingKinds.ProvidePin => AuthenticationReplyMethod.ReplyNone,
            _ => AuthenticationReplyMethod.ReplyYesNo,
        };

    private static AuthenticationEventType GetAuthEvent(DevicePairingKinds pairingKinds) =>
        pairingKinds switch
        {
            DevicePairingKinds.DisplayPin => AuthenticationEventType.DisplayPinCode,

            DevicePairingKinds.ConfirmOnly => AuthenticationEventType.AuthorizePairing,

            DevicePairingKinds.ConfirmPinMatch => AuthenticationEventType.ConfirmPasskey,
            _ => AuthenticationEventType.AuthEventNone,
        };

    public override string ToConsoleString() =>
        _devicePairingKinds switch
        {
            DevicePairingKinds.DisplayPin =>
                $"[{_address}] Enter pin on device: ({TextToValidate})",
            DevicePairingKinds.ConfirmPinMatch =>
                $"[{_address}] Confirm pairing with pin: {TextToValidate} (y/n)",
            _ => $"[{_address}] Confirm pairing (y/n)",
        };

    public override void WriteJsonToStream(Utf8JsonWriter writer)
    {
        var parameters = new PairingParameters
        {
            AuthId = CurrentAuthId,
            Address = _address,
            AuthEvent = GetAuthEvent(_devicePairingKinds),
            AuthReplyMethod = GetReplyMethod(_devicePairingKinds),
            TimeoutMs = TimeoutMs,
        };

        if (!string.IsNullOrEmpty(TextToValidate))
            if (parameters.AuthEvent == AuthenticationEventType.DisplayPinCode)
            {
                parameters.Pincode = TextToValidate;
            }
            else
            {
                try
                {
                    parameters.Passkey = Convert.ToUInt32(TextToValidate);
                }
                catch { }
            }

        writer.WritePropertyName(SerializableContext.PairingAuthEventPropertyName);
        parameters.SerializeAll(writer);
    }
}
