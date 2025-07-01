using Bluetuith.Shim.DataTypes;
using DotMake.CommandLine;

namespace Bluetuith.Shim.Operations;

[CliCommand(Description = "Bluetooth shim and command-line utility.")]
public sealed class CommandDefinitions
{
    public static readonly IBluetoothStack SelectedStack = BluetoothStack.CurrentStack;

    [CliOption(Required = true, Hidden = true, Name = "--request-id")]
    public required long RequestId { get; set; }

    [CliOption(Required = true, Hidden = true, Name = "--operation-id")]
    public required long OperationId { get; set; }

    [CliCommand(Description = "Manage new/existing RPC server services.")]
    public class Server
    {
        [CliCommand(
            Description = "Start a shim server instance which will open a UNIX socket to perform RPC related functions."
        )]
        public class Start
        {
            [CliOption(
                Required = false,
                Description = "The full path and the socket name to be created and listened on."
            )]
            public string SocketPath { get; set; } =
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "bh-shim",
                    "bh-shim.sock"
                );

            [CliOption(Hidden = true)]
            public bool Tray { get; set; } = false;

            public int Run(CliContext context)
            {
                if (Output.IsOnSocket)
                    return Errors.ErrorUnsupported.Code.Value;

                var error = ServerHost.Instance.StartServer(
                    SocketPath,
                    Tray ? "" : string.Join(" ", context.ParseResult.Tokens) + " -t"
                );
                if (error != Errors.ErrorNone)
                    Output.Error(error, OperationToken.None);

                return error.Code.Value;
            }
        }

        [CliCommand(Description = "Stop all shim server instances in the current user session.")]
        public class Stop
        {
            [CliOption(Hidden = true)]
            public bool Tray { get; set; } = false;

