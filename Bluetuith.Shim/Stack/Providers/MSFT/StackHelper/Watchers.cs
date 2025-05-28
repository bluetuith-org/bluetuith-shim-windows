using System.Management;
using System.Text;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices;
using Bluetuith.Shim.Types;
using DotNext;
using Microsoft.Win32;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using static Bluetuith.Shim.Types.IEvent;

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
                    Task.Run(() => SetupDevicesWatcher(token)),
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

    private static void SetupDevicesWatcher(OperationToken token)
    {
        if (_isDeviceWatcherRunning)
            return;

        try
        {
            if (!_resume.IsCancellationRequested)
                _resume.Token.WaitHandle.WaitOne();

            if (token.CancelTokenSource.IsCancellationRequested)
                return;

            TypedEventHandler<DeviceWatcher, DeviceInformation> addedEvent = new(
                (s, e) =>
                {
                    var d = BluetoothDevice.FromIdAsync(e.Id).GetAwaiter().GetResult();
                    if (d == null)
                    {
                        return;
                    }

                    var (device, error) = DeviceModelExt.ConvertToDeviceModel(d);
                    if (error == Errors.ErrorNone)
                    {
                        Output.Event(device.ToEvent(EventAction.Added), token);
                    }
                }
            );
            TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> removedEvent = new(
                (s, e) =>
                {
                    var (device, error) = DeviceModelExt.ConvertToDeviceModel(e);
                    if (error == Errors.ErrorNone)
                    {
                        Output.Event(device.ToEvent(EventAction.Removed), token);
                    }
                }
            );
            TypedEventHandler<DeviceWatcher, DeviceInformationUpdate> updatedEvent = new(
                (s, e) =>
                {
                    var (device, error) = DeviceModelExt.ConvertToDeviceModel(e);
                    if (error == Errors.ErrorNone)
                    {
                        Output.Event(device.ToEvent(EventAction.Updated), token);
                    }
                }
            );

            DeviceWatcher watcher = DeviceInformation.CreateWatcher(
                BluetoothDevice.GetDeviceSelectorFromPairingState(true),
                [
                    "System.Devices.Aep.DeviceAddress",
                    "System.Devices.Aep.SignalStrength",
                    "System.Devices.Aep.IsConnected",
                    "System.Devices.Aep.IsPaired",
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
