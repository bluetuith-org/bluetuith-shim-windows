using System.Text.Json;
using System.Text.Json.Serialization;
using static Bluetuith.Shim.DataTypes.IEvent;

namespace Bluetuith.Shim.DataTypes;

public record class AuthenticationEvent : IEvent
{
    [JsonConverter(typeof(JsonStringEnumConverter<AuthenticationReplyMethod>))]
    public enum AuthenticationReplyMethod : byte
    {
        ReplyNone = 0,
        ReplyYesNo,
        ReplyWithInput,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<AuthenticationEventType>))]
    public enum AuthenticationEventType : byte
    {
        AuthEventNone = 0,
        DisplayPinCode,
        DisplayPasskey,
        ConfirmPasskey,
        AuthorizePairing,
        AuthorizeService,
        AuthorizeTransfer,
    }

    public class AuthenticationParameters
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

    public AuthenticationReplyMethod ReplyMethod { get; }
    public string TextToValidate { get; }
    public int TimeoutMs { get; }
    public readonly OperationToken Token;

    private bool _response = false;
    private bool _responseSet = false;

    EventType IEvent.Event => EventTypes.EventAuthentication;
    EventAction IEvent.Action
    {
        get => EventAction.Added;
        set => throw new InvalidDataException();
    }

    public AuthenticationEvent(
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

    public AuthenticationEvent(
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

    public virtual void WriteJsonToStream(Utf8JsonWriter writer) { }
}

public record class PairingAuthenticationEvent : AuthenticationEvent
{
    public class PairingParameters : AuthenticationParameters
    {
        [JsonPropertyName("address")]
        public string Address { get; set; }

        [JsonPropertyName("pincode")]
        public string Pincode { get; set; }

        [JsonPropertyName("passkey")]
        public uint Passkey { get; set; }
    }

    protected readonly string _address;

    public PairingAuthenticationEvent(
        string address,
        string pin,
        int timeout,
        AuthenticationReplyMethod pairingKind,
        OperationToken token
    )
        : base(pin, timeout, pairingKind, token)
    {
        _address = address;
    }
}

public record class OppAuthenticationEvent : AuthenticationEvent
{
    private readonly IFileTransferEvent _fileTransferEvent;

    public class OppParameters : AuthenticationParameters
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

    public override void WriteJsonToStream(Utf8JsonWriter writer)
    {
        var parameters = new OppParameters
        {
            AuthId = (int)Token.OperationId,
            AuthEvent = AuthenticationEventType.AuthorizeTransfer,
            AuthReplyMethod = AuthenticationReplyMethod.ReplyYesNo,
            TransferEvent = _fileTransferEvent,
            TimeoutMs = TimeoutMs,
        };

        writer.WritePropertyName(ModelEventSerializableContext.TransferAuthEventPropertyName);
        parameters.SerializeAll(writer, ModelEventSerializableContext.Default);
    }
}
