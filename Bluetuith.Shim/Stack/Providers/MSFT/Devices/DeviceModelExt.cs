using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Types;
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

        return (device, device.MergeBluetoothDevice(windowsDevice, appendServices));
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(
        DeviceInformation deviceInformation,
        bool appendServices = false
    )
    {
        DeviceModel device = new();

        return (
            device,
            device.MergeDeviceInformation(deviceInformation, appendServices)
                ? Errors.ErrorNone
                : Errors.ErrorUnexpected.AddMetadata(
                    "exception",
                    "Could not parse device information"
                )
        );
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(PnPDevice pnpDevice)
    {
        DeviceModelExt device = new();

        return (device, device.MergePnpDevice(pnpDevice));
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(
        string interfaceId,
        int batteryPercentage
    )
    {
        DeviceModel device = new DeviceModel();

        return (device, device.MergeBatteryInformation(interfaceId, batteryPercentage));
    }

    internal static (DeviceModel, ErrorData) ConvertToDeviceModel(string address)
    {
        DeviceModel device = new();

        return (device, device.MergeDeviceByAddress(address));
    }
}
