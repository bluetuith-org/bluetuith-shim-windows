using System.Collections.Concurrent;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices;
using DotNext.Threading;
using Windows.Devices.Enumeration;
using static Bluetuith.Shim.Types.IEvent;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Monitors;

internal class Monitor : IDisposable
{
    private readonly Dictionary<string, DeviceChanges> devices = [];
    private readonly ConcurrentDictionary<long, Subscriber> subscribers = [];

    private readonly DeviceWatcher _watcher;
    private readonly ManualResetEvent mre = new(true);

    internal IEnumerable<DeviceChanges> CurrentDevices
    {
        get
        {
            List<DeviceChanges> changes = [];

            using (devices.AcquireReadLock())
            {
                foreach (var device in devices.Values)
                    changes.Add(device with { });
            }

            return changes;
        }
    }

    internal bool IsStarted
    {
        get => _watcher.Status == DeviceWatcherStatus.Started;
    }

    internal Monitor(string aqs)
    {
        _watcher = DeviceInformation.CreateWatcher(
            aqs,
            [
                "System.ItemNameDisplay",
                "System.Devices.Aep.AepId",
                "System.Devices.Aep.DeviceAddress",
                "System.Devices.Aep.IsConnected",
                "System.Devices.Aep.IsPaired",
                "System.Devices.Aep.Category",
                "System.Devices.Aep.Manufacturer",
                "System.Devices.Aep.SignalStrength",
                "System.Devices.Aep.Bluetooth.Cod.Major",
                "System.Devices.Aep.Bluetooth.Cod.Minor",
                "System.Devices.Aep.Bluetooth.Cod.Services.Audio",
                "System.Devices.Aep.Bluetooth.Cod.Services.Capturing",
                "System.Devices.Aep.Bluetooth.Cod.Services.Information",
                "System.Devices.Aep.Bluetooth.Cod.Services.LimitedDiscovery",
                "System.Devices.Aep.Bluetooth.Cod.Services.Networking",
                "System.Devices.Aep.Bluetooth.Cod.Services.ObjectXfer",
                "System.Devices.Aep.Bluetooth.Cod.Services.Positioning",
                "System.Devices.Aep.Bluetooth.Cod.Services.Rendering",
                "System.Devices.Aep.Bluetooth.Cod.Services.Telephony",
            ],
            DeviceInformationKind.AssociationEndpointContainer
        );
    }

    private void Watcher_Stopped(DeviceWatcher sender, object args)
    {
        ClearDevices();
        mre.Set();
    }

