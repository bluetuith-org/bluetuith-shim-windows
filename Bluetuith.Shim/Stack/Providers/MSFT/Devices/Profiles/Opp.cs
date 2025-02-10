using System.Collections.Concurrent;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Types;
using GoodTimeStudio.MyPhone.OBEX.Opp;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using Windows.Devices.Bluetooth;
using static Bluetuith.Shim.Stack.Events.IFileTransferEvent;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices.Profiles;

internal static class Opp
{
    private static readonly OppSessions _sessions = new();

    internal static async Task<ErrorData> StartFileTransferSessionAsync(
        OperationToken token,
        string address,
        bool startQueue
    )
    {
        try
        {
            if (_sessions.GetClientSession(out _, out _))
                return Errors.ErrorDeviceFileTransferSession.AddMetadata(
                    "exception",
                    "An Object Push Client session is in progress"
                );

            var addr = BluetoothAddress.Parse(address);

            var _device = await DeviceUtils.GetBluetoothDeviceWithService(
                address,
                BluetoothService.ObexObjectPush
            );
            if (_device.ConnectionStatus == BluetoothConnectionStatus.Connected)
                return Errors.ErrorDeviceAlreadyConnected;

            var _client = new BluetoothOppClientSession(_device, new CancellationTokenSource());
            await _client.ConnectAsync();
            if (_client.ObexClient == null)
                throw new Exception("OPP ObexClient is null on connect");

            _sessions.Start(addr, new ClientSession(startQueue, addr, _client, _device, token));
        }
        catch (OperationCanceledException)
        {
            return Errors.ErrorOperationCancelled;
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceFileTransferSession.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }

    internal static (FileTransferModel, ErrorData) QueueFileSend(string filepath)
    {
        if (!_sessions.GetClientSession(out var client, out var error))
            return (default, error);

        return client.QueueFileSend(filepath);
    }

    internal static async Task<ErrorData> SendFileAsync(string filepath)
    {
        if (!_sessions.GetClientSession(out var client, out var error))
            return error;

        return await client.SendFileAsync(filepath);
    }

    internal static ErrorData CancelFileTransfer(string address)
    {
        try
        {
            var addr = BluetoothAddress.Parse(address);

            return OppSessions.CancelTransfer(addr);
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceFileTransferSession.AddMetadata("exception", e.Message);
        }
    }

    internal static ErrorData StopFileTransferSession()
    {
        if (!_sessions.GetClientSession(out var client, out var error))
            return error;

        return _sessions.Stop(client.Address);
    }

    internal static async Task<ErrorData> StartFileTransferServerAsync(
        OperationToken token,
        string destinationDirectory
    )
    {
        var (adapter, error) = AdapterModelExt.ConvertToAdapterModel();
        if (error != Errors.ErrorNone)
            return error;

        if (adapter.OptionPowered.TryGet(out var powered) && !powered)
            return Errors.ErrorDeviceFileTransferSession.AddMetadata(
                "exception",
                "The adapter is not powered on"
            );

        if (_sessions.GetServerSession(out var session, out _) && session.IsMainServer)
        {
            return Errors.ErrorOperationInProgress.WrapError(
                new()
                {
                    { "operation", "Object Push Server" },
                    { "exception", "An Object Push server instance is in progress" },
                }
            );
        }

        try
        {
            var _server = new BluetoothOppServerSession(
                token.LinkedCancelTokenSource,
                destinationDirectory
            );
            await _server.StartServerAsync();

            var addr = BluetoothAddress.Parse(adapter.Address);
            return _sessions.Start(addr, new ServerSession(true, addr, _server, token, null));
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            return Errors.ErrorDeviceFileTransferSession.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }

    internal static ErrorData StopFileTransferServer()
    {
        var (adapter, error) = AdapterModelExt.ConvertToAdapterModel();
        if (error != Errors.ErrorNone)
            return error;

        return _sessions.Stop(BluetoothAddress.Parse(adapter.Address));
    }
}

internal partial record class OppSessions : IDisposable
{
    private static readonly ConcurrentDictionary<BluetoothAddress, IOppSession> _sessions = [];

    internal ErrorData Start(BluetoothAddress address, IOppSession session)
    {
        if (!_sessions.TryAdd(address, session))
        {
            session.Dispose();
            return Errors.ErrorDeviceFileTransferSession.AddMetadata(
                "exception",
                "A session already exists with this address"
            );
        }

        session.RemoveSession = () => _sessions.TryRemove(address, out _);
        session.Sessions = () => this;

        session.Start();

        return Errors.ErrorNone;
    }

    internal ErrorData Stop(BluetoothAddress address)
    {
        if (!_sessions.TryRemove(address, out var session))
            return Errors.ErrorDeviceFileTransferSession.AddMetadata(
                "exception",
                "No session exists for Object Push"
            );

        session.Stop();

        return Errors.ErrorNone;
    }

    internal static ErrorData CancelTransfer(BluetoothAddress address)
    {
        if (!_sessions.TryGetValue(address, out var session))
            return Errors.ErrorDeviceFileTransferSession.AddMetadata(
                "exception",
                "No session exists for Object Push"
            );

        session.CancelTransfer();

        return Errors.ErrorNone;
    }

    internal bool Exists(BluetoothAddress address) => _sessions.TryGetValue(address, out _);

    internal bool GetClientSession(out ClientSession session, out ErrorData error)
    {
        error = Errors.ErrorNone;
        session = null;

        foreach (var (_, s) in _sessions)
        {
            if (s.IsClient)
            {
                session = s.Client;
                return true;
            }
        }

        error = Errors.ErrorDeviceFileTransferSession.AddMetadata(
            "exception",
            "No session exists for Object Push Client"
        );
        return false;
    }

    internal bool GetServerSession(out ServerSession session, out ErrorData error)
    {
        error = Errors.ErrorNone;
        session = null;

        foreach (var (_, s) in _sessions)
        {
            if (s.IsServer)
            {
                session = s.Server;
                return true;
            }
        }

        error = Errors.ErrorDeviceFileTransferSession.AddMetadata(
            "exception",
            "No session exists for Object Push Server"
        );
        return false;
    }

    public void Dispose()
    {
        foreach (var (_, s) in _sessions)
        {
            s.Stop();
        }
        _sessions.Clear();
    }
}

internal interface IOppSession : IDisposable
{
    public ClientSession Client { get; }
    public bool IsClient
    {
        get => Client != null;
    }

    public ServerSession Server { get; }
    public bool IsServer
    {
        get => Server != null;
    }

    public Action RemoveSession { get; set; }
    public Func<OppSessions> Sessions { get; set; }

    public void Start();
    public void Stop();
    public void CancelTransfer();
}

internal partial record class ServerSession(
    bool IsMainServer,
    BluetoothAddress Address,
    BluetoothOppServerSession Server,
    OperationToken ServerToken,
    Action CancelFunc
) : IOppSession
{
    private readonly string AddressString = Address.ToString("C");

    ClientSession IOppSession.Client => null;
    ServerSession IOppSession.Server => this;

    public Action RemoveSession { get; set; } = null;
    public Func<OppSessions> Sessions { get; set; } = null;

    private ConcurrentDictionary<string, BluetoothAddress> _transfers = null;

    public void Start()
    {
        if (!IsMainServer)
            return;

        OperationManager.MarkAsExtended(ServerToken);

        Server.ClientAccepted += (sender, args) =>
        {
            args.ObexServer.ReceiveTransferEventHandler += (s, e) =>
            {
                var addrstring = DeviceUtils.HostAddress(args.ClientInfo.HostName.RawName);
                AddSubSession(addrstring, () => args.ObexServer.CancelTransfer());

                Output.Event(
                    new FileTransferEvent()
                    {
                        Name = e.FileName,
                        FileName = e.TempFileName,
                        Address = addrstring,
                        FileSize = e.FileSize,
                        BytesTransferred = e.BytesTransferred,
                    },
                    ServerToken
                );
            };
        };
        Server.ClientDisconnected += (sender, args) =>
        {
            var addrstring = DeviceUtils.HostAddress(args.ClientInfo.HostName.RawName);
            RemoveSubSession(addrstring);

            if (args.ObexServerException != null)
            {
                Stop();

                Output.Event(
                    Errors.ErrorDeviceFileTransferSession.AddMetadata(
                        "exception",
                        args.ObexServerException.Message
                    ),
                    ServerToken
                );
            }
        };

        _ = Task.Run(() =>
        {
            ServerToken.Wait();
            Stop();
        });
    }

    public void Stop()
    {
        CancelTransfer();
        Dispose();
    }

    public void CancelTransfer()
    {
        CancelFunc?.Invoke();
    }

    public void Dispose()
    {
        if (IsMainServer)
        {
            Server.Dispose();
            ServerToken.Release();
        }

        RemoveSession?.Invoke();
    }

    private void AddSubSession(string address, Action cancelFunc)
    {
        _transfers ??= [];

        if (_transfers.TryGetValue(address, out _))
            return;

        var addr = BluetoothAddress.Parse(address);
        if (_transfers.TryAdd(address, addr))
            Sessions().Start(addr, new ServerSession(false, addr, null, ServerToken, cancelFunc));
    }

    private void RemoveSubSession(string address)
    {
        var addr = BluetoothAddress.Parse(address);
        if (_transfers?.TryRemove(address, out _) == true)
            Sessions().Stop(addr);
    }
}

internal partial record class ClientSession(
    bool StartQueue,
    BluetoothAddress Address,
    BluetoothOppClientSession Client,
    BluetoothDevice Device,
    OperationToken ClientToken
) : IOppSession
{
    private BlockingCollection<string> _filequeue = null;

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string AddressString = Address.ToString("C");

    ClientSession IOppSession.Client => this;
    ServerSession IOppSession.Server => null;

    public Action RemoveSession { get; set; } = null;
    public Func<OppSessions> Sessions { get; set; } = null;

    public void Start()
    {
        Client.ObexClient.TransferEventHandler += (s, e) =>
        {
            var fileTransferEvent = new FileTransferEvent()
            {
                Name = e.FileName,
                FileName = e.FileName,
                Address = AddressString,
                FileSize = e.FileSize,
                BytesTransferred = e.BytesTransferred,
            };

            if (e.TransferDone)
                fileTransferEvent.Status = e.Error ? TransferStatus.Error : TransferStatus.Complete;
            else
                fileTransferEvent.Status = TransferStatus.Active;

            Output.Event(fileTransferEvent, ClientToken);
        };

        OperationManager.MarkAsExtended(ClientToken);
        Device.ConnectionStatusChanged += (s, e) =>
        {
            if (s.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                Stop();
        };

        if (StartQueue)
            _ = Task.Run(() => FilesQueueProcessor());

        _ = Task.Run(() =>
        {
            ClientToken.Wait();
            Stop();
        });
    }

    public void Stop()
    {
        CancelClientFileTransfer();
        Dispose();
    }

    public void CancelTransfer()
    {
        CancelClientFileTransfer();
    }

    public async Task<ErrorData> SendFileAsync(string filepath)
    {
        try
        {
            _semaphore.Wait();
            var task = Client.ObexClient.SendFile(filepath);
            _semaphore.Release();

            if (!await task)
                return Errors.ErrorOperationCancelled;
        }
        catch (OperationCanceledException)
        {
            return Errors.ErrorOperationCancelled;
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceFileTransferSession.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }

    public (FileTransferModel, ErrorData) QueueFileSend(string filepath)
    {
        var fileTransferEvent = new FileTransferModel()
        {
            Name = Path.GetFileName(filepath),
            Status = TransferStatus.Error,
        };

        try
        {
            _filequeue.Add(filepath);
        }
        catch (Exception e)
        {
            return (
                fileTransferEvent,
                Errors.ErrorDeviceFileTransferSession.AddMetadata("exception", e.Message)
            );
        }

        return (
            fileTransferEvent with
            {
                FileName = filepath,
                Address = ((BluetoothAddress)(Device.BluetoothAddress)).ToString("C"),
                Status = TransferStatus.Queued,
            },
            Errors.ErrorNone
        );
    }

    private async Task FilesQueueProcessor()
    {
        try
        {
            foreach (var filepath in _filequeue.GetConsumingEnumerable())
            {
                if (Client == null || Client.ObexClient == null)
                    return;

                if (await SendFileAsync(filepath) is var error && error != Errors.ErrorNone)
                    Output.Event(error, ClientToken);
            }

            Client?.ObexClient?.Refresh();
            _filequeue = [];
        }
        catch { }
    }

    private ErrorData CancelClientFileTransfer()
    {
        try
        {
            _semaphore.Wait();

            Client?.ObexClient.CancelTransfer();
            _filequeue?.CompleteAdding();
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceFileTransferSession.AddMetadata("exception", e.Message);
        }
        finally
        {
            _semaphore.Release();
        }

        return Errors.ErrorNone;
    }

    public void Dispose()
    {
        _semaphore.Wait();

        _filequeue?.CompleteAdding();

        ClientToken.Release();

        Client?.Dispose();

        _semaphore.Release();

        RemoveSession?.Invoke();
    }
}
