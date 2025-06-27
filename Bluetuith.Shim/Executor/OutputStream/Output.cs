using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Data.Events;
using Bluetuith.Shim.Types;

namespace Bluetuith.Shim.Executor.OutputStream;

internal static class Output
{
    private static OutputBase _output = new CommandOutput();
    internal static bool IsOnSocket = false;

    internal static ErrorData StartSocketServer(
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

    internal static int Result<T>(T result, ErrorData error, OperationToken token)
        where T : IResult
    {
        if (error != Errors.ErrorNone)
        {
            return Error(error, token);
        }

        IResult outputResult = result;
        return _output.EmitResult(outputResult, token);
    }

    internal static int Error(ErrorData error, OperationToken token)
    {
        return _output.EmitError(error, token);
    }

    internal static void Event<T>(T ev, OperationToken token)
        where T : IEvent
    {
        _output.EmitEvent(ev, token);
    }

    internal static void ClientEvent<T>(T ev, OperationToken token)
        where T : IEvent
    {
        _output.EmitEvent(ev, token, true);
    }

    internal static bool ConfirmAuthentication<T>(T authEvent, OperationToken token)
        where T : AuthenticationEvent
    {
        Task.Run(() => _output.EmitAuthenticationRequest(authEvent, token));
        return authEvent.WaitForResponse();
    }

    internal static bool ReplyToAuthenticationRequest(long operationId, string response)
    {
        return _output.SetAuthenticationResponse(operationId, response);
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

    internal virtual void EmitAuthenticationRequest<T>(T authEvent, OperationToken token)
        where T : AuthenticationEvent { }

    internal virtual bool SetAuthenticationResponse(long operationId, string response)
    {
        return true;
    }

    internal virtual void WaitForClose() { }
}
