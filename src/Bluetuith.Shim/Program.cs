using System.Runtime.InteropServices;
using Bluetuith.Shim.DataTypes;
using Bluetuith.Shim.Operations;

namespace Bluetuith.Shim;

internal static class Program
{
    public static void Main(string[] args)
    {
#if WINDOWS10_0_19041_0_OR_GREATER
        BluetoothStack.CurrentStack = new Bluetuith.Shim.Stack.Microsoft.MSFTStack();
        ServerHost.Instance = new Bluetuith.Shim.Stack.Microsoft.MSFTServer();
#endif

        using var sigintHandler = PosixSignalRegistration.Create(PosixSignal.SIGINT, SignalHandler);
        ErrorData error = CommandExecutor.Run(args);
        if (error != Errors.ErrorNone)
        {
            Console.Write(error.ToConsoleString());
            Environment.Exit(1);
        }
    }

    private static async void SignalHandler(PosixSignalContext context)
    {
        context.Cancel = true;
        await CommandExecutor.StopAsync();
    }
}
