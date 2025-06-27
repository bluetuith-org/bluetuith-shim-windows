using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Data.Models;
using Bluetuith.Shim.Types;
using InTheHand.Net;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Adapters;

internal record class AdapterModelExt : AdapterModel
{
    internal static string CurrentAddress
    {
        get
        {
            string address = "";

            if (Devcon.FindByInterfaceGuid(HostRadio.DeviceInterface, out PnPDevice device))
            {
                address = (
                    (BluetoothAddress)device.GetProperty<ulong>(PnPInformation.Adapter.Address)
                ).ToString("C");
            }
            else
            {
                AdapterMethods.ThrowIfRadioNotOperable();
            }

            return address;
        }
    }

    internal static (AdapterModel Adapter, ErrorData Error) ConvertToAdapterModel(
        OperationToken token = default
    )
    {
        AdapterModel adapter = new();

        return (adapter, adapter.MergeRadioData(token));
    }
}
