using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Monitors;
using Bluetuith.Shim.Types;
using InTheHand.Net;
using Nefarius.Utilities.Bluetooth;
using Nefarius.Utilities.DeviceManagement.PnP;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Adapters;

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
                    (BluetoothAddress)device.GetProperty<ulong>(PnPInformation.Adapter.Address)
                ).ToString("C");

                adapter.OptionPowered = adapter.OptionPairable = HostRadio.IsOperable;

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
