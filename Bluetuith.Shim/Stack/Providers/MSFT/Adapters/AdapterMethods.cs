using System.Runtime.InteropServices;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Data.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices;
using Bluetuith.Shim.Stack.Providers.MSFT.Monitors;
using Bluetuith.Shim.Types;
using Microsoft.Win32;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;
using Windows.Devices.Bluetooth;
using Windows.Win32;
using Windows.Win32.Devices.Bluetooth;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

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
                return Errors.ErrorDeviceNotFound;

            using BluetoothLEDevice device1 = await BluetoothLEDevice.FromBluetoothAddressAsync(
                device.BluetoothAddress
            );
            if (device1 != null)
                await device1.DeviceInformation.Pairing.UnpairAsync();

            var addressParam = new BLUETOOTH_ADDRESS();
            addressParam.Anonymous.ullLong = device.BluetoothAddress;

            var res = PInvoke.BluetoothRemoveDevice(addressParam);
            if (res != 0)
            {
                throw new Exception($"could not unpair device with address {address}");
            }
        }
        catch (Exception ex)
        {
            return Errors.ErrorDeviceUnpairing.AddMetadata("exception", ex);
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

    internal static unsafe ErrorData SetDiscoverableState(bool enable)
    {
        SafeHandle _radioHandle = null;

        try
        {
            if (
                !Devcon.FindByInterfaceGuid(
                    HostRadio.DeviceInterface,
                    out string path,
                    out string instanceId
                )
            )
                throw new Exception("No adapter found");

            _radioHandle = PInvoke.CreateFile(
                path,
                (uint)FileAccess.ReadWrite,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                null
            );

            short[] scanFlags = [0x0103, (short)(enable ? 1 : 0)];
            fixed (void* ptr = scanFlags)
            {
                BOOL ret = PInvoke.DeviceIoControl(
                    _radioHandle,
                    0x411020,
                    ptr,
                    (uint)(scanFlags.Length * 2),
                    null,
                    0u,
                    null,
                    null
                );

                if (!ret)
                    throw new Exception(
                        "Could not set discovery state: Win32 error: " + Marshal.GetLastWin32Error()
                    );
            }
        }
        catch (Exception ex)
        {
            return Errors.ErrorAdapterStateAccess.AddMetadata("exception", ex);
        }
        finally
        {
            _radioHandle?.Close();
        }

        return Errors.ErrorNone;
    }

    internal static Nullable<bool> GetDiscoverableState()
    {
        var discoverable = false;

        SafeHandle _radioHandle = null;

        try
        {
            if (
                !Devcon.FindByInterfaceGuid(
                    HostRadio.DeviceInterface,
                    out string path,
                    out string instanceId
                )
            )
                throw new Exception("No adapter found");

            _radioHandle = PInvoke.CreateFile(
                path,
                (uint)FileAccess.ReadWrite,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                null
            );

            discoverable = PInvoke.BluetoothIsDiscoverable(_radioHandle) > 0;
        }
        catch { }
        finally
        {
            _radioHandle?.Close();
        }

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

    internal static ErrorData StartDeviceDiscovery(OperationToken token, int timeout = 0)
    {
        try
        {
            ThrowIfRadioNotOperable();

            if (timeout > 0)
                if (!token.ReleaseAfter(timeout))
                    return Errors.ErrorUnexpected;

            return DiscoveryMonitor.Start(token);
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceDiscovery.WrapError(new() { { "exception", e.Message } });
        }
    }

    internal static ErrorData StopDeviceDiscovery(OperationToken token)
    {
        return DiscoveryMonitor.Stop(token);
    }

    internal static void ThrowIfRadioNotOperable()
    {
        if (!HostRadio.IsOperable)
            throw new Exception("The host radio cannot be accessed");
    }
}
