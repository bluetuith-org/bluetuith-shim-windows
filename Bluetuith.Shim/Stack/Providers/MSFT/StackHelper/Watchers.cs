using System.Collections.Concurrent;
using System.Management;
using System.Text;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices;
using Bluetuith.Shim.Types;
using DotNext;
using DotNext.Threading;
using Microsoft.Win32;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using static Bluetuith.Shim.Types.IEvent;
using Lock = DotNext.Threading.Lock;

namespace Bluetuith.Shim.Stack.Providers.MSFT.StackHelper;

internal static class Watchers
{
    private static CancellationTokenSource _resume;

    private static bool _isAdapterWatcherRunning = false;
    private static bool _isDeviceWatcherRunning = false;
    private static bool _isBatteryWatcherRunning = false;

    internal static async Task<ErrorData> Setup(
        OperationToken token,
        CancellationTokenSource waitForResume
    )
    {
        _resume = waitForResume;

        try
        {
            while (!HostRadio.IsOperable)
            {
                if (token.IsReleased())
                {
                    return Errors.ErrorNone;
                }

                await Task.Delay(1000);
            }

            await Task.WhenAll(
                new List<Task>()
                {
                    Task.Run(() => SetupAdapterWatcher(token)),
                    Task.Run(() => SetupDevicesWatcher(token, _resume)),
                    Task.Run(() => SetupBatteryWatcher(token)),
                }
            );
        }
        catch (AggregateException ae)
        {
            return Errors.ErrorUnexpected.WrapError(new() { { "exception", ae.Message } });
        }

        return Errors.ErrorNone;
    }

    private static void SetupAdapterWatcher(OperationToken token)
    {
        if (_isAdapterWatcherRunning)
            return;

        if (!_resume.IsCancellationRequested)
            _resume.Token.WaitHandle.WaitOne();

        if (token.CancelTokenSource.IsCancellationRequested)
            return;

        try
        {
            if (!Devcon.FindByInterfaceGuid(HostRadio.DeviceInterface, out PnPDevice pnpDevice))
                return;

            var regPath = PnPInformation.Adapter.DeviceParametersRegistryPath(pnpDevice.DeviceId);
            using var watcher = new RegistryWatcher(
                "HKEY_LOCAL_MACHINE",
                PnPInformation.Adapter.DeviceParametersRegistryPath(pnpDevice.DeviceId),
                [
                    PnPInformation.Adapter.RadioStateRegistryKey,
                    PnPInformation.Adapter.DiscoverableRegistryKey,
                ],
                (s, e) =>
                {
                    try
                    {
                        var powered = Optional<bool>.None;
                        var discoverable = Optional<bool>.None;

                        var keyName = e.NewEvent.Properties["KeyPath"].Value as string;
                        var valueName = e.NewEvent.Properties["ValueName"].Value as string;

                        using (RegistryKey key = Registry.LocalMachine.OpenSubKey(keyName, false))
                        {
                            if (key == null)
                                return;

                            switch (valueName)
                            {
                                case var _
                                    when valueName == PnPInformation.Adapter.RadioStateRegistryKey:
                                    if (key.GetValue(valueName) is not int radioValue)
                                        return;

                                    powered = Convert.ToInt32(radioValue) == 2;
                                    break;

                                case var _
                                    when valueName
                                        == PnPInformation.Adapter.DiscoverableRegistryKey:
                                    switch (key.GetValue(valueName))
                                    {
                                        case byte[] discoverableValue:
                                            discoverable =
                                                Convert.ToInt32(discoverableValue[0]) > 0;
                                            break;

                                        case int discoverableInt:
                                            discoverable = discoverableInt > 0;
                                            break;

                                        default:
                                            return;
                                    }

                                    break;
                            }
                        }

                        var address = pnpDevice.GetProperty<ulong>(PnPInformation.Adapter.Address);
                        var adapter = new AdapterModelExt(address, powered, discoverable);
                        Output.Event(adapter.ToEvent(EventAction.Updated), token);
                    }
                    catch { }
                }
            );

            watcher.Start();
            _isAdapterWatcherRunning = true;

            token.Wait();

            watcher.Stop();
        }
        finally
        {
            _isAdapterWatcherRunning = false;
        }
    }

