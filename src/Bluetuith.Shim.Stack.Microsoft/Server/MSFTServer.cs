using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Bluetuith.Shim.DataTypes;
using Bluetuith.Shim.Operations;
using H.NotifyIcon.Core;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Threading;

namespace Bluetuith.Shim.Stack.Microsoft;

public class MSFTServer : IServer
{
    public ErrorData StartServer(string SocketPath, string newProcessArgs)
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
            _ = Task.Run(async () => await Watchers.Setup(token, resumeToken));

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
            PInvoke.MessageBox(
                (HWND)null,
                ex.Message,
                IServer.Name,
                Windows.Win32.UI.WindowsAndMessaging.MESSAGEBOX_STYLE.MB_ICONERROR
            );

            return Errors.ErrorUnexpected.AddMetadata("exception", ex.Message);
        }
    }

    public ErrorData StopServer(string newProcessArgs)
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

    private Mutex GetGlobalMutex(out bool gotHandle)
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

    public bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public bool ShouldRelaunch()
    {
        try
        {
            var process = ProcessUtilities.GetParentProcess();
            if (process.ProcessName != "explorer")
                return false;

            PInvoke.FreeConsole();

            new Server().Start(null, "", false);
        }
        catch { }

        return true;
    }
}

internal partial class Tray : IDisposable
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

internal static class ProcessUtilities
{
    public static unsafe Process GetParentProcess()
    {
        uint returnLength = 0;

        var info = new PROCESS_BASIC_INFORMATION();
        PROCESS_BASIC_INFORMATION* infoPtr = &info;

        var status = Windows.Wdk.PInvoke.NtQueryInformationProcess(
            new(Process.GetCurrentProcess().Handle),
            Windows.Wdk.System.Threading.PROCESSINFOCLASS.ProcessBasicInformation,
            infoPtr,
            (uint)Unsafe.SizeOf<PROCESS_BASIC_INFORMATION>(),
            ref returnLength
        );

        if (status.SeverityCode != NTSTATUS.Severity.Success)
            throw new Exception(status.SeverityCode.ToString());

        return Process.GetProcessById((int)info.InheritedFromUniqueProcessId);
    }
}

/*
 * Taken from: https://stackoverflow.com/a/15281070
 */
internal static class ConsoleNativeMethods
{
    // Enumerated type for the control messages sent to the handler routine
    enum CtrlTypes : uint
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT,
        CTRL_CLOSE_EVENT,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT,
    }

    // Modified from the original linked answer.
    internal unsafe static void StopProgram()
    {
        var thisPid = Environment.ProcessId;
        var notAttached = 0;

        try
        {
            PInvoke.SetConsoleCtrlHandler(null, true);

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
                        if (PInvoke.FreeConsole() && PInvoke.AttachConsole((uint)proc.Id))
                        {
                            if (PInvoke.GenerateConsoleCtrlEvent((uint)CtrlTypes.CTRL_C_EVENT, 0))
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
            PInvoke.SetConsoleCtrlHandler(null, false);
        }

        if (notAttached > 0)
            throw new Exception("Could not attach to and send signal to processes");
    }
}
