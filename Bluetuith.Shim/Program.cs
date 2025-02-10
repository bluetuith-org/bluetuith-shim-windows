using System.Runtime.InteropServices;
using Bluetuith.Shim.Executor.Command;
using Bluetuith.Shim.Types;

namespace Bluetuith.Shim;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        using var sigintHandler = PosixSignalRegistration.Create(PosixSignal.SIGINT, SignalHandler);
        ErrorData error = await CommandExecutor.RunAsync(args);
        if (error != Errors.ErrorNone)
        {
            Console.Write(error.ToConsoleString());
            Environment.Exit(1);
        }
    }

    private static async void SignalHandler(PosixSignalContext context)
    {
        context.Cancel = true;
        await Executor.Command.CommandExecutor.StopAsync();
    }
}
