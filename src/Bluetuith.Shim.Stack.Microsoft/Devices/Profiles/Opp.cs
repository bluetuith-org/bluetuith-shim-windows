using System.Collections.Concurrent;
using Bluetuith.Shim.DataTypes;
using Bluetuith.Shim.Operations;
using GoodTimeStudio.MyPhone.OBEX.Opp;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using static Bluetuith.Shim.DataTypes.IFileTransferEvent;

namespace Bluetuith.Shim.Stack.Microsoft;

internal static class Opp
{
    private static readonly OppSessions _sessions = new();

    private static readonly string transferCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "bh-shim",
        "transfers"
    );

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

            var _client = new BluetoothOppClientSession(_device, new CancellationTokenSource());
            await _client.ConnectAsync();
            if (_client.ObexClient == null)
                throw new Exception("OPP ObexClient is null on connect");

            _sessions.Start(addr, new ClientSession(startQueue, addr, _client, token));
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

            return _sessions.CancelTransfer(addr);
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
        string destinationDirectory = ""
    )
    {
        if (destinationDirectory == "")
            destinationDirectory = transferCacheDir;

        if (!Path.Exists(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        var (adapter, error) = AdapterModelExt.ConvertToAdapterModel();
        if (error != Errors.ErrorNone)
            return error;

        if (adapter.OptionPowered != true)
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
                destinationDirectory,
                (data) =>
                {
                    var fileTransferEvent = new FileTransferEvent()
                    {
                        Name = data.FileName,
                        FileName = data.FilePath,
                        Address = DeviceUtils.HostAddress(data.HostName),
                        FileSize = data.FileSize,
                        BytesTransferred = data.BytesTransferred,
                        Status = TransferStatus.Queued,
                        Action = IEvent.EventAction.Added,
                    };

                    var tok = OperationManager.GenerateToken(0, token.ClientId);
                    OperationManager.SetOperationProperties(tok, true, true);

                    var authEvent = new WindowsOppAuthEvent(10000, fileTransferEvent, tok);

                    return Output.ConfirmAuthentication(authEvent, tok);
                }
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
    private readonly ConcurrentDictionary<BluetoothAddress, IOppSession> _sessions = [];

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

    internal ErrorData CancelTransfer(BluetoothAddress address)
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
    ClientSession IOppSession.Client => null;
    ServerSession IOppSession.Server => this;

    public Action RemoveSession { get; set; } = null;
    public Func<OppSessions> Sessions { get; set; } = null;

    private ConcurrentDictionary<string, BluetoothAddress> _transfers = null;

    public void Start()
    {
        if (!IsMainServer)
            return;

        OperationManager.SetOperationProperties(ServerToken);

        Server.ClientAccepted += (sender, args) =>
        {
            var address = DeviceUtils.HostAddress(args.ClientInfo.HostName.RawName);
            var fileTransferEvent = new FileTransferEvent();

            AddSubSession(address, () => args.ObexServer.StopServer());

            args.ObexServer.ReceiveTransferEventHandler += (s, e) =>
            {
                fileTransferEvent.Name = e.FileName;
                fileTransferEvent.FileName = e.FilePath;
                fileTransferEvent.Address = address;
                fileTransferEvent.FileSize = e.FileSize;
                fileTransferEvent.BytesTransferred = e.BytesTransferred;
                fileTransferEvent.Status = TransferStatus.Active;
                fileTransferEvent.Action = IEvent.EventAction.Updated;

                if (e.Error)
                {
                    fileTransferEvent.Status = TransferStatus.Error;
                }
                else
                {
                    if (e.Queued)
                    {
                        fileTransferEvent.Status = TransferStatus.Queued;
                        fileTransferEvent.Action = IEvent.EventAction.Added;
                    }

                    if (e.TransferDone || e.SessionClosed)
                        fileTransferEvent.Status = TransferStatus.Complete;
                }

                Output.Event(fileTransferEvent, ServerToken);

                if (e.SessionClosed)
                    Output.Event(
                        fileTransferEvent with
                        {
                            Action = IEvent.EventAction.Removed,
                        },
                        ServerToken
                    );
                ;
            };
        };
        Server.ClientDisconnected += (sender, args) =>
        {
            RemoveSubSession(DeviceUtils.HostAddress(args.ClientInfo.HostName.RawName));
            args.ObexServer.ReceiveTransferEventHandler = null;

            if (args.ObexServerException != null)
            {
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
        var fileTransferEvent = new FileTransferEvent();

        Client.ObexClient.TransferEventHandler += (s, e) =>
        {
            fileTransferEvent.Name = e.FileName;
            fileTransferEvent.FileName = e.FileName;
            fileTransferEvent.Address = AddressString;
            fileTransferEvent.FileSize = e.FileSize;
            fileTransferEvent.BytesTransferred = e.BytesTransferred;
            fileTransferEvent.Status = TransferStatus.Active;
            fileTransferEvent.Action = IEvent.EventAction.Updated;

            if (e.BytesTransferred == 0 && e.BytesTransferred < e.FileSize)
                fileTransferEvent.Action = IEvent.EventAction.Added;

            if (e.Error)
            {
                fileTransferEvent.Status = TransferStatus.Error;
            }
            else
            {
                if (e.TransferDone || e.SessionClosed)
                    fileTransferEvent.Status = TransferStatus.Complete;
            }

            Output.Event(fileTransferEvent, ClientToken);

            if (e.SessionClosed)
                Output.Event(
                    fileTransferEvent with
                    {
                        Action = IEvent.EventAction.Removed,
                    },
                    ClientToken
                );
        };

        OperationManager.SetOperationProperties(ClientToken);

        if (StartQueue)
        {
            _filequeue = [];
            _ = Task.Run(() => FilesQueueProcessor());
        }

        _ = Task.Run(() =>
        {
            ClientToken.Wait();
            Client.ObexClient.TransferEventHandler = null;

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
        long size;
        var fileTransferEvent = new FileTransferModel()
        {
            Address = AddressString,
            Name = Path.GetFileName(filepath),
            Status = TransferStatus.Error,
        };

        try
        {
            var info = new FileInfo(filepath);
            size = info.Length;

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
                Address = AddressString,
                Status = TransferStatus.Queued,
                FileSize = size,
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