    private void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args) =>
        RemoveDeviceInformation(args);

    private void Watcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args) =>
        UpdateDeviceInformation(args);

    private void Watcher_Added(DeviceWatcher sender, DeviceInformation args) =>
        AddDeviceInformation(args);

    internal bool Start()
    {
        if (!mre.WaitOne(TimeSpan.FromSeconds(1)))
            return false;

        if (
            _watcher.Status == DeviceWatcherStatus.Stopped
            || _watcher.Status == DeviceWatcherStatus.Aborted
            || _watcher.Status == DeviceWatcherStatus.Created
        )
        {
            _watcher.Added += Watcher_Added;
            _watcher.Updated += Watcher_Updated;
            _watcher.Removed += Watcher_Removed;
            _watcher.Stopped += Watcher_Stopped;

            _watcher.Start();
        }

        return true;
    }

    internal void Stop(CancellationToken timeout)
    {
        if (_watcher.Status == DeviceWatcherStatus.Started)
            return;

        timeout.WaitHandle.WaitOne();
        if (timeout.IsCancellationRequested)
            return;

        mre.Reset();

        _watcher.Stop();
        _watcher.Added -= Watcher_Added;
        _watcher.Updated -= Watcher_Updated;
        _watcher.Removed -= Watcher_Removed;
        _watcher.Stopped -= Watcher_Stopped;
    }

    internal void ClearDevices()
    {
        using (devices.AcquireWriteLock())
            devices.Clear();
    }

    public void Dispose()
    {
        try
        {
            _watcher.Stop();
        }
        catch { }
        finally
        {
            _watcher.Added -= Watcher_Added;
            _watcher.Updated -= Watcher_Updated;
            _watcher.Removed -= Watcher_Removed;
        }
    }

    internal void AddDeviceInformation(DeviceInformation deviceInformation)
    {
        var changes = new DeviceChanges(deviceInformation);

        using (devices.AcquireWriteLock())
            devices[deviceInformation.Id] = changes with { };

        PushEvents(EventAction.Added, changes, null);
    }

    internal void UpdateDeviceInformation(DeviceInformationUpdate deviceInformationUpdate)
    {
        DeviceChanges oldChanges = null,
            newChanges = null;

        using (devices.AcquireWriteLock())
        {
            if (devices.TryGetValue(deviceInformationUpdate.Id, out var device))
            {
                oldChanges = device with { Action = EventAction.Added };

                device.UpdateInformation(deviceInformationUpdate);

                newChanges = device with { Action = EventAction.Updated };
            }
        }

        PushEvents(EventAction.Updated, oldChanges, newChanges);
    }

    internal void RemoveDeviceInformation(DeviceInformationUpdate deviceInformationUpdate)
    {
        DeviceChanges currentInfo = null;

        using (devices.AcquireWriteLock())
            devices.Remove(deviceInformationUpdate.Id, out currentInfo);

        currentInfo.Action = EventAction.Removed;

        PushEvents(EventAction.Removed, currentInfo, null);
    }

    private void PushEvents(
        EventAction eventAction,
        DeviceChanges oldChanges,
        DeviceChanges newChanges
    )
    {
        if (oldChanges == null || eventAction == EventAction.Updated && newChanges == null)
            return;

        foreach (var subscriber in subscribers.Values)
        {
            switch (eventAction)
            {
                case EventAction.Added:
                    subscriber.OnAdded(oldChanges);
                    break;
                case EventAction.Updated:
                    subscriber.OnUpdated(oldChanges, newChanges);
                    break;
                case EventAction.Removed:
                    subscriber.OnRemoved(oldChanges);
                    break;
            }
        }
    }

    internal void RegisterSubscriber(Subscriber subscriber)
    {
        subscribers.TryAdd(subscriber.Id, subscriber);
    }

    internal void UnregisterSubscriber(Subscriber subscriber)
    {
        subscribers.TryRemove(subscriber.Id, out _);
    }

    internal class Subscriber : IDisposable
    {
        internal Action<DeviceChanges> OnAdded = delegate { };
        internal Action<DeviceChanges> OnRemoved = delegate { };
        internal Action<DeviceChanges, DeviceChanges> OnUpdated = delegate { };

        private readonly Monitor _monitor;

        internal long Id { get; private set; }

        internal Subscriber(Monitor monitor, long subId)
        {
            _monitor = monitor;
            Id = subId;

            _monitor.RegisterSubscriber(this);
        }

        public void Dispose()
        {
            _monitor.UnregisterSubscriber(this);
        }
    }

    internal record class DeviceChanges : DeviceEvent
    {
        private readonly DeviceInformation _deviceInformation;

        internal DeviceChanges(DeviceInformation deviceInformation)
        {
            _deviceInformation = deviceInformation;
            this.MergeDeviceInformation(_deviceInformation);
        }

        internal bool UpdateInformation(
            DeviceInformationUpdate deviceInformationUpdate,
            bool appendServices = false
        )
        {
            if (_deviceInformation == null)
                return false;

            _deviceInformation.Update(deviceInformationUpdate);
            return this.MergeDeviceInformation(_deviceInformation, appendServices);
        }

        internal bool RefreshInformation()
        {
            if (_deviceInformation == null)
                return false;

            return this.MergeDeviceInformation(
                _deviceInformation,
                _deviceInformation.Pairing.IsPaired
            );
        }
    }
}
