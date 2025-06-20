using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Types;
using DotNext;
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

    private AdapterModelExt() { }

    internal AdapterModelExt(ulong address, Optional<bool> powered, Optional<bool> discoverable)
    {
        Address = ((BluetoothAddress)address).ToString("C");
        OptionPowered = OptionPairable = powered;
        OptionDiscoverable = discoverable;
    }

    internal static (AdapterModel Adapter, ErrorData Error) ConvertToAdapterModel(
        OperationToken token = default
    )
    {
        AdapterModel adapter = new();

        return (adapter, adapter.MergeRadioData(token));
    }
}
