using Bluetuith.Shim.DataTypes;
using ConsoleAppFramework;

namespace Bluetuith.Shim.Operations;

/// <summary>Perform operations on a Bluetooth device.</summary>
[RegisterCommands("device")]
public class Device
{
    /// <summary>Get information about a Bluetooth device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public int Properties(ConsoleAppContext context, string address)
    {
        (DeviceModel device, ErrorData error) = BluetoothStack.CurrentStack.GetDevice(
            (OperationToken)context.State,
            address
        );

        return Output.Result(device, error, (OperationToken)context.State);
    }

    /// <summary>Disconnect from a Bluetooth device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public int Disconnect(ConsoleAppContext context, string address)
    {
        ErrorData error = BluetoothStack.CurrentStack.DisconnectDevice(address);

        return Output.Error(error, (OperationToken)context.State);
    }

    /// <summary>Remove a Bluetooth device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> Remove(ConsoleAppContext context, string address)
    {
        ErrorData error = await BluetoothStack.CurrentStack.RemoveDeviceAsync(address);

        return Output.Error(error, (OperationToken)context.State);
    }
}

[RegisterCommands("device pair")]
public class Pairing
{
    /// <summary>Pair with a Bluetooth device.</summary>
    /// <param name="timeout">-t, The maximum amount of time in seconds that a pairing request can wait for a reply during authentication.</param>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    [Command("")]
    public async Task<int> Pair(ConsoleAppContext context, string address, int timeout = 10)
    {
        ErrorData error = await BluetoothStack.CurrentStack.PairAsync(
            (OperationToken)context.State,
            address,
            timeout
        );

        return Output.Error(error, (OperationToken)context.State);
    }

    [Hidden]
    /// <summary>Cancel pairing with a Bluetooth device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public int Cancel(ConsoleAppContext context, string address)
    {
        ErrorData error = BluetoothStack.CurrentStack.CancelPairing(address);

        return Output.Error(error, (OperationToken)context.State);
    }
}

[RegisterCommands("device connect")]
public class Connection
{
    /// <summary>Connect to a Bluetooth device. If this command is used without any subcommands and an address parameter is specified, it will try to connect to the specified device automatically.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    [Command("")]
    public int Connect(ConsoleAppContext context, string address)
    {
        ErrorData error = BluetoothStack.CurrentStack.Connect(
            (OperationToken)context.State,
            address
        );

        return Output.Error(error, (OperationToken)context.State);
    }

    /// <summary>Connect to a device using a specified profile"</summary>
    /// <param name="uuid">-u, The Bluetooth service profile as a UUID.</param>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public int Profile(ConsoleAppContext context, string address, Guid uuid)
    {
        ErrorData error = BluetoothStack.CurrentStack.ConnectProfile(
            (OperationToken)context.State,
            address,
            uuid
        );

        return Output.Error(error, (OperationToken)context.State);
    }
}

[RegisterCommands("device a2dp")]
public class A2dp
{
    /// <summary>Start an A2DP session with a Bluetooth device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> StartSession(ConsoleAppContext context, string address)
    {
        ErrorData error = await BluetoothStack.CurrentStack.StartAudioSessionAsync(
            (OperationToken)context.State,
            address
        );

        return Output.Error(error, (OperationToken)context.State);
    }
}

/// <summary>Perform a Object Push Profile based operation.</summary>
[RegisterCommands("device opp")]
public class Opp
{
    [Hidden]
    /// <summary>Start an Object Push client session with a device (RPC mode only).</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> StartSession(ConsoleAppContext context, string address)
    {
        ErrorData error = await BluetoothStack.CurrentStack.StartFileTransferSessionAsync(
            (OperationToken)context.State,
            address
        );

        return Output.Error(error, (OperationToken)context.State);
    }

    [Hidden]
    /// <summary>Send a file to a device with an open session. (RPC mode only).</summary>
    /// <param name="file">-f, A full path of the file to send.</param>
    public int SendFile(ConsoleAppContext context, string address, string file)
    {
        var token = (OperationToken)context.State;
        var (filetransfer, error) = BluetoothStack.CurrentStack.QueueFileSend(token, address, file);

        return Output.Result(filetransfer, error, token);
    }

    [Hidden]
    /// <summary>Cancel an Object Push file transfer with a device (RPC mode only).</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public int CancelTransfer(ConsoleAppContext context, string address)
    {
        var token = (OperationToken)context.State;

        var error = BluetoothStack.CurrentStack.CancelFileTransfer(token, address);

        return Output.Error(error, token);
    }

    [Hidden]
    /// <summary>Suspend an Object Push file transfer with a device (no-op, RPC mode only).</summary>
    public int SuspendTransfer(ConsoleAppContext context)
    {
        return Output.Error(Errors.ErrorUnsupported, (OperationToken)context.State);
    }

    [Hidden]
    /// <summary>Resume an Object Push file transfer with a device (no-op, RPC mode only).</summary>
    public int ResumeTransfer(ConsoleAppContext context)
    {
        return Output.Error(Errors.ErrorUnsupported, (OperationToken)context.State);
    }

