using System.Text;
using System.Timers;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Stack.Data.Events;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices;
using Bluetuith.Shim.Types;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using Windows.Foundation;
using Windows.Win32;
using WmiLight;
using static Bluetuith.Shim.Types.IEvent;
using Timer = System.Timers.Timer;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Monitors;

internal static class Watchers
{
    private static CancellationTokenSource _resume;

    private static bool _isAdapterWatcherRunning = false;
    private static bool _isDeviceWatcherRunning = false;

    private static bool _isBatteryWatcherRunning = false;

    internal static bool IsDeviceWatcherRunning
    {
        get => _isDeviceWatcherRunning;
    }

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
            var adapter = BluetoothAdapter.GetDefaultAsync().GetAwaiter().GetResult();
            if (adapter == null)
                throw new NullReferenceException(nameof(adapter));

            var radio = adapter.GetRadioAsync().GetAwaiter().GetResult();
            if (radio == null)
                throw new NullReferenceException(nameof(radio));

            var adapterEvent = new AdapterEvent();
            var error = adapterEvent.MergeRadioEventData();
            if (error != Errors.ErrorNone)
                throw new Exception(error.Description);

            var poweredEvent = new AdapterEvent()
            {
                Action = EventAction.Updated,
                Address = adapterEvent.Address,
            };

            var discoverableEvent = poweredEvent with
            {
                OptionDiscoverable = PInvoke.BluetoothIsDiscoverable(null) > 0,
            };

            TypedEventHandler<Radio, object> stateChanged = new(
                (radio, _) =>
                {
                    poweredEvent.OptionPowered = poweredEvent.OptionPairable =
                        radio.State == RadioState.On;
                    Output.Event(poweredEvent, token);
                }
            );

            // TODO: Use WM_DEVICECHANGE events instead.
            void elapsedAction(object s, ElapsedEventArgs e)
            {
                var discoverable = PInvoke.BluetoothIsDiscoverable(null);
                if (
                    discoverableEvent.OptionDiscoverable.HasValue
                    && discoverableEvent.OptionDiscoverable.Value == discoverable
                )
                    return;

                discoverableEvent.OptionDiscoverable = discoverable > 0;
                Output.Event(discoverableEvent, token);
            }

            radio.StateChanged += stateChanged;

            using var timer = new Timer(1000);
            timer.Elapsed += elapsedAction;
            timer.AutoReset = true;
            timer.Start();

            _isAdapterWatcherRunning = true;

            token.Wait();

            radio.StateChanged -= stateChanged;
            timer.Elapsed -= elapsedAction;
            timer.Stop();
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
                        var roothPathEvent = e.NewEvent.GetPropertyValue("RootPath");
                        if (roothPathEvent != null && roothPathEvent is string rootPath)
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
                                    var deviceEvent = new DeviceEvent()
                                    {
                                        Action = EventAction.Updated,
                                    };

                                    var error = deviceEvent.MergeBatteryInformation(
                                        pnpDevice.DeviceId,
                                        batteryPercentage
                                    );

                                    if (error == Errors.ErrorNone)
                                    {
                                        Output.Event(deviceEvent, token);
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

    internal static void SetupDevicesWatcher(OperationToken token)
    {
        if (_isDeviceWatcherRunning)
            return;

        if (!_resume.IsCancellationRequested)
            _resume.Token.WaitHandle.WaitOne();

        try
        {
            if (token.CancelTokenSource.IsCancellationRequested)
                return;

            using var monitor = new Monitor(
                BluetoothDevice.GetDeviceSelectorFromPairingState(true)
            );

            using var subscriber = new Monitor.Subscriber(monitor, token.OperationId)
            {
                OnAdded = (info) =>
                {
                    if (info.OptionPaired.HasValue && info.OptionPaired.Value)
                    {
                        info.RefreshInformation();
                        Output.Event(info, token);
                    }
                },
                OnRemoved = (info) =>
                {
                    Output.Event(info, token);
                },
                OnUpdated = (oldInfo, newInfo) =>
                {
                    var wasPaired = oldInfo.OptionPaired.HasValue && oldInfo.OptionPaired.Value;
                    var isPaired = newInfo.OptionPaired.HasValue && newInfo.OptionPaired.Value;

                    if (isPaired)
                        Output.Event(newInfo, token);
                },
            };

            monitor.Start();
            token.Wait();
        }
        finally
        {
            _isDeviceWatcherRunning = false;
        }
    }
}

internal sealed partial class RegistryWatcher : IDisposable
{
    private readonly WmiConnection _connection;

    private readonly WmiEventWatcher _eventWatcher;

    private readonly EventHandler<WmiEventArrivedEventArgs> _onChange;

    private bool _started = false;

    internal RegistryWatcher(
        string hive,
        string rootPath,
        EventHandler<WmiEventArrivedEventArgs> onChange
    )
    {
        var query =
            $@"SELECT * FROM RegistryTreeChangeEvent WHERE Hive = '{hive}' AND RootPath = '{rootPath.Replace(@"\", @"\\")}'";

        _connection = new WmiConnection();
        _eventWatcher = _connection.CreateEventWatcher(query);
        _onChange = onChange;
    }

    internal RegistryWatcher(
        string hive,
        string keyPath,
        string[] valueNames,
        EventHandler<WmiEventArrivedEventArgs> onChange
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

        _connection = new WmiConnection();
        _eventWatcher = _connection.CreateEventWatcher(sb.ToString());
        _onChange = onChange;
    }

    internal void Start()
    {
        _eventWatcher.EventArrived += _onChange;
        _eventWatcher.Start();
        _started = true;
    }

    internal void Stop()
    {
        if (!_started)
            return;

        _eventWatcher.EventArrived -= _onChange;
        _eventWatcher.Stop();
        _connection.Close();
    }

    public void Dispose()
    {
        try
        {
            _eventWatcher.EventArrived -= _onChange;
            _eventWatcher?.Dispose();
            _connection?.Dispose();
        }
        catch { }
    }
}
