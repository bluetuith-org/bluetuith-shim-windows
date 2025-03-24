# bluetuith-shim-windows
A shim and command-line tool to use Bluetooth Classic features on Windows.<br />
Part of the cross-platform work for the [bluetuith](https://github.com/darkhz/bluetuith) project.

This is alpha-grade software.

## Funding

This project is funded through [NGI Zero Core](https://nlnet.nl/core), a fund established by [NLnet](https://nlnet.nl) with financial support from the European Commission's [Next Generation Internet](https://ngi.eu) program. Learn more at the [NLnet project page](https://nlnet.nl/project/bluetuith).

[<img src="https://nlnet.nl/logo/banner.png" alt="NLnet foundation logo" width="20%" />](https://nlnet.nl)
[<img src="https://nlnet.nl/image/logos/NGI0_tag.svg" alt="NGI Zero Logo" width="20%" />](https://nlnet.nl/core)

# Requirements
- Windows 10 19041 or later.
- [.NET Runtime 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
- Administrator access for certain Registry-related APIs.

# Download and Installation
All downloads can be found within the 'Releases' page of the repository.

- Download the binary that matches your CPU architecture to a known path.
- Open CMD or Powershell, and run the binary.

# Documentation
This documentation is mostly a quick-start guide. Detailed documentation will come later.

Interaction with the shim can be done via:
- The console (command-line mode)
- A socket (rpc mode)

## Command-line
As with most command-line tools,
- `--help` or `-h` will show the command's help page.
- `--version` or `-v` will show the version information.

The 'help' option can be used with subcommands as well, for example:
```
bluetuith-shim adapter --help
```

### Adapter commands
- To switch on/off the adapter:
```
bluetuith-shim adapter set-power-state <on|off>
```

- To get information about the Bluetooth adapter:
```
bluetuith-shim adapter properties
```

- To scan for Bluetooth devices:
```
bluetuith-shim adapter discovery start
```
(Press Ctrl-C to stop discovery)

- To get the currently paired devices:
```
bluetuith-shim adapter get-paired-devices
```

### Device commands
Most subcommands of the 'device' command have a mandatory `--address` or `-a` parameter to specify the Bluetooth address of the device.<br />

- To pair a device:
```
bluetuith-shim device pair -a <address>
```
While pairing, a prompt may appear (to confirm whether PINs match between host and remote device, for example).<br />
Press "y" or "n" to accept or decline pairing with the remote device.<br />
Note that a default timeout of 10 seconds is set to wait for a reply from the user.<br />

- Similarly, to unpair a device:
```
bluetuith-shim device remove -a <address>
```

- To view information about a device:
```
bluetuith-shim device properties -a <address>
```

Once a device has been paired, a connection can be made to the device.<br />
To connect to a Bluetooth device, a Bluetooth profile must be specified.<br />
Currently, within the shim, each connectable profile appears as a subcommand of the `connect` command.<br />

To view all connectable/supported profiles:
```
bluetuith-shim device connect --help
```

To automatically connect to a device:
```
bluetuith-shim device connect -a <address>
```

**Some connection examples:**
- To start an A2DP session with a device:
```
bluetuith-shim device connect a2dp -a <address>
```
(Note that the application has to remain open to maintain the session. Closing the app will close the session.)

- To start an Object Push Profile session with a device:
	- To send file(s): `bluetuith-shim device connect opp send-files -a <address> -f <path-to-file> -f <another-file> -f ...`
	- To listen for and receive files: `bluetuith-shim device connect opp start-server -d <directory-path-to-save-transferred-files>`
	(Press Ctrl-C to stop the server)

- To start a Phonebook Access Profile session with a device:
	- To get all contacts: `bluetuith-shim device connect pbap get-all-contacts -a <address>`
	- To get combined call history: `bluetuith-shim device connect pbap get-combined-calls -a <address>`

More subcommands for the `pbap` command can be listed using: `bluetuith-shim device connect pbap --help`.<br />
Similarly, more subcommands for each profile can be listed using: `bluetuith-shim device connect <profile-name> --help`.<br />

- To disconnect a connected device:
```
bluetuith-shim device disconnect -a <address>
```

## RPC
A Unix Domain socket is required, currently, to perform RPC with the shim.<br />
Future versions may support named pipes as well.

JSON is the primary data format used to exchange information to and from the shim.<br />

Note: Pbap and Map profiles aren't ready for use via RPC yet.

### Start/Stop the server
To start the server over a socket:
```
bluetuith-shim server start
```
A socket will be automatically created at the user's `$env:Temp` (Temporary files) directory with the name 'bh-svc.sock'
A popup should be displayed as well, once the service starts, and a system tray icon will be created.

To stop the server:
```
bluetuith-shim server stop
```

Once the server has started, commands can be sent to the shim via the socket.

### Request format
To send a request to execute a command to the shim, the message should look like:
```
{ "command": ["device", "info", "-a", "AA:BB:CC:DD:EE:FF"], "requestId": 1 }
```

Where:
- The `command` property must contain an string array of commands and associated subcommands/options.
- The `requestId` property must contain a non-zero positive integer.

The message must be serialized to UTF-8 bytes before sending it to the shim.

To indicate that the command was parsed and is being executed.<br />
Note that all calls are asynchronous, so use the `requestId` or `operationId` to keep track of requests and replies.

### Reply format
Once an operation has finished, the shim will encode the command's result to JSON and send it to the client.

The reply payload consists of two parts:
- A Big-Endian encoded 18-byte header which contains:
	- A 1 byte API version
	- A 1 byte optional flags
	- An 8 byte Request ID (as provided by the client)
	- A 4 byte Operation ID
	- A 4 byte JSON payload size
- A UTF-8 encoded JSON payload, in bytes.

Which should look like this:
```
[<18-byte header>]{JSON payload}
```

If the command outputs any events, the JSON payload will look like:
```
{ "eventId": 0, "event": { "file_transfer": { "filename": "customFile.mp4", "size": 100 ...} } }
```

If the command does not return any error, the JSON payload will look like:
```
{ "status": "ok", "data": { "device": { "name": "Bluetooth Device", "address": "AA:BB:CC:DD:EE:FF" ...} } }
```

If the commands returns an error, the JSON payload will look like:
```
{ "status": "error", "error": { "code": 10, "name": "ERROR_DEVICE_NOT_FOUND", "description": "" ...} }
```

A sample program to parse a reply payload is provided in the 'Samples' directory.

### Authentication
This section mainly applies to the `pairing` operation.

During a pairing operation, if the device asks to perform additional authentication (like confirming if PINs match),<br />
an authentication event is emitted.

An authentication event usually is in the following format:
```
{ "action": "added", "event": { "pairing_auth_event": { "pin": 0000, "timeout": 10000, "authenticationType": "confirm-yes-no" } } }
```
Note the operation ID of the event within the header.

There are two authentication request types: `ConfirmYesNo` and `ConfirmInput`.
- The reply to a `ConfirmYesNo` request should be one of `y|yes|n|no`.
- The reply to a `ConfirmInput` request should be the same PIN text as provided in the event.

To reply to the `ConfirmYesNo` authentication request for example, the following JSON payload should be sent:
```
{ "command": ["rpc", "auth", "--operation-id", 1, "--response", "yes"], "requestId": 1 }
```

### Version querying
While in RPC mode, do not use the `-v` switch to query the version. Instead use `rpc version` to get a version number
serialized to JSON, that can be parsed.

# Credits
These repositories were invaluable during the development of the shim.<br />
Please star these repositories individually, and possibly submit contributions to them.<br />

- For very extensive Bluetooth related functionality, wrappers and documentation: [32feet](https://github.com/inthehand/32feet).<br />
(A clone of the '32feet' library is [here](https://github.com/bluetuith-org/32feet))

- For very good PBAP and MAP support: [MyPhone](https://github.com/BestOwl/MyPhone).<br />
(A clone of the 'MyPhone.OBEX' library is [here](https://github.com/bluetuith-org/MyPhone.Obex), with added basic OPP support)

- For native adapter related functionalities: [Nefarius' Bluetooth Utilities](https://github.com/nefarius/Nefarius.Utilities.Bluetooth).
