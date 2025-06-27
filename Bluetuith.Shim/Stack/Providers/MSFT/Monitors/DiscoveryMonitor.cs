using System.Collections.Concurrent;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Stack.Data.Events;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Types;
using Windows.Devices.Bluetooth;
using static Bluetuith.Shim.Types.IEvent;
using Lock = DotNext.Threading.Lock;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Monitors;

internal static class DiscoveryMonitor
{
    private static readonly ConcurrentDictionary<Guid, OperationToken> clients = [];
    private static Monitor monitor = new(BluetoothDevice.GetDeviceSelectorFromPairingState(false));
    private static readonly Lock @lock = Lock.Semaphore(new(1, 1));
    private static CancellationTokenSource stopTimeout = null;

    public static bool IsStarted(OperationToken token)
    {
        return clients.ContainsKey(token.ClientId);
    }

    internal static ErrorData Start(OperationToken token)
    {
        if (!@lock.TryAcquire(out var holder))
            return Errors.ErrorOperationInProgress;

        using var sem = holder;

        var adapterEvent = new AdapterEvent();
        var error = adapterEvent.MergeRadioEventData();
        if (error != Errors.ErrorNone)
            return error;

        if (!token.HasClientId)
            return Errors.ErrorUnexpected.AddMetadata(
                "exception",
                "Token does not contain a client ID"
            );

        if (clients.ContainsKey(token.ClientId))
            return Errors.ErrorDeviceDiscovery.AddMetadata(
                "exception",
                "Device discovery is already running"
            );

        clients.TryAdd(token.ClientId, token);

        if (stopTimeout != null)
        {
            stopTimeout.Cancel();
            stopTimeout.Dispose();
            stopTimeout = null;
        }

        try
        {
            _ = Task.Run(() =>
            {
                Output.ClientEvent(
                    adapterEvent with
                    {
                        OptionDiscovering = true,
                        Action = EventAction.Updated,
                    },
                    token
                );

                using var subscriber = Subscriber(monitor, token);

                token.Wait();

                Output.ClientEvent(
                    adapterEvent with
                    {
                        OptionDiscovering = false,
                        Action = EventAction.Updated,
                    },
                    token
                );
            });

            if (!monitor.Start())
                return Errors.ErrorDeviceDiscovery.AddMetadata(
                    "exception",
                    "The device discovery service is being reset"
                );
        }
        catch (Exception e)
        {
            StopInternal(token, true, out _);
            return Errors.ErrorDeviceDiscovery.AddMetadata("exception", e.Message);
        }

        foreach (var device in monitor.CurrentDevices)
        {
            if (device.OptionPaired.HasValue && !device.OptionPaired.Value)
                Output.ClientEvent(device with { Action = EventAction.Added }, token);
        }

        OperationManager.SetOperationProperties(token, true, true);

        return Errors.ErrorNone;
    }

    internal static ErrorData Stop(OperationToken token)
    {
        if (!@lock.TryAcquire(out var holder))
            return Errors.ErrorOperationInProgress;

        CancellationToken tok = default;
        using (holder)
        {
            if (!StopInternal(token, false, out var cancellationToken))
                return Errors.ErrorDeviceDiscovery.AddMetadata(
                    "exception",
                    "Device discovery is not running"
                );

            tok = cancellationToken;
        }

        if (tok != default)
            monitor.Stop(stopTimeout.Token);

        return Errors.ErrorNone;
    }

    internal static bool StopInternal(
        OperationToken token,
        bool forceStop,
        out CancellationToken cancellationToken
    )
    {
        cancellationToken = default;

        if (!clients.TryRemove(token.ClientId, out var tok))
            return false;

        if (forceStop)
        {
            token.Release();

            monitor.Dispose();
            monitor = new(BluetoothDevice.GetDeviceSelectorFromPairingState(false));

            return true;
        }

        tok.Release();
        foreach (var device in monitor.CurrentDevices)
        {
            if (device.OptionPaired.HasValue && !device.OptionPaired.Value)
                Output.ClientEvent(device with { Action = EventAction.Removed }, token);
        }

        if (clients.IsEmpty && stopTimeout == null)
        {
            stopTimeout = new();
            cancellationToken = stopTimeout.Token;
        }

        return true;
    }

    private static Monitor.Subscriber Subscriber(Monitor monitor, OperationToken token)
    {
        return new Monitor.Subscriber(monitor, token.OperationId)
        {
            OnAdded = (info) =>
            {
                if (info.OptionPaired.HasValue && !info.OptionPaired.Value)
                    Output.ClientEvent(info, token);
            },

            OnUpdated = (oldInfo, newInfo) =>
            {
                if (newInfo.OptionPaired.HasValue && !newInfo.OptionPaired.Value)
                    Output.ClientEvent(newInfo, token);
            },

            OnRemoved = (info) =>
            {
                if (info.OptionPaired.HasValue && !info.OptionPaired.Value)
                    Output.ClientEvent(info, token);
            },
        };
    }
}
