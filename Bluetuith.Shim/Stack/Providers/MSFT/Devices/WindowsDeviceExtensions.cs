using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Types;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using Microsoft.Win32;
using Nefarius.Utilities.DeviceManagement.PnP;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices;

internal static class WindowsDeviceExtensions
{
    internal static ErrorData MergeDeviceByAddress<T>(this T device, string address)
        where T : notnull, IDevice
    {
        try
        {
            var parsedAddr = BluetoothAddress.Parse(address);

            using var windowsDevice = BluetoothDevice
                .FromBluetoothAddressAsync(parsedAddr)
                .GetAwaiter()
                .GetResult();
            if (device == null)
            {
                return Errors.ErrorDeviceNotFound;
            }

            return device.MergeBluetoothDevice(windowsDevice, true);
        }
        catch (Exception ex)
        {
            return Errors.ErrorDeviceNotFound.AddMetadata("exception", ex.Message);
        }
    }

    internal static ErrorData MergeBluetoothDevice<T>(
        this T device,
        BluetoothDevice windowsDevice,
        bool appendServices = false
    )
        where T : notnull, IDevice
    {
        try
        {
            ArgumentNullException.ThrowIfNull(windowsDevice);

            device.Name = windowsDevice.Name;
            device.Class = (ClassOfDevice)windowsDevice.ClassOfDevice.RawValue;

            device.OptionPaired = device.OptionBonded = windowsDevice
                .DeviceInformation
                .Pairing
                .IsPaired;

            device.OptionConnected =
                windowsDevice.ConnectionStatus == BluetoothConnectionStatus.Connected;

            if (
                appendServices
                && DeviceUtils.GetServicesFromSdpRecords(windowsDevice.SdpRecords)
                    is Guid[] services
                && services.Length > 0
            )
                device.OptionUUIDs = services;

            device.MergeDeviceInformation(windowsDevice.DeviceInformation);
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceNotFound.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }

    internal static bool MergeDeviceInformation<T>(
        this T device,
        DeviceInformation deviceInformation,
        bool appendServices = false
    )
        where T : notnull, IDevice
    {
        if (deviceInformation == null)
            return false;

        if (appendServices)
        {
            using var windowsDevice = BluetoothDevice
                .FromIdAsync(deviceInformation.Id)
                .GetAwaiter()
                .GetResult();
            return device.MergeBluetoothDevice<T>(windowsDevice, appendServices)
                == Errors.ErrorNone;
        }

        if (
            !DeviceUtils.ParseAepId(
                deviceInformation.Id,
                out var adapterAddress,
                out var deviceAddress
            )
        )
            return false;

        device.Address = deviceAddress;
        device.AssociatedAdapter = adapterAddress;

        foreach (var (aep, value) in deviceInformation.Properties)
        {
            if (value == null)
                continue;

            switch (aep)
            {
                case "System.ItemNameDisplay":
                    device.Name = device.Alias = Convert.ToString(value);
                    break;

                case "System.Devices.Aep.SignalStrength":
                    device.OptionRSSI = Convert.ToInt16(value);
                    break;

                case "System.Devices.Aep.IsConnected":
                    device.OptionConnected = Convert.ToBoolean(value);
                    break;

                case "System.Devices.Aep.IsPaired":
                    device.OptionPaired = device.OptionBonded = Convert.ToBoolean(value);
                    break;
            }
        }

        return true;
    }

    internal static ErrorData MergePnpDevice<T>(this T device, PnPDevice pnpDevice)
        where T : notnull, IDevice
    {
        try
        {
            device.Name = pnpDevice.GetProperty<string>(PnPInformation.Device.Name);
            device.Alias = device.Name;

            var aepId = pnpDevice.GetProperty<string>(PnPInformation.Device.AepId);
            if (!DeviceUtils.ParseAepId(aepId, out var adapterAddress, out var deviceAddress))
                throw new Exception("Cannot parse the device's AEP ID");

            device.Address = deviceAddress;
            device.AssociatedAdapter = AdapterModelExt.CurrentAddress;
            device.Class = pnpDevice.GetProperty<UInt32>(PnPInformation.Device.Class);

            device.OptionConnected = pnpDevice.GetProperty<bool>(PnPInformation.Device.IsConnected);
            device.OptionPaired = device.OptionBonded = true;

            var uuids = new List<Guid>();
            using (
                RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    PnPInformation.Device.ServicesRegistryPath(adapterAddress, deviceAddress),
                    false
                )
            )
            {
                foreach (var services in key?.GetSubKeyNames())
                {
                    if (services is null)
                        continue;

                    uuids.Add(Guid.Parse(services));
                }

                if (uuids.Count > 0)
                {
                    device.OptionUUIDs = uuids.ToArray();
                }
            }
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceNotFound.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }

    internal static ErrorData MergeBatteryInformation<T>(
        this T device,
        string interfaceId,
        int batteryPercentage
    )
        where T : notnull, IDevice
    {
        if (
            batteryPercentage <= 0
            || !PnPUtils.GetAddressFromInterfaceId(interfaceId, out var address)
        )
            return Errors.ErrorUnexpected;

        device.Address = address;
        device.AssociatedAdapter = AdapterModelExt.CurrentAddress;
        device.OptionPercentage = batteryPercentage;

        return Errors.ErrorNone;
    }
}