            public int Run(CliContext context)
            {
                if (Output.IsOnSocket)
                    return Errors.ErrorUnsupported.Code.Value;

                var error = ServerHost.Instance.StopServer(
                    Tray ? "" : string.Join(" ", context.ParseResult.Tokens) + " -t"
                );
                if (error != Errors.ErrorNone)
                    Output.Error(error, OperationToken.None);

                return error.Code.Value;
            }
        }
    }

    [CliCommand(
        Hidden = true,
        Description = "Perform functions related to an ongoing RPC operation within a session."
    )]
    public class Rpc
    {
        [CliCommand(
            Hidden = true,
            Description = "Get the platform-specific information of the Bluetooth stack and the Operating System the server is running on."
        )]
        public class PlatformInfo
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        return Output.Result(
                            SelectedStack.GetPlatformInfo(),
                            Errors.ErrorNone,
                            token
                        );
                    }
                );
            }
        }

        [CliCommand(Hidden = true, Description = "Show the features of the RPC server.")]
        public class FeatureFlags
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        var features = SelectedStack.GetFeatureFlags();

                        return Output.Result(features, Errors.ErrorNone, token);
                    }
                );
            }
        }

        [CliCommand(
            Hidden = true,
            Description = "Show the current version information of the RPC server."
        )]
        public class Version
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        System.Version version =
                            typeof(CommandDefinitions).Assembly.GetName().Version ?? new();

                        return Output.Result(
                            version.ToString().ToResult("Version", "version"),
                            Errors.ErrorNone,
                            token
                        );
                    }
                );
            }
        }

        [CliCommand(
            Hidden = true,
            Description = "Set the response for a pending authentication request attached to an operation ID."
        )]
        public class Auth
        {
            [CliOption(
                Required = true,
                Description = "The response to sent to the authentication request."
            )]
            public string Response { get; set; } = "";

            [CliOption(Required = true, Description = "The ID of the authentication request.")]
            public ushort AuthenticationId { get; set; }

            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        if (!Output.ReplyToAuthenticationRequest(AuthenticationId, Response))
                            return Output.Error(
                                Errors.ErrorOperationCancelled.AddMetadata(
                                    "exception",
                                    "No authentication ID found: " + AuthenticationId
                                ),
                                token
                            );

                        return Output.Error(Errors.ErrorNone, token);
                    }
                );
            }
        }
    }

    [CliCommand(Description = "Perform operations on the Bluetooth adapter.")]
    public class Adapter
    {
        public class AddressParameter
        {
            [CliOption(Required = false, Description = "The Bluetooth address of the adapter.")]
            public string Address { get; set; } = "";
        }

        public enum ToggleState : int
        {
            On,
            Off,
        }

        [CliCommand(Description = "List all available Bluetooth adapters.")]
        public class List
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        (AdapterModel adapter, ErrorData error) = SelectedStack.GetAdapter(token);

                        return Output.Result(
                            new List<AdapterModel>() { adapter }.ToResult("Adapters", "adapters"),
                            error,
                            token
                        );
                    }
                );
            }
        }

        [CliCommand(Description = "Get information about the Bluetooth adapter.")]
        public class Properties : AddressParameter
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        (AdapterModel adapter, ErrorData error) = SelectedStack.GetAdapter(token);

                        return Output.Result(adapter, error, token);
                    }
                );
            }
        }

        [CliCommand(Description = "Perform device discovery operations on the Bluetooth adapter.")]
        public class Discovery
        {
            [CliCommand(Description = "Start a device discovery.")]
            public class Start : AddressParameter
            {
                [CliOption(
                    Description = "A value in seconds which determines when the device discovery will finish."
                )]
                public int Timeout { get; set; }

                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        (token) =>
                        {
                            ErrorData error = SelectedStack.StartDeviceDiscovery(token, Timeout);

                            return Output.Error(error, token);
                        }
                    );
                }
            }

            [CliCommand(Hidden = true, Description = "Stop a device discovery (RPC only).")]
            public class Stop : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        int (token) =>
                        {
                            ErrorData error = SelectedStack.StopDeviceDiscovery(token);

                            return Output.Error(error, token);
                        }
                    );
                }
            }
        }

        [CliCommand(Description = "Get the list of paired devices.")]
        public class GetPairedDevices : AddressParameter
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        (GenericResult<List<DeviceModel>> devices, ErrorData error) =
                            SelectedStack.GetPairedDevices();

                        return Output.Result(devices, error, token);
                    }
                );
            }
        }

        [CliCommand(Description = "Sets the power state of the adapter.")]
        public class SetPoweredState : AddressParameter
        {
            [CliOption(Description = "The adapter power state to set.")]
            public ToggleState State { get; set; }

            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        ErrorData error = SelectedStack.SetPoweredState(State == ToggleState.On);

                        return Output.Error(error, token);
                    }
                );
            }
        }

        [CliCommand(Description = "Sets the pairable state of the adapter.")]
        public class SetPairableState : AddressParameter
        {
            [CliOption(Description = "The adapter pairable state to set.")]
            public ToggleState State { get; set; }

            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        ErrorData error = SelectedStack.SetPairableState(State == ToggleState.On);

                        return Output.Error(error, token);
                    }
                );
            }
        }

        [CliCommand(Description = "Sets the discoverable state of the adapter.")]
        public class SetDiscoverableState : AddressParameter
        {
            [CliOption(Description = "The adapter discoverable state to set.")]
            public ToggleState State { get; set; }

            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        ErrorData error = SelectedStack.SetDiscoverableState(
                            State == ToggleState.On
                        );

                        return Output.Error(error, token);
                    }
                );
            }
        }
    }

    [CliCommand(Description = "Perform operations on a Bluetooth device.")]
    public class Device
    {
        public class AddressParameter
        {
            [CliOption(Required = true, Description = "The Bluetooth address of the device.")]
            public string Address { get; set; } = "";
        }

        [CliCommand(Description = "Get information about a Bluetooth device.")]
        public class Properties : AddressParameter
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        (DeviceModel device, ErrorData error) = SelectedStack.GetDevice(
                            token,
                            Address
                        );

                        return Output.Result(device, error, token);
                    }
                );
            }
        }

        [CliCommand(Description = "Pair with a Bluetooth device.")]
        public class Pair : AddressParameter
        {
            [CliOption(
                Description = "The maximum amount of time in seconds that a pairing request can wait for a reply during authentication."
            )]
            public int Timeout { get; set; } = 10;

            public CommandDefinitions RootCommand { get; set; }

            public async Task<int> RunAsync(CliContext context)
            {
                return await OperationManager.OffloadAsync(
                    RootCommand.OperationId,
                    async (token) =>
                    {
                        ErrorData error = await SelectedStack.PairAsync(token, Address, Timeout);

                        return Output.Error(error, token);
                    }
                );
            }

            [CliCommand(Hidden = true, Description = "Cancel pairing with a Bluetooth device.")]
            public class Cancel : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        (token) =>
                        {
                            ErrorData error = SelectedStack.CancelPairing(Address);

                            return Output.Error(error, token);
                        }
                    );
                }
            }
        }

        [CliCommand(
            Description = "Connect to a Bluetooth device.\nIf this command is used without any subcommands and an address parameter is specified,\nit will try to connect to the specified device automatically."
        )]
        public class Connect : AddressParameter
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        ErrorData error = SelectedStack.Connect(token, Address);

                        return Output.Error(error, token);
                    }
                );
            }

            [CliCommand(
                Name = "profile",
                Description = "Connect to a device using a specified profile"
            )]
            public class Profile : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                [CliOption(
                    Name = "--uuid",
                    Required = true,
                    Description = "The Bluetooth service profile as a UUID."
                )]
                public Guid ProfileGuid { get; set; } = Guid.Empty;

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        int (token) =>
                        {
                            ErrorData error = SelectedStack.ConnectProfile(
                                token,
                                Address,
                                ProfileGuid
                            );

                            return Output.Error(error, token);
                        }
                    );
                }
            }
        }

        [CliCommand(Description = "Disconnect from a Bluetooth device.")]
        public class Disconnect : AddressParameter
        {
            public CommandDefinitions RootCommand { get; set; }

            public int Run(CliContext context)
            {
                return OperationManager.Offload(
                    RootCommand.OperationId,
                    int (token) =>
                    {
                        ErrorData error = SelectedStack.DisconnectDevice(Address);

                        return Output.Error(error, token);
                    }
                );
            }
        }

        [CliCommand(Description = "Remove a Bluetooth device.")]
        public class Remove : AddressParameter
        {
            public CommandDefinitions RootCommand { get; set; }

            public async Task<int> RunAsync(CliContext context)
            {
                return await OperationManager.OffloadAsync(
                    RootCommand.OperationId,
                    async (token) =>
                    {
                        ErrorData error = await SelectedStack.RemoveDeviceAsync(Address);

                        return Output.Error(error, token);
                    }
                );
            }
        }

        [CliCommand(Description = "Perform a Object Push Profile based operation.")]
        public class Opp
        {
            [CliCommand(
                Hidden = true,
                Description = "Start an Object Push client session with a device (RPC mode only)."
            )]
            public class StartSession : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            ErrorData error = await SelectedStack.StartFileTransferSessionAsync(
                                token,
                                Address,
                                true
                            );

                            return Output.Error(error, token);
                        }
                    );
                }
            }

            [CliCommand(
                Hidden = true,
                Description = "Send a file to a device with an open session. (RPC mode only)."
            )]
            public class SendFile
            {
                [CliOption(
                    Required = true,
                    HelpName = "file",
                    Description = "A full path of the file to send."
                )]
                public string File { get; set; } = "";
                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        int (token) =>
                        {
                            var (filetransfer, error) = SelectedStack.QueueFileSend(File);

                            return Output.Result(filetransfer, error, token);
                        }
                    );
                }
            }

            [CliCommand(
                Hidden = true,
                Description = "Cancel an Object Push file transfer with a device (RPC mode only)."
            )]
            public class CancelTransfer : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        int (token) =>
                        {
                            var error = SelectedStack.CancelFileTransfer(Address);

                            return Output.Error(error, token);
                        }
                    );
                }
            }

            [CliCommand(
                Hidden = true,
                Description = "Suspend an Object Push file transfer with a device (no-op, RPC mode only)."
            )]
            public class SuspendTransfer
            {
                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        int (token) =>
                        {
                            return Output.Error(Errors.ErrorUnsupported, token);
                        }
                    );
                }
            }

            [CliCommand(
                Hidden = true,
                Description = "Resume an Object Push file transfer with a device (no-op, RPC mode only)."
            )]
            public class ResumeTransfer
            {
                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        int (token) =>
                        {
                            return Output.Error(Errors.ErrorUnsupported, token);
                        }
                    );
                }
            }

            [CliCommand(
                Hidden = true,
                Description = "Stop an Object Push client session with a device (RPC mode only)."
            )]
            public class StopSession
            {
                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        int (token) =>
                        {
                            return Output.Error(SelectedStack.StopFileTransferSession(), token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Start an Object Push server.")]
            public class StartServer
            {
                [CliOption(
                    HelpName = "directory",
                    Description = "A full path to a directory to save incoming file transfers."
                )]
                public string Directory { get; set; } = "";

                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            ErrorData error = await SelectedStack.StartFileTransferServerAsync(
                                token,
                                Directory
                            );

                            return Output.Error(error, token);
                        }
                    );
                }
            }

            [CliCommand(Hidden = true, Description = "Stop an Object Push server (RPC only).")]
            public class StopServer
            {
                [CliOption(
                    HelpName = "directory",
                    Description = "A full path to a directory to save incoming file transfers."
                )]
                public string Directory { get; set; } = "";

                public CommandDefinitions RootCommand { get; set; }

                public int Run(CliContext context)
                {
                    return OperationManager.Offload(
                        RootCommand.OperationId,
                        int (token) =>
                        {
                            return Output.Error(SelectedStack.StopFileTransferServer(), token);
                        }
                    );
                }
            }
        }

        [CliCommand(
            Name = "a2dp",
            Description = "Start an Advanced Audio Distribution Profile session."
        )]
        public class A2dp
        {
            [CliCommand(Description = "Start an A2DP session with a Bluetooth device.")]
            public class StartSession : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            ErrorData error = await SelectedStack.StartAudioSessionAsync(
                                token,
                                Address
                            );

                            return Output.Error(error, token);
                        }
                    );
                }
            }
        }

        [CliCommand(Description = "Perform a Phonebook Profile based operation.")]
        public class Pbap
        {
            [CliCommand(Description = "Get the contacts list from a device.")]
            public class GetAllContacts : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (VcardModel vcard, ErrorData error) =
                                await SelectedStack.GetAllContactsAsync(token, Address);

                            return Output.Result(vcard, error, token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Get the combined call history from a device.")]
            public class GetCombinedCalls : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (VcardModel vcard, ErrorData error) =
                                await SelectedStack.GetCombinedCallHistoryAsync(token, Address);

                            return Output.Result(vcard, error, token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Get the incoming call history from a device.")]
            public class GetIncomingCalls : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (VcardModel vcard, ErrorData error) =
                                await SelectedStack.GetIncomingCallsHistoryAsync(token, Address);

                            return Output.Result(vcard, error, token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Get the outgoing call history from a device.")]
            public class GetOutgoingCalls : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (VcardModel vcard, ErrorData error) =
                                await SelectedStack.GetOutgoingCallsHistoryAsync(token, Address);

                            return Output.Result(vcard, error, token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Get the missed call history from a device.")]
            public class GetMissedCalls : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (VcardModel vcard, ErrorData error) =
                                await SelectedStack.GetMissedCallsAsync(token, Address);

                            return Output.Result(vcard, error, token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Get the speed dial list from a device.")]
            public class GetSpeedDial : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (VcardModel vcard, ErrorData error) =
                                await SelectedStack.GetSpeedDialAsync(token, Address);

                            return Output.Result(vcard, error, token);
                        }
                    );
                }
            }
        }

        [CliCommand(Description = "Perform a Message Access Profile based operation.")]
        public class Map
        {
            [CliCommand(Description = "Get a list of messages from a Bluetooth device.")]
            public class GetMessages : AddressParameter
            {
                [CliOption(
                    HelpName = "folder",
                    Description = "A path to a folder in the remote device to list messages from, for example, 'telecom/msg/inbox'."
                )]
                public string Folder { get; set; } = "telecom";

                [CliOption(
                    HelpName = "index",
                    Description = "The index of the message within a folder listing, from where messages can start being listed."
                )]
                public ushort StartIndex { get; set; }

                [CliOption(
                    HelpName = "count",
                    Description = "The maximum amount of messages to display. Setting bigger values will lead to longer download times."
                )]
                public ushort MaximumCount { get; set; }

                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (MessageListingModel list, ErrorData error) =
                                await SelectedStack.GetMessagesAsync(
                                    token,
                                    Address,
                                    Folder,
                                    StartIndex,
                                    MaximumCount
                                );

                            return Output.Result(list, error, token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Get a list of message folders from a Bluetooth device.")]
            public class GetMessageFolders : AddressParameter
            {
                [CliOption(
                    HelpName = "folder",
                    Description = "A path to a folder in the remote device to list folders from, for example, 'telecom/msg'."
                )]
                public string Folder { get; set; } = "telecom";

                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (GenericResult<List<string>> folders, ErrorData error) =
                                await SelectedStack.GetMessageFoldersAsync(token, Address, Folder);

                            return Output.Result(folders, error, token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Show a message from a Bluetooth device.")]
            public class ShowMessage : AddressParameter
            {
                [CliOption(
                    HelpName = "handle",
                    Required = true,
                    Description = "A message handle of the message to be displayed. Use 'get-messages' to list message handles."
                )]
                public string MessageHandle { get; set; } = "";

                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            (BMessagesModel message, ErrorData error) =
                                await SelectedStack.ShowMessageAsync(token, Address, MessageHandle);

                            return Output.Result(message, error, token);
                        }
                    );
                }
            }

            [CliCommand(Description = "Listen for notification events from a Bluetooth device.")]
            public class StartNotificationServer : AddressParameter
            {
                public CommandDefinitions RootCommand { get; set; }

                public async Task<int> RunAsync(CliContext context)
                {
                    return await OperationManager.OffloadAsync(
                        RootCommand.OperationId,
                        async (token) =>
                        {
                            ErrorData error = await SelectedStack.StartNotificationEventServerAsync(
                                token,
                                Address
                            );

                            return Output.Error(error, token);
                        }
                    );
                }
            }
        }
    }
}
