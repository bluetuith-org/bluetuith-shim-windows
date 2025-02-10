using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Types;

namespace Bluetuith.Shim.Executor.OutputStream;

internal sealed class CommandOutput : OutputBase
{
    internal override int EmitResult<T>(T result, OperationToken token)
    {
        Console.Write(result.ToConsoleString());
        return base.EmitResult(result, token);
    }

    internal override int EmitError(ErrorData error, OperationToken token)
    {
        Console.Write(error.ToConsoleString());
        return base.EmitError(error, token);
    }

    internal override void EmitEvent<T>(T ev, OperationToken token)
    {
        Console.Write(ev.ToConsoleString());
    }

    internal override void EmitAuthenticationRequest<T>(T authEvent, OperationToken token)
    {
        if (Readline.TryReadInput(authEvent.ToConsoleString(), authEvent.TimeoutMs, out var input))
        {
            authEvent.SetResponse(input.Trim());
        }
    }
}

internal static class Readline
{
    private static readonly AutoResetEvent _readHandle = new(false);
    private static readonly AutoResetEvent _waitHandle = new(false);
    private static readonly Thread _inputThread;
    private static string _input = "";

    static Readline()
    {
        _input = "";

        _inputThread = new Thread(reader) { IsBackground = true };
        _inputThread.Start();
    }

    internal static bool TryReadInput(string prompt, int timeout, out string input)
    {
        Console.Write(prompt + " ");
        input = "";

        _readHandle.Set();
        if (_waitHandle.WaitOne(timeout))
        {
            input = _input;
            return true;
        }

        return false;
    }

    private static void reader()
    {
        while (true)
        {
            _readHandle.WaitOne();
            _input = Console.ReadLine() ?? "";
            _waitHandle.Set();
        }
    }
}
