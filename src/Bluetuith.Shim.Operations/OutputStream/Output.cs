using Bluetuith.Shim.DataTypes;
using static Bluetuith.Shim.Operations.AuthenticationManager;

namespace Bluetuith.Shim.Operations;

public static class Output
{
    private static OutputBase _output = new CommandOutput();

    private static bool _isOnSocket;
    public static bool IsOnSocket
    {
        get => _isOnSocket;
    }

    public static event Action<string> OnStarted;

    public static ErrorData StartSocketServer(string socketPath, OperationToken token)
    {
        try
        {
            _output = new SocketOutput(socketPath, token);
            _isOnSocket = true;

            OnStarted?.Invoke(socketPath);
            _output.WaitForClose();
        }
        catch (Exception e)
        {
            return Errors.ErrorOperationCancelled.WrapError(new() { { "exception", e.Message } });
        }

        return Errors.ErrorNone;
    }

    public static int Result<T>(T result, ErrorData error, OperationToken token)
        where T : IResult
    {
        if (error != Errors.ErrorNone)
        {
            return Error(error, token);
        }

        IResult outputResult = result;
        return _output.EmitResult(outputResult, token);
    }

    public static int Error(ErrorData error, OperationToken token)
    {
        return _output.EmitError(error, token);
    }

    public static void Event<T>(T ev, OperationToken token)
        where T : IEvent
    {
        _output.EmitEvent(ev, token);
    }

    public static void ClientEvent<T>(T ev, OperationToken token)
        where T : IEvent
    {
        _output.EmitEvent(ev, token, true);
    }

    public static bool ConfirmAuthentication<T>(
        T authEvent,
        AuthAgentType authAgentType = AuthAgentType.None
    )
        where T : AuthenticationEvent
    {
        _ = Task.Run(() =>
            _output.EmitAuthenticationRequest(authEvent, authEvent.Token, authAgentType)
        );
        return authEvent.WaitForResponse();
    }

    public static bool ReplyToAuthenticationRequest(
        OperationToken token,
        long authId,
        string response
    )
    {
        return _output.SetAuthenticationResponse(token, authId, response);
    }
}

internal abstract class OutputBase
{
    internal virtual int EmitResult<T>(T result, OperationToken token)
        where T : IResult
    {
        return Errors.ErrorNone.Code.Value;
    }

    internal virtual int EmitError(ErrorData error, OperationToken token)
    {
        return error.Code.Value;
    }

    internal virtual void EmitEvent<T>(T ev, OperationToken token, bool clientOnly = false)
        where T : IEvent { }

    internal virtual void EmitAuthenticationRequest<T>(
        T authEvent,
        OperationToken token,
        AuthAgentType authAgentType = AuthAgentType.None
    )
        where T : AuthenticationEvent { }

    internal virtual bool SetAuthenticationResponse(
        OperationToken token,
        long authId,
        string response
    )
    {
        return true;
    }

    internal virtual void WaitForClose() { }
}
