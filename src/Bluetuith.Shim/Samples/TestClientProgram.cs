using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bluetuith.Shim.Executor;
using Bluetuith.Shim.Executor.OutputHandler;

namespace Bluetuith.Shim;

internal static class TestClientProgram
{
    // A test program to demonstrate a simple request-reply between client-server.
    public static void MainProgram(string[] args)
    {
        // Initialize the socket server.
        // This code block directly calls the StartSocketServer function,
        // but the client will actually initialize it like this (in a separate task):
        // (pseudocode):
        // var process = new Process("bluetuith-shim", "rpc", "start-session", "--socket-path", "<path-to-socket>");
        // process.Start();
        var path = Path.Join(Path.GetTempPath(), "socket.sock");
        File.Delete(path);
        Task.Run(() =>
        {
            Output.StartSocketServer(path, Operation.GenerateToken(0));
        });

        // Here we start with the client code, once the process has started.
        // Create a new JSON object (or struct) with two mandatory keys: "command" and "requestId".
        // - The "command" key is an array, which contains the subcommands and parameters to be executed.
        // - The "request" key is an integer, for the client to keep track of the request.
        // The following code block looks like this in serialized JSON:
        // { "command": ["device", "info", "-a", "a0:46:5a:52:93:23", "--show-services"], "requestId": 1 }
        var json = new JsonObject()
        {
            ["command"] = JsonSerializer.SerializeToNode(
                new List<string>()
                {
                    "device",
                    "info",
                    "-a",
                    "a0:46:5a:52:93:23",
                    "--show-services",
                }
            ),
            ["requestId"] = 1,
        };

        // Connect to the socket using the socket path specified above.
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var ep = new UnixDomainSocketEndPoint(path);
        socket.Connect(ep);

        // Send the JSON payload after serializing it to UTF8 bytes.
        socket.Send(JsonSerializer.SerializeToUtf8Bytes(json));

        // Initialize a memory stream to read the response from the server.
        var ms = new MemoryStream();
        // This buffer size is small, ideally it should be much bigger, but it is set to 128 bytes
        // to demonstrate merging multiple message parts into a single JSON reply playload.
        const int MAX_BUFFER_SIZE = 128;

        while (socket.Connected)
        {
            // Get the response from the server as bytes.
            // The reply byte array has the following components:
            // - An Int32 length header, as a 4-byte Big Endian byte array.
            // - The JSON payload, as a UTF8 encoded byte array.
            var buffer = new byte[MAX_BUFFER_SIZE];
            var bytesReadFromSocket = socket.Receive(buffer);

            // From the buffer, first parse the length header.
            // Convert the first 4 bytes of the array from Big Endian to Int32.
            Span<byte> lengthArrayBE = buffer.AsSpan().Slice(0, 4);
            var length = BinaryPrimitives.ReadInt32BigEndian(lengthArrayBE);

            // Seek to the 4th position in the buffer byte array.
            // Write the first chunk of data to the memory stream.
            ms.Write(buffer.AsSpan(4));

            // Compare the current buffer length to the actual payload length.
            // Read from the socket until both the buffer length and the payload length match.
            // Note that subsequent payload chunks do not have the length header attached to them.
            while (bytesReadFromSocket < length)
            {
                buffer = new byte[MAX_BUFFER_SIZE];
                bytesReadFromSocket += socket.Receive(buffer);
                ms.Write(buffer);
            }

            // Decode the final JSON string from the memory stream.
            var serializedJSON = Encoding.UTF8.GetString(ms.ToArray());
            // Print the JSON string, or begin deserialize and parsing.
            Console.WriteLine(serializedJSON);

            // Clear the memory stream.
            ms.SetLength(0);
        }
    }
}
