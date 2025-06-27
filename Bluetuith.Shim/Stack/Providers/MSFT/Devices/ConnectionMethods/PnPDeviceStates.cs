using Bluetuith.Shim.Stack.Data.Models;
using Bluetuith.Shim.Types;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices.ConnectionMethods;

internal static class PnPDeviceStates
{
    internal static ErrorData Toggle(DeviceModel device)
    {
        try
        {
            var instances = 0;
            var pnpDevices = new List<PnPDevice>();
            var aepIdProperty = PnPInformation.Device.AepId;

            foreach (var profile in device.OptionUUIDs ?? [])
            {
                instances = 0;
                while (
                    Devcon.FindByInterfaceGuid(
                        profile,
                        out var pnpDevice,
                        instances++,
                        HostRadio.IsOperable
                    )
                )
                {
                    var aepId = pnpDevice.GetProperty<string>(aepIdProperty);
                    if (!DeviceUtils.ParseAepId(aepId, out var _, out var deviceAddress))
                        continue;

                    if (device.Address == deviceAddress)
                        pnpDevices.Add(pnpDevice);
                }
            }

            foreach (var pnpDevice in pnpDevices)
                pnpDevice.Disable();
            foreach (var pnpDevice in pnpDevices)
                pnpDevice.Enable();
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceNotConnected.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }
}
