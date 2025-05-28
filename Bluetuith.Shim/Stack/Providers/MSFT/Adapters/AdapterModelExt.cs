using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Types;
using DotNext;
using InTheHand.Net;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Adapters;

internal record class AdapterModelExt : AdapterModel
{
    private record struct adapterInfo
    {
        internal BluetoothAddress address;
        internal string name;
        internal bool isPowered;
        internal Guid[] services;
    }

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

    private AdapterModelExt(adapterInfo info)
    {
        Address = info.address.ToString("C");
        Name = Alias = UniqueName = info.name;

        OptionPowered = OptionPairable = info.isPowered;
        OptionDiscoverable = AdapterMethods.GetDiscoverableState();
        UUIDs = info.services;
    }

    internal AdapterModelExt(ulong address, Optional<bool> powered, Optional<bool> discoverable)
    {
        Address = ((BluetoothAddress)address).ToString("C");
        OptionPowered = OptionPairable = powered;
        OptionDiscoverable = discoverable;
    }

    internal static (AdapterModel Adapter, ErrorData Error) ConvertToAdapterModel()
    {
        AdapterModel adapter = new();
        adapterInfo info = new()
        {
            address = BluetoothAddress.None,
            name = "",
            isPowered = false,
        };

        try
        {
            if (Devcon.FindByInterfaceGuid(HostRadio.DeviceInterface, out PnPDevice device))
            {
                info.name = device.GetProperty<string>(DevicePropertyKey.NAME);
                info.isPowered = HostRadio.IsOperable;
                info.address = device.GetProperty<ulong>(PnPInformation.Adapter.Address);
                info.services = AdapterMethods.GetAdapterServices();

                adapter = new AdapterModelExt(info);
            }
            else
            {
                AdapterMethods.ThrowIfRadioNotOperable();
            }
        }
        catch (Exception e)
        {
            return (adapter, Errors.ErrorAdapterNotFound.AddMetadata("exception", e.Message));
        }

        return (adapter, Errors.ErrorNone);
    }
}
