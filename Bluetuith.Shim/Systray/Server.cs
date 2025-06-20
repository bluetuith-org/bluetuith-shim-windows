using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Stack;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices.Profiles;
using Bluetuith.Shim.Types;
using H.NotifyIcon.Core;

namespace Bluetuith.Shim.Systray;

internal static class Server
{
    internal static ErrorData Launch(string SocketPath, string newProcessArgs)
    {
        try
        {
            using var mutex = GetGlobalMutex(out var gotHandle);
            if (newProcessArgs != "")
            {
                RelaunchProcess(newProcessArgs);
                return Errors.ErrorNone;
            }

            var token = OperationManager.GenerateToken(0);
            using var trayIcon = new Tray(token, SocketPath);

            OperationManager.AddToken(token);
            var resumeToken = new CancellationTokenSource();
            _ = Task.Run(async () =>
                await AvailableStacks.CurrentStack.SetupWatchers(token, resumeToken)
            );

            trayIcon.ShowInfo($"The shim RPC server instance has started at socket '{SocketPath}'");

            ErrorData error = Opp.StartFileTransferServerAsync(token).GetAwaiter().GetResult();
            if (error != Errors.ErrorNone)
            {
                trayIcon.ShowError(error);
            }

            error = Output.StartSocketServer(SocketPath, resumeToken, token);
            OperationManager.CancelAllAsync().Wait();
            if (error != Errors.ErrorNone)
            {
                trayIcon.ShowError(error);
            }

            return error;
        }
        catch (Exception ex)
        {
            return Errors.ErrorUnexpected.AddMetadata("exception", ex.Message);
        }
    }

    internal static ErrorData Stop(string newProcessArgs)
    {
        try
        {
            if (newProcessArgs != "")
            {
                RelaunchProcess(newProcessArgs);
                return Errors.ErrorNone;
            }

            ConsoleNativeMethods.StopProgram();
        }
        catch (Exception ex)
        {
            return Errors.ErrorUnexpected.AddMetadata("exception", ex.Message);
        }

        return Errors.ErrorNone;
    }

    private static void RelaunchProcess(string newProcessArgs)
    {
        var p = new Process();
        p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

        var si = p.StartInfo;
        si.UseShellExecute = true;
        si.CreateNoWindow = true;
        si.FileName = Environment.ProcessPath;
        si.Verb = "runas";
        si.Arguments = newProcessArgs;

        p.Start();
    }

    private static Mutex GetGlobalMutex(out bool gotHandle)
    {
        var executingAssembly = Assembly.GetExecutingAssembly();
        var customAttributes = executingAssembly.GetCustomAttributes(typeof(GuidAttribute), false);
        var guidAttribute = ((GuidAttribute)customAttributes[0]).Value.ToString();
        var mutexId = string.Format("Global\\{{{0}}}", guidAttribute);

        var mutex = new Mutex(false, mutexId, out _);
        try
        {
            gotHandle = mutex.WaitOne(500, false);
        }
        catch (AbandonedMutexException)
        {
            gotHandle = true;
        }

        if (!gotHandle)
            throw new Exception("Cannot start a new instance. Server is already running.");

        return mutex;
    }
}

internal class Tray : IDisposable
{
    private readonly OperationToken _token;
    private readonly TrayIconWithContextMenu _tray;

    private readonly Stream _iconStream;
    private readonly Icon _icon;

    public TrayIconWithContextMenu TrayInstance
    {
        get { return _tray; }
    }

    internal Tray(OperationToken token, string SocketPath)
    {
        _token = token;
        _iconStream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream("Bluetuith.Shim.Resources.shimicon.ico");
        _icon = new Icon(_iconStream);

        var trayIcon = new TrayIconWithContextMenu
        {
            Icon = _icon.Handle,
            ToolTip = $"Bluetuith Shim RPC server\n\nStarted at socket '{SocketPath}'",
            ContextMenu = new PopupMenu()
            {
                Items = { new PopupMenuItem("Stop", (_, _) => token.Release()) },
            },
        };

        trayIcon.Create();
        Thread.Sleep(1000);

        _tray = trayIcon;
    }

    internal void ShowInfo(string info)
    {
        _tray.ShowNotification(
            title: nameof(NotificationIcon.Info),
            message: info,
            icon: NotificationIcon.Info
        );
    }

    internal void ShowError(ErrorData error)
    {
        var errmsg = "An unknown error occurred";
        if (error.Metadata.TryGetValue("exception", out var message))
            errmsg = message.ToString();

        _tray.ShowNotification(
            title: nameof(NotificationIcon.Error),
            message: errmsg,
            icon: NotificationIcon.Error
        );
    }

    public void Dispose()
    {
        _token.Release();

        _icon.Dispose();
        _iconStream.Dispose();

        _tray.Dispose();
    }
}

/*
 * Taken from: https://stackoverflow.com/a/15281070
 */
internal static class ConsoleNativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

    delegate bool ConsoleCtrlDelegate(CtrlTypes CtrlType);

    // Enumerated type for the control messages sent to the handler routine
    enum CtrlTypes : uint
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT,
        CTRL_CLOSE_EVENT,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT,
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GenerateConsoleCtrlEvent(
        CtrlTypes dwCtrlEvent,
        uint dwProcessGroupId
    );

    // Modified from the original linked answer.
    internal static void StopProgram()
    {
        var thisPid = Environment.ProcessId;
        var notAttached = 0;

        try
        {
            SetConsoleCtrlHandler(null, true);

            foreach (
                var proc in Process.GetProcessesByName(
                    Path.GetFileNameWithoutExtension(Environment.ProcessPath)
                )
            )
            {
                using (proc)
                {
                    if (proc is null || proc.Id == thisPid)
                        continue;

                    try
                    {
                        if (FreeConsole() && AttachConsole((uint)proc.Id))
                        {
                            if (GenerateConsoleCtrlEvent(CtrlTypes.CTRL_C_EVENT, 0))
                                proc.WaitForExit(2000);
                            else
                                notAttached++;
                        }
                        else
                        {
                            notAttached++;
                        }
                    }
                    catch
                    {
                        notAttached++;
                    }
                }
            }
        }
        catch
        {
            notAttached++;
        }
        finally
        {
            SetConsoleCtrlHandler(null, false);
        }

        if (notAttached > 0)
            throw new Exception("Could not attach to and send signal to processes");
    }
}
