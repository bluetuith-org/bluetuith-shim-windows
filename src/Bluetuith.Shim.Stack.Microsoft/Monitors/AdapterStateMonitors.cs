using Bluetuith.Shim.DataTypes;
using Bluetuith.Shim.Operations;
using InTheHand.Net;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using Windows.Win32;
using Timer = System.Timers.Timer;

namespace Bluetuith.Shim.Stack.Microsoft;

internal partial class AdapterStateMonitor : IMonitor
{
    protected OperationToken _token;

    protected Radio radio;
    private BluetoothAdapter bluetoothAdapter;

    protected readonly AdapterEvent adapterEvent = new();

    public virtual MonitorName Name { get; }
    public virtual bool IsRunning { get; }
    public virtual bool IsCreated { get; }
    public virtual bool RestartOnPowerStateChanged { get; }

    public BluetoothAddress AdapterAddress =>
        bluetoothAdapter != null
            ? (new BluetoothAddress(bluetoothAdapter.BluetoothAddress))
            : throw new InvalidDataException("Uninitialized adapter");

    public virtual void Create(OperationToken token)
    {
        _token = token;

        bluetoothAdapter =
            BluetoothAdapter.GetDefaultAsync().GetAwaiter().GetResult()
            ?? throw new NullReferenceException("No adapter was found");
        radio =
            bluetoothAdapter.GetRadioAsync().GetAwaiter().GetResult()
            ?? throw new NullReferenceException("No radio was found or is disabled");

        adapterEvent.Address = AdapterAddress.ToString("C");
        adapterEvent.Action = IEvent.EventAction.Updated;
    }

    public virtual bool Start()
    {
        return true;
    }

    public virtual void Stop() { }

    public virtual void Dispose() { }
}

internal partial class AdapterPowerStateMonitor : AdapterStateMonitor
{
    private bool _started = false;

    public override MonitorName Name => MonitorName.AdapterPowerStateMonitor;
    public override bool IsRunning => radio != null && _started;
    public override bool IsCreated => radio != null;
    public override bool RestartOnPowerStateChanged => false;

    public override void Create(OperationToken token)
    {
        base.Create(token);
    }

    public override bool Start()
    {
        if (base.Start() && radio != null)
        {
            radio.StateChanged += Push_Powered_Event;
            _started = true;
        }

        return _started;
    }

    public override void Stop()
    {
        if (radio != null)
            radio.StateChanged -= Push_Powered_Event;

        _started = false;

        base.Stop();
    }

    public override void Dispose()
    {
        Stop();

        base.Dispose();
    }

    internal virtual void Push_Powered_Event(Radio radio, object args)
    {
        if (adapterEvent.OptionPowered == false && radio.State == RadioState.On)
            adapterEvent.OptionDiscoverable = PInvoke.BluetoothIsDiscoverable(null) > 0;
        else
            adapterEvent.OptionDiscoverable = null;

        adapterEvent.OptionPowered = adapterEvent.OptionPairable = radio.State == RadioState.On;

        Output.Event(adapterEvent, _token);
    }
}

internal partial class AdapterDiscoverableStateMonitor : AdapterStateMonitor
{
    private Timer timer;

    public override MonitorName Name => MonitorName.AdapterDiscoverableStateMonitor;
    public override bool IsRunning => radio != null && timer != null && timer.Enabled;
    public override bool IsCreated => radio != null && timer != null;
    public override bool RestartOnPowerStateChanged => false;

    public override void Create(OperationToken token)
    {
        base.Create(token);

        timer = new(TimeSpan.FromSeconds(1)) { AutoReset = true };

        adapterEvent.OptionDiscoverable = PInvoke.BluetoothIsDiscoverable(null) > 0;
    }

    public override bool Start()
    {
        if (base.Start() && timer != null)
        {
            timer.Elapsed += Push_Discovery_Event;
            timer.Start();
        }

        return IsRunning;
    }

    public override void Stop()
    {
        if (timer != null)
        {
            timer.Stop();
            timer.Elapsed -= Push_Discovery_Event;
        }

        base.Stop();
    }

    internal void PushEvent()
    {
        if (timer != null)
        {
            timer.Stop();
            Push_Discovery_Event(null, null);
            timer.Start();
        }
    }

    private void Push_Discovery_Event(object sender, System.Timers.ElapsedEventArgs e)
    {
        if (radio == null)
            return;

        var discoverable =
            radio.State == RadioState.On && PInvoke.BluetoothIsDiscoverable(null) > 0;
        if (
            adapterEvent.OptionDiscoverable.HasValue
            && adapterEvent.OptionDiscoverable.Value == discoverable
        )
            return;

        adapterEvent.OptionDiscoverable = discoverable;
        Output.Event(adapterEvent, _token);
    }

    public override void Dispose()
    {
        timer.Stop();
        timer.Dispose();

        base.Dispose();
    }
}
