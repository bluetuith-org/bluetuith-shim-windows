using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Types;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using Microsoft.Win32;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices;

internal record class DeviceModelExt : DeviceModel
{
    private DeviceModelExt() { }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(
        BluetoothDevice windowsDevice,
        bool appendServices = false
    )
    {
        DeviceModelExt device = new();

        try
        {
            device.Name = windowsDevice.Name;
            device.Class = (ClassOfDevice)windowsDevice.ClassOfDevice.RawValue;

            if (
                appendServices
                && DeviceUtils.GetServicesFromSdpRecords(windowsDevice.SdpRecords)
                    is Guid[] services
                && services.Length > 0
            )
                device.OptionUUIDs = services;

            ParseDeviceInformation(
                device,
                windowsDevice.DeviceInformation.Id,
                windowsDevice.DeviceInformation.Properties
            );
        }
        catch (Exception e)
        {
            return (device, Errors.ErrorDeviceNotFound.AddMetadata("exception", e.Message));
        }

        return (device, Errors.ErrorNone);
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(
        DeviceInformation deviceInformation
    )
    {
        DeviceModelExt device = new();

        try
        {
            if (!ParseDeviceInformation(device, deviceInformation.Id, deviceInformation.Properties))
                return (device, Errors.ErrorDeviceNotFound);

            device.Name = device.Alias = deviceInformation.Name;
        }
        catch (Exception e)
        {
            return (device, Errors.ErrorDeviceNotFound.AddMetadata("exception", e.Message));
        }

        return (device, Errors.ErrorNone);
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(
        DeviceInformationUpdate deviceInformation
    )
    {
        DeviceModelExt device = new();

        try
        {
            if (!ParseDeviceInformation(device, deviceInformation.Id, deviceInformation.Properties))
                return (device, Errors.ErrorDeviceNotFound);
        }
        catch (Exception e)
        {
            return (device, Errors.ErrorDeviceNotFound.AddMetadata("exception", e.Message));
        }

        return (device, Errors.ErrorNone);
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(PnPDevice pnpDevice)
    {
        DeviceModelExt device = new();

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
            return (device, Errors.ErrorDeviceNotFound.AddMetadata("exception", e.Message));
        }

        return (device, Errors.ErrorNone);
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(
        string interfaceId,
        int batteryPercentage
    )
    {
        (DeviceModelExt, ErrorData) result = (new(), Errors.ErrorDeviceNotFound);

        if (
            batteryPercentage <= 0
            || !PnPUtils.GetAddressFromInterfaceId(interfaceId, out var address)
        )
            return result;

        result.Item1.Address = address;
        result.Item1.AssociatedAdapter = AdapterModelExt.CurrentAddress;
        result.Item1.OptionPercentage = batteryPercentage;
        result.Item2 = Errors.ErrorNone;

        return result;
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(string address)
    {
        (DeviceModel, ErrorData) result = (default, Errors.ErrorDeviceNotFound);

        try
        {
            var parsedAddr = BluetoothAddress.Parse(address);
            address = parsedAddr.ToString("C").ToLower();

            var instances = 0;
            var aepIdProperty = PnPInformation.Device.AepId;

            while (
                Devcon.FindByInterfaceGuid(
                    PnPInformation.BluetoothDevicesInterfaceGuid,
                    out var pnpDevice,
                    instances++,
                    HostRadio.IsOperable
                )
            )
            {
                var aepId = pnpDevice.GetProperty<string>(aepIdProperty);
                if (!DeviceUtils.ParseAepId(aepId, out var _, out var deviceAddress))
                    continue;

                if (address == deviceAddress)
                {
                    result = ConvertToDeviceModel(pnpDevice);
                    break;
                }
            }
        }
        catch (Exception e)
        {
            return (result.Item1, Errors.ErrorDeviceNotFound.AddMetadata("exception", e.Message));
        }

        return result;
    }

    private static bool ParseDeviceInformation(
        DeviceModelExt device,
        string id,
        IReadOnlyDictionary<string, object> properties
    )
    {
        if (properties == null)
            return false;

        if (!DeviceUtils.ParseAepId(id, out var adapterAddress, out var deviceAddress))
            return false;

        device.Address = deviceAddress;
        device.AssociatedAdapter = adapterAddress;

        foreach (var (aep, value) in properties)
        {
            if (value == null)
                continue;

            switch (aep)
            {
                case "System.ItemNameDisplay":
                    device.Name = Convert.ToString(value);
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
}
