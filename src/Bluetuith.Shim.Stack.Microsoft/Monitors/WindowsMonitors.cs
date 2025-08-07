using System.Collections.Concurrent;
using System.Collections.Frozen;
using Bluetuith.Shim.DataTypes;
using Bluetuith.Shim.Operations;
using Windows.Devices.Radios;
using Timer = System.Timers.Timer;

namespace Bluetuith.Shim.Stack.Microsoft;

internal static partial class WindowsMonitors
{
    private record class MonitorTaskRecord
    {
        public OperationToken Token;

        public int ExceptionCount = 0;
        public Exception Exception = null;

        public IMonitor Monitor;
    }

    private static readonly ManualResetEvent monitorStateMre = new(true);
    private static bool monitorRunning = false;
    private static bool monitorStarted = false;

    private static readonly ManualResetEvent adapterStateMre = new(false);
    private static readonly AdapterEventMonitor adapterEventMonitor = new(PowerStateChangeAction);

    private static readonly ConcurrentDictionary<MonitorName, MonitorTaskRecord> _tasks = new();
    private static readonly FrozenSet<IMonitor> _monitors =
    [
        new DevicesStateMonitor(),
        new DevicesBatteryStateMonitor(),
        new OppServerMonitor(),
    ];

    private static Timer _timer;

    public static ErrorData Start(OperationToken token)
    {
        if (monitorStarted)
            return Errors.ErrorNone;

        try
        {
            adapterEventMonitor.Create(token);
            adapterEventMonitor.Start();

            _timer = new(TimeSpan.FromSeconds(2)) { AutoReset = false };
            _timer.Elapsed += Timer_Elapsed;

            adapterEventMonitor.PushPoweredEvent(false);
        }
        catch (Exception e)
        {
            return Errors.ErrorUnexpected.AddMetadata("exception", e.Message);
        }

        monitorStarted = true;

        return Errors.ErrorNone;
    }

    public static ErrorData Stop()
    {
        if (!monitorStarted)
            return Errors.ErrorNone;

        try
        {
            monitorStateMre.WaitOne();
            monitorStateMre.Reset();

            monitorRunning = false;
            monitorStarted = false;

            _timer.Stop();
            _timer.Dispose();

            foreach (var mrecord in _tasks.Values)
            {
                try
                {
                    mrecord.Monitor.Stop();
                    mrecord.Monitor.Dispose();
                }
                catch { }
            }

            _tasks.Clear();

            adapterEventMonitor.Stop();
            adapterEventMonitor.Dispose();
        }
        catch (Exception e)
        {
            return Errors.ErrorUnexpected.AddMetadata("exception", e.Message);
        }
        finally
        {
            monitorStateMre.Set();
        }

        return Errors.ErrorNone;
    }

    public static void PushDiscoverableEvent() => adapterEventMonitor.PushDiscoverableEvent();

    private static async Task LaunchNewMonitor(object state)
    {
        var mrecord = state as MonitorTaskRecord;

        Restart:
        try
        {
            if (_tasks.TryGetValue(mrecord.Monitor.Name, out var mrec))
            {
                if (mrec.Monitor.IsRunning)
                    return;

                mrec.Token = mrecord.Token;
                mrecord = mrec;
            }

            adapterStateMre.WaitOne();
            monitorStateMre.WaitOne();

            if (!monitorRunning)
                return;

            if (!mrecord.Monitor.IsCreated)
                mrecord.Monitor.Create(mrecord.Token);

            if (!mrecord.Monitor.Start())
                throw new Exception($"Cannot start monitor {mrecord.Monitor.Name}");

            _tasks.TryAdd(mrecord.Monitor.Name, mrecord);
        }
        catch (Exception e)
        {
            mrecord.Exception = e;
            mrecord.ExceptionCount++;

            if (mrecord.ExceptionCount > 3)
            {
                Output.Event(
                    Errors.ErrorUnexpected.AddMetadata(
                        $"An error occurred while starting a monitor: {mrecord.Monitor.Name}",
                        e.Message
                    ),
                    mrecord.Token
                );
                _tasks.TryRemove(mrecord.Monitor.Name, out _);

                return;
            }

            await Task.Delay(1000);
            goto Restart;
        }
    }

    private static void RestartMonitors(OperationToken token, RadioState radioState)
    {
        try
        {
            if (!monitorStateMre.WaitOne(TimeSpan.FromSeconds(1)))
                return;

            monitorStateMre.Reset();

            switch (radioState)
            {
                case RadioState.On:
                    StopAllMonitors();
                    StartAllMonitors();
                    break;

                default:
                    StopAllMonitors();
                    break;
            }
        }
        catch { }
        finally
        {
            monitorStateMre.Set();
        }

        void StartAllMonitors()
        {
            monitorRunning = true;

            foreach (var monitor in _monitors)
                Task.Factory.StartNew(
                    LaunchNewMonitor,
                    new MonitorTaskRecord() { Token = token, Monitor = monitor }
                );
        }

        void StopAllMonitors()
        {
            try
            {
                foreach (var monitor in _monitors)
                {
                    if (
                        _tasks.TryGetValue(monitor.Name, out var mrecord)
                        && mrecord.Monitor.RestartOnPowerStateChanged
                    )
                    {
                        try
                        {
                            mrecord.Monitor.Stop();
                        }
                        catch
                        {
                            mrecord.Monitor.Dispose();
                        }
                        finally
                        {
                            _tasks.TryRemove(mrecord.Monitor.Name, out _);
                        }
                    }
                }
            }
            catch { }
            finally { monitorRunning = false; }
        }
    }

    private static void PowerStateChangeAction(Radio radio, object args)
    {
        switch (radio.State)
        {
            case RadioState.On:
                adapterStateMre.Set();
                break;

            case RadioState.Off:
            case RadioState.Unknown:
                adapterStateMre.Reset();
                break;
        }

        PushDiscoverableEvent();

        _timer.Stop();
        _timer.Start();
    }

    private static void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        RestartMonitors(adapterEventMonitor.Token, adapterEventMonitor.AdapterPowerState);
    }

    private partial class AdapterEventMonitor(Action<Radio, object> action)
        : AdapterPowerStateMonitor
    {
        private readonly AdapterDiscoverableStateMonitor _discoverableMonitor = new();

        internal OperationToken Token => _token;

        internal RadioState AdapterPowerState => radio != null ? radio.State : RadioState.Unknown;

        public override void Create(OperationToken token)
        {
            base.Create(token);
            _discoverableMonitor.Create(token);
        }

        public override bool Start()
        {
            return base.Start() && _discoverableMonitor.Start();
        }

        public override void Stop()
        {
            _discoverableMonitor.Stop();
            base.Stop();
        }

        public override void Dispose()
        {
            _discoverableMonitor.Dispose();
            base.Dispose();
        }

        internal void PushPoweredEvent(bool publishEvent = true) =>
            Push_Powered_Event(radio, publishEvent);

        internal void PushDiscoverableEvent() => _discoverableMonitor.PushEvent();

        internal override void Push_Powered_Event(Radio radio, object args)
        {
            action(radio, args);

            if (args is bool publishEvent && !publishEvent)
                return;

            base.Push_Powered_Event(radio, args);
        }
    }
}
