using Bluetuith.Shim.DataTypes;
using Bluetuith.Shim.Operations;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;
using Windows.Devices.Bluetooth;
using WmiLight;
using static Bluetuith.Shim.DataTypes.IEvent;

namespace Bluetuith.Shim.Stack.Microsoft;

internal partial class DevicesStateMonitor : IMonitor
{
    private DevicesWatcher monitor;
    private DevicesWatcher.Subscriber subscriber;

    MonitorName IMonitor.Name => MonitorName.DevicesStateMonitor;
    bool IMonitor.RestartOnPowerStateChanged => false;
    bool IWatcher.IsRunning => monitor != null && monitor.IsRunning;
    bool IWatcher.IsCreated => monitor != null;

    public void Create(OperationToken token)
    {
        monitor = new DevicesWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true));

        subscriber = new DevicesWatcher.Subscriber(monitor, token)
        {
            OnAdded = static (info, tok) =>
            {
                if (info.OptionPaired.HasValue && info.OptionPaired.Value)
                {
                    info.RefreshInformation();
                    Output.Event(info, tok);
                }
            },
            OnRemoved = static (info, tok) =>
            {
                Output.Event(info, tok);
            },
            OnUpdated = static (oldInfo, newInfo, tok) =>
            {
                var wasPaired = oldInfo.OptionPaired.HasValue && oldInfo.OptionPaired.Value;
                var isPaired = newInfo.OptionPaired.HasValue && newInfo.OptionPaired.Value;

                if (isPaired)
                    Output.Event(newInfo, tok);
            },
        };
    }

    public bool Start()
    {
        return monitor != null && monitor.Start();
    }

    public void Stop()
    {
        monitor?.Stop();
    }

    public void Dispose()
    {
        subscriber?.Dispose();
        monitor?.Dispose();
    }
}

internal partial class DevicesBatteryStateMonitor : IMonitor
{
    private RegistryWatcher _monitor;
    private OperationToken _token;

    MonitorName IMonitor.Name => MonitorName.DevicesBatteryMonitor;
    bool IMonitor.RestartOnPowerStateChanged => false;
    bool IWatcher.IsRunning => _monitor != null && _monitor.IsRunning;
    bool IWatcher.IsCreated => _monitor != null;

    public void Create(OperationToken token)
    {
        _token = token;
        _monitor = new(
            "HKEY_LOCAL_MACHINE",
            WindowsPnPInformation.HFEnumeratorRegistryKey,
            EventWatcher_EventArrived
        );
    }

    public bool Start()
    {
        return _monitor != null && _monitor.Start();
    }

    public void Stop()
    {
        _monitor?.Stop();
    }

    public void Dispose()
    {
        _monitor?.Dispose();
    }

    private void EventWatcher_EventArrived(object s, WmiEventArrivedEventArgs e)
    {
        try
        {
            var roothPathEvent = e.NewEvent.GetPropertyValue("RootPath");
            if (roothPathEvent != null && roothPathEvent is string rootPath)
            {
                var instances = 0;
                while (
                    Devcon.FindByInterfaceGuid(
                        WindowsPnPInformation.HFEnumeratorInterfaceGuid,
                        out var pnpDevice,
                        instances++,
                        HostRadio.IsOperable
                    )
                )
                {
                    if (
                        Convert.ToInt32(
                            pnpDevice.GetProperty<byte>(
                                WindowsPnPInformation.Device.BatteryPercentage
                            )
                        )
                            is int batteryPercentage
                        && batteryPercentage > 0
                    )
                    {
                        var deviceEvent = new DeviceEvent() { Action = EventAction.Updated };

                        var error = deviceEvent.MergeBatteryInformation(
                            pnpDevice.DeviceId,
                            batteryPercentage
                        );

                        if (error == Errors.ErrorNone)
                        {
                            Output.Event(deviceEvent, _token);
                        }
                    }
                }
            }
        }
        catch { }
    }
}
