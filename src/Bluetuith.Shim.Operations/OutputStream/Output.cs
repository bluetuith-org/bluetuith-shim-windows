using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Operations;

public static class Output
{
    private static OutputBase _output = new CommandOutput();
    public static bool IsOnSocket = false;

    public static ErrorData StartSocketServer(
        string socketPath,
        CancellationTokenSource waitForResume,
        OperationToken token
    )
    {
        try
        {
            _output = new SocketOutput(socketPath, waitForResume, token);
            IsOnSocket = true;

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

    public static bool ConfirmAuthentication<T>(T authEvent, OperationToken token)
        where T : AuthenticationEvent
    {
        Task.Run(() => _output.EmitAuthenticationRequest(authEvent, token));
        return authEvent.WaitForResponse();
    }

    public static bool ReplyToAuthenticationRequest(long operationId, string response)
    {
        return _output.SetAuthenticationResponse(operationId, response);
    }
}

public abstract class OutputBase
{
    public virtual int EmitResult<T>(T result, OperationToken token)
        where T : IResult
    {
        return Errors.ErrorNone.Code.Value;
    }

    public virtual int EmitError(ErrorData error, OperationToken token)
    {
        return error.Code.Value;
    }

    public virtual void EmitEvent<T>(T ev, OperationToken token, bool clientOnly = false)
        where T : IEvent { }

    public virtual void EmitAuthenticationRequest<T>(T authEvent, OperationToken token)
        where T : AuthenticationEvent { }

    public virtual bool SetAuthenticationResponse(long operationId, string response)
    {
        return true;
    }

    public virtual void WaitForClose() { }
}
