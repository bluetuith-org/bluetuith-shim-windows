using System.Runtime.InteropServices;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices;
using Bluetuith.Shim.Stack.Providers.MSFT.Monitors;
using Bluetuith.Shim.Types;
using DotNext;
using InTheHand.Net;
using Microsoft.Win32;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;
using Vanara.PInvoke;
using Windows.Devices.Bluetooth;
using static Vanara.PInvoke.Kernel32;

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

    [DllImport(
        "BluetoothAPIs.dll",
        SetLastError = true,
        CallingConvention = CallingConvention.StdCall
    )]
    [return: MarshalAs(UnmanagedType.U4)]
    static extern UInt32 BluetoothRemoveDevice(IntPtr pAddress);

    internal static async Task<ErrorData> RemoveDeviceAsync(string address)
    {
        try
        {
            using BluetoothDevice device = await DeviceUtils.GetBluetoothDevice(address);
            if (!device.DeviceInformation.Pairing.IsPaired)
                if (device == null)
                {
                    return Errors.ErrorDeviceNotFound;
                }

            using BluetoothLEDevice device1 = await BluetoothLEDevice.FromBluetoothAddressAsync(
                BluetoothAddress.Parse(address)
            );
            if (device1 != null)
            {
                await device1.DeviceInformation.Pairing.UnpairAsync();
            }

            var addr = BluetoothAddress.Parse(address);
            var addrUlong = addr.ToUInt64();

            GCHandle gCHandle = GCHandle.Alloc(addrUlong, GCHandleType.Pinned);
            IntPtr pinnedAddr = gCHandle.AddrOfPinnedObject();
            var res = BluetoothRemoveDevice(pinnedAddr);
            gCHandle.Free();

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

    internal static ErrorData SetDiscoverableState(bool enable)
    {
        short[] scanFlags = [0x0103, (short)(enable ? 1 : 0)];

        GCHandle gCHandle = default;
        SafeHFILE _radioHandle = null;

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

            _radioHandle = Kernel32.CreateFile(
                path,
                (Kernel32.FileAccess.FILE_GENERIC_READ | Kernel32.FileAccess.FILE_GENERIC_WRITE),
                FileShare.ReadWrite,
                null,
                FileMode.Open,
                FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL,
                null
            );

            gCHandle = GCHandle.Alloc(scanFlags, GCHandleType.Pinned);
            IntPtr pinnedAddr = gCHandle.AddrOfPinnedObject();

            var ret = Kernel32.DeviceIoControl(
                _radioHandle,
                0x411020,
                pinnedAddr,
                (uint)(scanFlags.Length * 2),
                IntPtr.Zero,
                0u,
                out _,
                IntPtr.Zero
            );

            if (!ret)
                throw new Exception(
                    "Could not set discovery state: Win32 error: " + Marshal.GetLastWin32Error()
                );

            using (
                RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    PnPInformation.Adapter.DeviceParametersRegistryPath(instanceId),
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
        catch (Exception ex)
        {
            return Errors.ErrorAdapterStateAccess.AddMetadata("exception", ex);
        }
        finally
        {
            if (gCHandle != default)
                gCHandle.Free();

            if (_radioHandle != null)
                _radioHandle.Close();
        }

        return Errors.ErrorNone;
    }

    [DllImport("bthprops.cpl", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool BluetoothIsDiscoverable(IntPtr hRadio);

    internal static Optional<bool> GetDiscoverableState()
    {
        var discoverable = Optional.None<bool>();

        SafeHFILE _radioHandle = null;

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

            _radioHandle = Kernel32.CreateFile(
                path,
                (Kernel32.FileAccess.FILE_GENERIC_READ | Kernel32.FileAccess.FILE_GENERIC_WRITE),
                FileShare.ReadWrite,
                null,
                FileMode.Open,
                FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL,
                null
            );

            discoverable = BluetoothIsDiscoverable(((nint)_radioHandle));
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