    private static void SetupBatteryWatcher(OperationToken token)
    {
        if (_isBatteryWatcherRunning)
            return;

        if (!_resume.IsCancellationRequested)
            _resume.Token.WaitHandle.WaitOne();

        if (token.CancelTokenSource.IsCancellationRequested)
            return;

        try
        {
            using var watcher = new RegistryWatcher(
                "HKEY_LOCAL_MACHINE",
                PnPInformation.HFEnumeratorRegistryKey,
                (s, e) =>
                {
                    try
                    {
                        var roothPathEvent = e.NewEvent.Properties["RootPath"];
                        if (roothPathEvent != null && roothPathEvent.Value is string rootPath)
                        {
                            var instances = 0;
                            while (
                                Devcon.FindByInterfaceGuid(
                                    PnPInformation.HFEnumeratorInterfaceGuid,
                                    out var pnpDevice,
                                    instances++,
                                    HostRadio.IsOperable
                                )
                            )
                            {
                                if (
                                    Convert.ToInt32(
                                        pnpDevice.GetProperty<byte>(
                                            PnPInformation.Device.BatteryPercentage
                                        )
                                    )
                                        is int batteryPercentage
                                    && batteryPercentage > 0
                                )
                                {
                                    var (device, error) = DeviceModelExt.ConvertToDeviceModel(
                                        pnpDevice.DeviceId,
                                        batteryPercentage
                                    );
                                    if (error == Errors.ErrorNone)
                                    {
                                        Output.Event(device.ToEvent(EventAction.Updated), token);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
            );

            watcher.Start();
            _isBatteryWatcherRunning = true;

            token.Wait();

            watcher.Stop();
        }
        finally
        {
            _isBatteryWatcherRunning = false;
        }
    }

    internal static void SetupDevicesWatcher(OperationToken token, CancellationTokenSource resume)
    {
        if (_isDeviceWatcherRunning)
            return;

        try
        {
            if (token.CancelTokenSource.IsCancellationRequested)
                return;

            TypedEventHandler<DeviceWatcher, DeviceInformation> addedEvent = new(
                (s, e) =>
                {
                    var (device, error) = DeviceModelExt.ConvertToDeviceModel(e, true);
                    if (
                        error == Errors.ErrorNone
                        && device.ToEvent(EventAction.Added) is var devent
                        && !UnpairedDevicesWatcher.AddDevice(e, devent)
                    )
                    {
                        Output.Event(device.ToEvent(EventAction.Added), token);
                    }
                }
            );
            TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> removedEvent = new(
                (s, e) =>
                {
                    var (device, error) = DeviceModelExt.ConvertToDeviceModel(e);
                    if (
                        error == Errors.ErrorNone
                        && device.ToEvent(EventAction.Removed) is var devent
                        && !UnpairedDevicesWatcher.RemoveDevice(e, devent)
                    )
                    {
                        Output.Event(device.ToEvent(EventAction.Removed), token);
                    }
                }
            );
            TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> updatedEvent = new(
                (s, e) =>
                {
                    var (device, error) = DeviceModelExt.ConvertToDeviceModel(e);
                    if (
                        error == Errors.ErrorNone
                        && device.ToEvent(EventAction.Updated) is var devent
                        && !UnpairedDevicesWatcher.UpdateDevice(e, devent)
                    )
                    {
                        Output.Event(devent, token);
                    }
                }
            );

            DeviceWatcher watcher = DeviceInformation.CreateWatcher(
                "System.Devices.DevObjectType:=5 AND System.Devices.Aep.ProtocolId:={E0CBF06C-CD8B-4647-BB8A-263B43F0F974}",
                [
                    "System.ItemNameDisplay",
                    "System.Devices.Aep.DeviceAddress",
                    "System.Devices.Aep.IsConnected",
                    "System.Devices.Aep.IsPaired",
                    "System.Devices.Aep.Category",
                    "System.Devices.Aep.Manufacturer",
                    "System.Devices.Aep.SignalStrength",
                ]
            );

            watcher.Added += addedEvent;
            watcher.Removed += removedEvent;
            watcher.Updated += updatedEvent;

            watcher.Start();
            _isDeviceWatcherRunning = true;

            token.Wait();

            watcher.Stop();
            watcher.Added -= addedEvent;
            watcher.Removed -= removedEvent;
            watcher.Updated -= updatedEvent;
        }
        finally
        {
            _isDeviceWatcherRunning = false;
        }
    }
}

internal static class UnpairedDevicesWatcher
{
    private record struct UnpairedDevice(DeviceInformation Info, DeviceEvent Event);

    private static Atomic<OperationToken> _token = new();
    private static readonly Lock _discoverylock = new();

    private static Dictionary<string, UnpairedDevice> _devices = [];
    private static BlockingCollection<DeviceEvent> _events = [];

    static UnpairedDevicesWatcher()
    {
        _token.Write(OperationToken.None);
    }

    internal static bool IsStarted
    {
        get
        {
            _token.Read(out var tok);
            return tok != OperationToken.None;
        }
    }

    internal static async Task<ErrorData> StartEmitter(OperationToken token)
    {
        _token.Read(out var tok);
        if (tok != OperationToken.None)
            return Errors.ErrorUnexpected.AddMetadata(
                "exception",
                "Device discovery is already running."
            );

        var (adapter, error) = AdapterModelExt.ConvertToAdapterModel();
        if (error != Errors.ErrorNone)
        {
            return error;
        }

        var t = Task.Run(() => Watchers.SetupDevicesWatcher(token, default));
        await Task.WhenAny(t, Task.Delay(500));
        if (t.IsFaulted)
            throw t.Exception;

        _token.Write(token);
        OperationManager.MarkAsExtended(token);

        _events = [];
        _ = Task.Run(() =>
        {
            foreach (var ev in _events.GetConsumingEnumerable())
            {
                Output.Event(ev, token);
            }
        });

        _ = Task.Run(() =>
        {
            Output.Event(
                (adapter with { OptionDiscovering = true }).ToEvent(EventAction.Updated),
                token
            );

            token.Wait();

            Output.Event(
                (adapter with { OptionDiscovering = false }).ToEvent(EventAction.Updated),
                token
            );

            _events?.CompleteAdding();
        });

        using (_discoverylock.Acquire())
        {
            foreach (var unpaired in _devices.Values)
            {
                var (device, err) = DeviceModelExt.ConvertToDeviceModel(unpaired.Info, true);
                if (err == Errors.ErrorNone)
                {
                    _events.Add(device.ToEvent(EventAction.Added));
                }
            }
        }

        return Errors.ErrorNone;
    }

    internal static ErrorData StopEmitter()
    {
        _token.Read(out var tok);
        if (tok == OperationToken.None)
            return Errors.ErrorUnexpected.AddMetadata(
                "exception",
                "No device discovery is running."
            );

        tok.Release();
        _token.Write(OperationToken.None);

        return Errors.ErrorNone;
    }

    internal static bool AddDevice(DeviceInformation info, DeviceEvent devent)
    {
        if (info.Pairing.IsPaired)
            return false;

        _events?.Add(devent);
        using (_discoverylock.Acquire())
        {
            _devices.Add(info.Id, new UnpairedDevice(info, devent));
        }

        return true;
    }

    internal static bool UpdateDevice(DeviceInformationUpdate update, DeviceEvent devent)
    {
        var remove = false;
        var updated = false;

        using (_discoverylock.Acquire())
        {
            if (_devices.TryGetValue(update.Id, out var unpaired))
            {
                if (unpaired.Info.Pairing.IsPaired)
                {
                    remove = true;
                    updated = false;
                }
                else
                {
                    unpaired.Info.Update(update);
                    unpaired.Event = devent;
                    _devices[update.Id] = unpaired;

                    updated = true;
                    remove = false;
                }
            }
        }

        if (remove)
            return RemoveDevice(update, devent);

        if (updated)
            _events?.Add(devent);

        return updated;
    }

    internal static bool RemoveDevice(DeviceInformationUpdate update, DeviceEvent devent)
    {
        var removed = false;
        using (_discoverylock.Acquire())
        {
            removed = _devices.Remove(update.Id);
        }

        if (removed)
            _events?.Add(devent);

        return false;
    }
}

internal sealed class RegistryWatcher : IDisposable
{
    private readonly ManagementEventWatcher _watcher;
    private readonly EventArrivedEventHandler _onChange;
    private bool _started = false;

    internal RegistryWatcher(string hive, string rootPath, EventArrivedEventHandler onChange)
    {
        string query =
            $@"SELECT * FROM RegistryTreeChangeEvent WHERE Hive = '{hive}' AND RootPath = '{rootPath.Replace(@"\", @"\\")}'";

        _watcher = new ManagementEventWatcher(query);
        _onChange = onChange;
    }

    internal RegistryWatcher(
        string hive,
        string keyPath,
        string[] valueNames,
        EventArrivedEventHandler onChange
    )
    {
        var sb = new StringBuilder();
        sb.Append(
            $@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = '{hive}' AND KeyPath = '{keyPath.Replace(@"\", @"\\")}'"
        );

        var count = 0;
        sb.Append("AND (");
        foreach (var valueName in valueNames)
        {
            if (count > 0)
                sb.Append(" OR ");

            sb.Append($@"ValueName = '{valueName}'");
            count++;
        }
        sb.Append(')');

        _watcher = new ManagementEventWatcher(sb.ToString());
        _onChange = onChange;
    }

    internal void Start()
    {
        _watcher.EventArrived += _onChange;
        _watcher.Start();
        _started = true;
    }

    internal void Stop()
    {
        if (!_started)
            return;

        _watcher.Stop();
    }

    public void Dispose()
    {
        _watcher?.Stop();
        _watcher?.Dispose();
    }
}
