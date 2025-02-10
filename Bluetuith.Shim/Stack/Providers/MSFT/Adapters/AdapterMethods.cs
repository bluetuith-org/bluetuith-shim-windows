using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Stack.Models;
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

namespace Bluetuith.Shim.Stack.Providers.MSFT.Adapters;

internal static class AdapterMethods
{
    private static OperationToken _discoveryToken = OperationToken.None;
    private static readonly object _discoverylock = new();

    internal static ErrorData DisconnectDevice(string address)
    {
        try
        {
            ThrowIfRadioNotOperable();

            using HostRadio radio = new();
            radio.DisconnectRemoteDevice(address);
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceDisconnect.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }

    internal static (GenericResult<List<DeviceModel>>, ErrorData) GetPairedDevices()
    {
        List<DeviceModel> pairedDevices = [];
        ErrorData error = Errors.ErrorNone;

        try
        {
            var instances = 0;

            while (
                Devcon.FindByInterfaceGuid(
                    PnPInformation.BluetoothDevicesInterfaceGuid,
                    out var pnpDevice,
                    instances++,
                    HostRadio.IsOperable
                )
            )
            {
                var (device, convertError) = DeviceModelExt.ConvertToDeviceModel(pnpDevice);
                if (error != Errors.ErrorNone)
                {
                    error = convertError;
                    goto PairedDevices;
                }

                pairedDevices.Add(device);
            }
        }
        catch (Exception e)
        {
            return (
                GenericResult<List<DeviceModel>>.Empty(),
                Errors.ErrorDeviceNotFound.WrapError(new() { { "exception", e.Message } })
            );
        }

        PairedDevices:
        return (pairedDevices.ToResult("Paired Devices:", "paired_devices"), error);
    }

    internal static async Task<ErrorData> RemoveDeviceAsync(string address)
    {
        try
        {
            using BluetoothDevice device = await DeviceUtils.GetBluetoothDevice(address);
            if (!device.DeviceInformation.Pairing.IsPaired)
            {
                return Errors.ErrorNone;
            }

            DeviceUnpairingResult unpairResult =
                await device.DeviceInformation.Pairing.UnpairAsync();
            if (unpairResult.Status != DeviceUnpairingResultStatus.Unpaired)
            {
                throw new Exception($"Could not unpair device {address}: {unpairResult.Status}");
            }
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceUnpairing.WrapError(
                new() { { "device-address", address }, { "exception", e.Message } }
            );
        }

        return Errors.ErrorNone;
    }

    internal static ErrorData SetPoweredState(bool enable)
    {
        try
        {
            var isOperable = HostRadio.IsOperable;
            if (enable && isOperable || !enable && !isOperable)
            {
                return Errors.ErrorNone;
            }

            using (HostRadio hostRadio = new())
            {
                if (!enable)
                {
                    hostRadio.DisableRadio();
                }
            }
            ;
        }
        catch (Exception e)
        {
            return Errors.ErrorAdapterStateAccess.WrapError(new() { { "exception", e.Message } });
        }

        return Errors.ErrorNone;
    }

    internal static ErrorData SetPairableState(bool _) => Errors.ErrorUnsupported;

    internal static ErrorData SetDiscoverableState(bool enable)
    {
        try
        {
            if (Devcon.FindByInterfaceGuid(HostRadio.DeviceInterface, out PnPDevice path))
            {
                using (
                    RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        PnPInformation.Adapter.DeviceParametersRegistryPath(path.DeviceId),
                        true
                    )
                )
                {
                    key?.SetValue(
                        PnPInformation.Adapter.DiscoverableRegistryKey,
                        enable ? 1 : 0,
                        RegistryValueKind.DWord
                    );
                }
            }
        }
        catch (Exception e)
        {
            return Errors.ErrorAdapterStateAccess.WrapError(new() { { "exception", e.Message } });
        }

        return Errors.ErrorNone;
    }

