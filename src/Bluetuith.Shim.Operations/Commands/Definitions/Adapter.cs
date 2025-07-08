using Bluetuith.Shim.DataTypes;
using ConsoleAppFramework;

namespace Bluetuith.Shim.Operations;

/// <summary>Perform operations on the Bluetooth adapter.</summary>
[RegisterCommands("adapter")]
public class Adapter
{
    public enum ToggleState : int
    {
        On,
        Off,
    }

    /// <summary>List all available Bluetooth adapters.</summary>
    public int List(ConsoleAppContext context)
    {
        (AdapterModel adapter, ErrorData error) = BluetoothStack.CurrentStack.GetAdapter(
            (OperationToken)context.State
        );

        return Output.Result(
            new List<AdapterModel>() { adapter }.ToResult("Adapters", "adapters"),
            error,
            (OperationToken)context.State
        );
    }

    /// <summary>Get information about the Bluetooth adapter.</summary>
    /// <param name="address">-a, The address of the Bluetooth adapter.</param>
    public int Properties(ConsoleAppContext context, string address)
    {
        (AdapterModel adapter, ErrorData error) = BluetoothStack.CurrentStack.GetAdapter(
            (OperationToken)context.State
        );

        return Output.Result(adapter, error, (OperationToken)context.State);
    }

    /// <summary>Get the list of paired devices.</summary>
    /// <param name="address">-a, The address of the Bluetooth adapter.</param>
    public int GetPairedDevices(ConsoleAppContext context, string address)
    {
        (GenericResult<List<DeviceModel>> devices, ErrorData error) =
            BluetoothStack.CurrentStack.GetPairedDevices();

        return Output.Result(devices, error, (OperationToken)context.State);
    }

    /// <summary>Sets the power state of the adapter.</summary>
    /// <param name="state">-s, The adapter power state to set.</param>
    /// <param name="address">-a, The address of the Bluetooth adapter.</param>
    public int SetPoweredState(ConsoleAppContext context, ToggleState state, string address)
    {
        ErrorData error = BluetoothStack.CurrentStack.SetPoweredState(state == ToggleState.On);

        return Output.Error(error, (OperationToken)context.State);
    }

    /// <summary>Sets the pairable state of the adapter.</summary>
    /// <param name="state">-s, The adapter pairable state to set.</param>
    /// <param name="address">-a, The address of the Bluetooth adapter.</param>
    public int SetPairableState(ConsoleAppContext context, ToggleState state, string address)
    {
        ErrorData error = BluetoothStack.CurrentStack.SetPairableState(state == ToggleState.On);

        return Output.Error(error, (OperationToken)context.State);
    }

    /// <summary>Sets the discoverable state of the adapter.</summary>
    /// <param name="state">-s, The adapter discoverable state to set.</param>
    /// <param name="address">-a, The address of the Bluetooth adapter.</param>
    public int SetDiscoverableState(ConsoleAppContext context, ToggleState state, string address)
    {
        ErrorData error = BluetoothStack.CurrentStack.SetDiscoverableState(state == ToggleState.On);

        return Output.Error(error, (OperationToken)context.State);
    }
}

/// <summary>Perform device discovery operations on the Bluetooth adapter.</summary>
[RegisterCommands("adapter discovery")]
public class Discovery
{
    /// <summary>Start a device discovery.</summary>
    /// <param name="address">-a, The address of the Bluetooth adapter.</param>
    /// <param name="timeout">-t, A value in seconds which determines when the device discovery will finish.</param>
    public int Start(ConsoleAppContext context, string address, int timeout = 0)
    {
        ErrorData error = BluetoothStack.CurrentStack.StartDeviceDiscovery(
            (OperationToken)context.State,
            timeout
        );

        return Output.Error(error, (OperationToken)context.State);
    }

    [Hidden]
    /// <summary>Stop a device discovery (RPC only).</summary>
    /// <param name="address">-a, The address of the Bluetooth adapter.</param>
    public int Stop(ConsoleAppContext context, string address)
    {
        ErrorData error = BluetoothStack.CurrentStack.StopDeviceDiscovery(
            (OperationToken)context.State
        );

        return Output.Error(error, (OperationToken)context.State);
    }
}