    [Hidden]
    /// <summary>Stop an Object Push client session with a device (RPC mode only).</summary>
    public int StopSession(ConsoleAppContext context, string address)
    {
        var token = (OperationToken)context.State;

        return Output.Error(
            BluetoothStack.CurrentStack.StopFileTransferSession(token, address),
            token
        );
    }
}

/// <summary>Perform a Phonebook Profile based operation.</summary>
[RegisterCommands("device pbap")]
public class Pbap
{
    /// <summary>Get the contacts list from a device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> GetAllContacts(ConsoleAppContext context, string address)
    {
        (VcardModel vcard, ErrorData error) = await BluetoothStack.CurrentStack.GetAllContactsAsync(
            (OperationToken)context.State,
            address
        );

        return Output.Result(vcard, error, (OperationToken)context.State);
    }

    /// <summary>Get the combined call history from a device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> GetCombinedCalls(ConsoleAppContext context, string address)
    {
        (VcardModel vcard, ErrorData error) =
            await BluetoothStack.CurrentStack.GetCombinedCallHistoryAsync(
                (OperationToken)context.State,
                address
            );

        return Output.Result(vcard, error, (OperationToken)context.State);
    }

    /// <summary>Get the incoming call history from a device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> GetIncomingCalls(ConsoleAppContext context, string address)
    {
        (VcardModel vcard, ErrorData error) =
            await BluetoothStack.CurrentStack.GetIncomingCallsHistoryAsync(
                (OperationToken)context.State,
                address
            );

        return Output.Result(vcard, error, (OperationToken)context.State);
    }

    /// <summary>Get the outgoing call history from a device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> GetOutgoingCalls(ConsoleAppContext context, string address)
    {
        (VcardModel vcard, ErrorData error) =
            await BluetoothStack.CurrentStack.GetOutgoingCallsHistoryAsync(
                (OperationToken)context.State,
                address
            );

        return Output.Result(vcard, error, (OperationToken)context.State);
    }

    /// <summary>Get the missed call history from a device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> GetMissedCalls(ConsoleAppContext context, string address)
    {
        (VcardModel vcard, ErrorData error) = await BluetoothStack.CurrentStack.GetMissedCallsAsync(
            (OperationToken)context.State,
            address
        );

        return Output.Result(vcard, error, (OperationToken)context.State);
    }

    /// <summary>Get the speed dial list from a device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> GetSpeedDial(ConsoleAppContext context, string address)
    {
        (VcardModel vcard, ErrorData error) = await BluetoothStack.CurrentStack.GetSpeedDialAsync(
            (OperationToken)context.State,
            address
        );

        return Output.Result(vcard, error, (OperationToken)context.State);
    }
}

/// <summary>Perform a Message Access Profile based operation.</summary>
[RegisterCommands("device map")]
public class Map
{
    /// <summary>Get a list of messages from a Bluetooth device.</summary>
    /// <param name="folder">-f, A path to a folder in the remote device to list messages from, for example, 'telecom/msg/inbox'.</param>
    /// <param name="index">-i, The index of the message within a folder listing, from where messages can start being listed.</param>
    /// <param name="count">-c, The maximum amount of messages to display. Setting bigger values will lead to longer download times.</param>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> GetMessages(
        ConsoleAppContext context,
        string address,
        string folder = "telecom",
        ushort index = 0,
        ushort count = 1024
    )
    {
        (MessageListingModel list, ErrorData error) =
            await BluetoothStack.CurrentStack.GetMessagesAsync(
                (OperationToken)context.State,
                address,
                folder,
                index,
                count
            );

        return Output.Result(list, error, (OperationToken)context.State);
    }

    /// <summary>Get a list of message folders from a Bluetooth device.</summary>
    /// <param name="folder">-f, A path to a folder in the remote device to list folders from, for example, 'telecom/msg'.</param>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> GetMessagefolders(
        ConsoleAppContext context,
        string address,
        string folder
    )
    {
        (GenericResult<List<string>> folders, ErrorData error) =
            await BluetoothStack.CurrentStack.GetMessageFoldersAsync(
                (OperationToken)context.State,
                address,
                folder
            );

        return Output.Result(folders, error, (OperationToken)context.State);
    }

    /// <summary>Show a message from a Bluetooth device.</summary>
    /// <param name="handle">-h, A message handle of the message to be displayed. Use 'get-messages' to list message handles.</param>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> ShowMessage(ConsoleAppContext context, string address, string handle)
    {
        (BMessagesModel message, ErrorData error) =
            await BluetoothStack.CurrentStack.ShowMessageAsync(
                (OperationToken)context.State,
                address,
                handle
            );

        return Output.Result(message, error, (OperationToken)context.State);
    }

    /// <summary>Listen for notification events from a Bluetooth device.</summary>
    /// <param name="address">-a, The address of the Bluetooth device.</param>
    public async Task<int> StartNotificationServer(ConsoleAppContext context, string address)
    {
        ErrorData error = await BluetoothStack.CurrentStack.StartNotificationEventServerAsync(
            (OperationToken)context.State,
            address
        );

        return Output.Error(error, (OperationToken)context.State);
    }
}
