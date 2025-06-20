using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Types;
using Windows.Devices.Enumeration;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Events;

public record class AuthenticationEvent : IEvent
{
    protected enum AuthenticationReplyMethod : byte
    {
        ReplyNone = 0,
        ReplyYesNo,
        ReplyWithInput,
    }

    protected enum AuthenticationEventType : byte
    {
        AuthEventNone = 0,
        DisplayPinCode,
        DisplayPasskey,
        ConfirmPasskey,
        AuthorizePairing,
        AuthorizeService,
        AuthorizeTransfer,
    }

    protected class AuthenticationParameters
    {
        [JsonPropertyName("auth_id")]
        public int AuthId { get; set; }

        [JsonPropertyName("auth_event")]
        public AuthenticationEventType AuthEvent { get; set; }

        [JsonPropertyName("auth_reply_method")]
        public AuthenticationReplyMethod AuthReplyMethod { get; set; }

        [JsonPropertyName("timeout_ms")]
        public int TimeoutMs { get; set; }
    }

    protected AuthenticationReplyMethod ReplyMethod { get; }
    public string TextToValidate { get; }
    public int TimeoutMs { get; }
    protected readonly OperationToken Token;

    private bool _response = false;
    private bool _responseSet = false;

    EventType IEvent.Event => EventTypes.EventAuthentication;
    IEvent.EventAction IEvent.Action
    {
        get => EventAction.Added;
        set => throw new InvalidDataException();
    }

    protected AuthenticationEvent(
        string textToValidate,
        int timeoutMs,
        AuthenticationReplyMethod replyMethod,
        OperationToken token
    )
    {
        ReplyMethod = replyMethod;
        TextToValidate = textToValidate;
        TimeoutMs = timeoutMs;
        Token = token;
    }

    protected AuthenticationEvent(
        int timeout,
        AuthenticationReplyMethod replyMethod,
        OperationToken token
    )
        : this("", timeout, replyMethod, token) { }

    public bool WaitForResponse()
    {
        if (TimeoutMs > 0)
            if (!Token.ReleaseAfter(TimeoutMs))
                return false;

        if (ReplyMethod != AuthenticationReplyMethod.ReplyNone)
            Token.Wait();

        return !_responseSet ? SetResponse() : _response;
    }

    public bool SetResponse(string response = "")
    {
        _response = false;
        response = response.ToLower();

        switch (ReplyMethod)
        {
            case AuthenticationReplyMethod.ReplyYesNo:
                _response = response is "y" or "yes";
                break;

            case AuthenticationReplyMethod.ReplyWithInput:
                _response = response == TextToValidate;
                break;

            case AuthenticationReplyMethod.ReplyNone:
                _response = true;
                break;
        }

        _responseSet = true;
        Token.Release();

        return _response;
    }

    public virtual string ToConsoleString() => "";

    public virtual (string, JsonNode) ToJsonNode() => ("", (JsonObject)[]);
}

public record class PairingAuthenticationEvent : AuthenticationEvent
{
    private class PairingParameters : AuthenticationParameters
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("pincode")]
        public string Pincode { get; set; }

        [JsonPropertyName("passkey")]
        public uint Passkey { get; set; }
    }

    private readonly string _address;
    private readonly DevicePairingKinds _devicePairingKinds;

    public PairingAuthenticationEvent(
        string address,
        string pin,
        int timeout,
        DevicePairingKinds pairingKind,
        OperationToken token
    )
        : base(pin, timeout, GetReplyMethod(pairingKind), token)
    {
        _address = address;
        _devicePairingKinds = pairingKind;
    }

    private static AuthenticationReplyMethod GetReplyMethod(DevicePairingKinds pairingKinds) =>
        pairingKinds switch
        {
            DevicePairingKinds.DisplayPin => AuthenticationReplyMethod.ReplyNone,
            DevicePairingKinds.ProvidePin => AuthenticationReplyMethod.ReplyNone,
            _ => AuthenticationReplyMethod.ReplyYesNo,
        };

    private static AuthenticationEventType GetAuthEvent(DevicePairingKinds pairingKinds)
    {
        switch (pairingKinds)
        {
            case DevicePairingKinds.DisplayPin:
                return AuthenticationEventType.DisplayPinCode;

            case DevicePairingKinds.ConfirmOnly:
                return AuthenticationEventType.AuthorizePairing;

            case DevicePairingKinds.ConfirmPinMatch:
                return AuthenticationEventType.ConfirmPasskey;
        }

        return AuthenticationEventType.AuthEventNone;
    }

    public override string ToConsoleString() =>
        _devicePairingKinds switch
        {
            DevicePairingKinds.DisplayPin =>
                $"[{_address}] Enter pin on device: ({TextToValidate})",
            DevicePairingKinds.ConfirmPinMatch =>
                $"[{_address}] Confirm pairing with pin: {TextToValidate} (y/n)",
            _ => $"[{_address}] Confirm pairing (y/n)",
        };

    public override (string, JsonNode) ToJsonNode()
    {
        var parameters = new PairingParameters
        {
            Address = _address,
            AuthId = (int)Token.OperationId,
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

        return ("pairing_auth_event", parameters.SerializeAll());
    }
}

public record class OppAuthenticationEvent : AuthenticationEvent
{
    private readonly IFileTransferEvent _fileTransferEvent;

    private class OppParameters : AuthenticationParameters
    {
        [JsonPropertyName("file_transfer")]
        public IFileTransferEvent TransferEvent { get; set; }
    }

    public OppAuthenticationEvent(
        int timeout,
        IFileTransferEvent fileTransferEvent,
        OperationToken token
    )
        : base(timeout, AuthenticationReplyMethod.ReplyYesNo, token)
    {
        _fileTransferEvent = fileTransferEvent;
    }

    public override string ToConsoleString() =>
        $"Accept file {_fileTransferEvent.Name} from address {_fileTransferEvent.Address} (y/n)";

    public override (string, JsonNode) ToJsonNode()
    {
        var parameters = new OppParameters
        {
            AuthId = (int)Token.OperationId,
            AuthEvent = AuthenticationEventType.AuthorizeTransfer,
            AuthReplyMethod = AuthenticationReplyMethod.ReplyYesNo,
            TransferEvent = _fileTransferEvent,
            TimeoutMs = TimeoutMs,
        };

        return ("transfer_auth_event", parameters.SerializeAll());
    }
}