    internal static Optional<bool> GetDiscoverableState()
    {
        var discoverable = Optional.None<bool>();

        try
        {
            if (Devcon.FindByInterfaceGuid(HostRadio.DeviceInterface, out PnPDevice path))
            {
                using (
                    RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        PnPInformation.Adapter.DeviceParametersRegistryPath(path.DeviceId),
                        false
                    )
                )
                {
                    var value = key?.GetValue(PnPInformation.Adapter.DiscoverableRegistryKey);
                    if (value is not null)
                        discoverable = value as int? == 1;
                }
            }
        }
        catch { }

        return discoverable;
    }

    internal static Guid[] GetAdapterServices()
    {
        var services = new List<Guid>();

        try
        {
            if (Devcon.FindByInterfaceGuid(HostRadio.DeviceInterface, out PnPDevice path))
            {
                using (
                    RegistryKey key = Registry.LocalMachine.OpenSubKey(
                        PnPInformation.Adapter.ServicesRegistryPath,
                        false
                    )
                )
                {
                    if (key is not null)
                    {
                        foreach (var subkeys in key.GetSubKeyNames())
                        {
                            services.Add(Guid.Parse(subkeys));
                        }
                    }
                }
            }
        }
        catch { }

        return [.. services];
    }

    internal static async Task<ErrorData> StartDeviceDiscovery(
        OperationToken token,
        int timeout = 0
    )
    {
        var (adapter, error) = AdapterModelExt.ConvertToAdapterModel();
        if (error != Errors.ErrorNone)
        {
            return error;
        }

        try
        {
            ThrowIfRadioNotOperable();

            if (timeout > 0)
                if (!token.ReleaseAfter(timeout))
                    return Errors.ErrorUnexpected;

            var t = Task.Run(() =>
            {
                var adapterEvent = new AdapterEvent(EventAction.Updated) with
                {
                    Address = adapter.Address,
                };

                try
                {
                    Output.Event(adapterEvent with { OptionDiscovering = true }, token);

                    TypedEventHandler<DeviceWatcher, DeviceInformation> addedEvent = new(
                        (s, e) =>
                        {
                            var (device, error) = DeviceModelExt.ConvertToDeviceModel(e);
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
                        BluetoothDevice.GetDeviceSelectorFromPairingState(false),
                        [
                            "System.Devices.Aep.DeviceAddress",
                            "System.Devices.Aep.IsConnected",
                            "System.Devices.Aep.IsPaired",
                        ]
                    );

                    watcher.Added += addedEvent;
                    watcher.Removed += removedEvent;
                    watcher.Updated += updatedEvent;
                    watcher.Start();

                    lock (_discoverylock)
                    {
                        _discoveryToken = token;
                    }
                    token.Wait();
                    lock (_discoverylock)
                    {
                        _discoveryToken = OperationToken.None;
                    }

                    watcher.Stop();
                    watcher.Added -= addedEvent;
                    watcher.Removed -= removedEvent;
                    watcher.Updated -= updatedEvent;
                }
                catch (Exception e)
                {
                    var error = Errors.ErrorDeviceDiscovery.AddMetadata("exception", e.Message);
                    Output.Event(error, token);

                    throw;
                }
                finally
                {
                    Output.Event(adapterEvent with { OptionDiscovering = false }, token);
                }
            });

            await Task.WhenAny(t, Task.Delay(2000));
            if (t.IsFaulted)
                throw t.Exception;

            OperationManager.MarkAsExtended(token);
        }
        catch (OperationCanceledException) { }
        catch (Exception e)
        {
            return Errors.ErrorDeviceDiscovery.WrapError(new() { { "exception", e.Message } });
        }

        return Errors.ErrorNone;
    }

    internal static ErrorData StopDeviceDiscovery()
    {
        lock (_discoverylock)
        {
            if (_discoveryToken == OperationToken.None)
                return Errors.ErrorUnexpected.AddMetadata(
                    "exception",
                    "No device discovery is running."
                );

            _discoveryToken.Release();
            _discoveryToken = OperationToken.None;

            return Errors.ErrorNone;
        }
    }

    internal static void ThrowIfRadioNotOperable()
    {
        if (!HostRadio.IsOperable)
            throw new Exception("The host radio cannot be accessed");
    }
}
