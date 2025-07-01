using Bluetuith.Shim.DataTypes;
using InTheHand.Net;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace Bluetuith.Shim.Stack.Microsoft;

internal static class WindowsAdapterExtensions
{
    public static ErrorData MergeRadioData<T>(this T adapter, OperationToken token = default)
        where T : notnull, IAdapter
    {
        return adapter.MergePnpDevice(false, token);
    }

    public static ErrorData MergeRadioEventData<T>(this T adapter, OperationToken token = default)
        where T : notnull, IAdapter
    {
        return adapter.MergePnpDevice(true, token);
    }

    public static void MergeWatcherData<T>(
        this T adapter,
        ulong address,
        Nullable<bool> powered,
        Nullable<bool> discoverable
    )
        where T : notnull, IAdapter
    {
        adapter.Address = ((BluetoothAddress)address).ToString("C");
        adapter.OptionPowered = adapter.OptionPairable = powered;
        adapter.OptionDiscoverable = discoverable;
    }

    public static ErrorData MergePnpDevice<T>(
        this T adapter,
        bool eventOnly,
        OperationToken token = default
    )
        where T : notnull, IAdapter
    {
        try
        {
            if (Devcon.FindByInterfaceGuid(HostRadio.DeviceInterface, out PnPDevice device))
            {
                adapter.Address = (
                    (BluetoothAddress)
                        device.GetProperty<ulong>(WindowsPnPInformation.Adapter.Address)
                ).ToString("C");

                adapter.OptionPowered = adapter.OptionPairable = HostRadio.IsOperable;
                adapter.OptionDiscoverable = AdapterMethods.GetDiscoverableState();

                if (token != default)
                    adapter.OptionDiscovering = DiscoveryMonitor.IsStarted(token);

                if (!eventOnly)
                {
                    adapter.Name =
                        adapter.Alias =
                        adapter.UniqueName =
                            device.GetProperty<string>(DevicePropertyKey.NAME);
                    adapter.UUIDs = AdapterMethods.GetAdapterServices();
                }
            }
            else
            {
                AdapterMethods.ThrowIfRadioNotOperable();
            }
        }
        catch (Exception e)
        {
            return Errors.ErrorAdapterNotFound.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }
}
