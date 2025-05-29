using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices;
using Bluetuith.Shim.Stack.Providers.MSFT.StackHelper;
using Bluetuith.Shim.Types;
using DotNext;
using Microsoft.Win32;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Adapters;

internal static class AdapterMethods
{
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
                if (convertError != Errors.ErrorNone)
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
        try
        {
            ThrowIfRadioNotOperable();

            if (timeout > 0)
                if (!token.ReleaseAfter(timeout))
                    return Errors.ErrorUnexpected;

            return await UnpairedDevicesWatcher.StartEmitter(token);
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceDiscovery.WrapError(new() { { "exception", e.Message } });
        }
    }

    internal static ErrorData StopDeviceDiscovery()
    {
        return UnpairedDevicesWatcher.StopEmitter();
    }

    internal static void ThrowIfRadioNotOperable()
    {
        if (!HostRadio.IsOperable)
            throw new Exception("The host radio cannot be accessed");
    }
}
