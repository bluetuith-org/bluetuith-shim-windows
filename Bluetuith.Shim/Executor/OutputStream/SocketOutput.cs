using System.Buffers;
using System.Collections.Concurrent;
using System.CommandLine;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Executor.Command;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Types;
using DotNext;
using DotNext.IO;
using NetCoreServer;

namespace Bluetuith.Shim.Executor.OutputStream;

internal sealed class SocketOutput : OutputBase
{
    private readonly string _socketPath;
    private readonly SocketServer _socketServer;
    private readonly OperationToken _operationToken;

    private readonly ConcurrentDictionary<long, AuthenticationEvent> authenticationEvents = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private JsonObject output = [];
    private JsonObject outputEvent = [];

    internal SocketOutput(
        string socketPath,
        CancellationTokenSource waitForResume,
        OperationToken token
    )
    {
        if (_socketServer != null && _socketServer.IsStarted)
        {
            throw new ArgumentException("The socket server has already started");
        }

        var socketDir =
            Path.GetDirectoryName(socketPath)
            ?? throw new InvalidDataException("Cannot find socket directory");

        if (!Path.Exists(socketDir))
            Directory.CreateDirectory(socketDir);

        if (File.Exists(socketPath))
            File.Delete(socketPath);

        _socketServer = new SocketServer(socketPath, waitForResume);
        _socketServer.Start();

        _socketPath = socketPath;
        _operationToken = token;
    }

    internal override int EmitResult<T>(T result, OperationToken token)
    {
        SendMarshalledResult(result, token);

        return base.EmitResult(result, token);
    }

    internal override int EmitError(ErrorData error, OperationToken token)
    {
        SendMarshalledError(error, token);

        return base.EmitError(error, token);
    }

    internal override void EmitEvent<T>(T ev, OperationToken token, bool clientOnly = false)
    {
        SendMarshalledEvent(ev, token, clientOnly);
    }

    internal override void EmitAuthenticationRequest<T>(T authEvent, OperationToken token)
    {
        if (!SendMarshalledEvent(authEvent, token, true))
        {
            authEvent.SetResponse();
            return;
        }

        if (authenticationEvents.TryAdd(token.OperationId, authEvent))
        {
            token.Wait();
            authenticationEvents.TryRemove(token.OperationId, out _);
        }
        else
        {
            authEvent.SetResponse();
        }
    }

#nullable enable
    internal override bool SetAuthenticationResponse(long operationId, string response)
    {
        if (authenticationEvents.TryRemove(operationId, out var authEvent))
        {
            authEvent.SetResponse(response);
            return true;
        }

        return false;
    }

#nullable disable

    internal override void WaitForClose()
    {
        Console.WriteLine(
            $"[+] Server started at '{Path.GetFileName(_socketPath)}' ({_socketServer.Id})"
        );

        _operationToken.Wait();
        _socketServer.Stop();

        Console.WriteLine("[+] Server stopped");
    }

    private void SendMarshalledResult<T>(T reply, OperationToken token)
        where T : IResult
    {
        try
        {
            _semaphore.Wait();

            (var operationId, var requestId) = GetIdsFromToken(token);

            output["status"] = "ok";
            output["operation_id"] = token.OperationId;
            output["request_id"] = token.RequestId;

            var (name, data) = reply.ToJsonNode();
            output["data"] = new JsonObject() { [name] = data };

            _socketServer.SendReply(ref output, token, EventTypes.EventNone);

            foreach (var (n, _) in output.ToDictionary())
            {
                output.Remove(n);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void SendMarshalledError(ErrorData error, OperationToken token)
    {
        if (error == Errors.ErrorNone)
        {
            SendMarshalledResult(error, token);
            return;
        }

        try
        {
            _semaphore.Wait();

            (var operationId, var requestId) = GetIdsFromToken(token);

            var status = "error";

            var (err, data) = error.ToJsonNode();
            output["status"] = status;
            output["operation_id"] = token.OperationId;
            output["request_id"] = token.RequestId;
            output[err] = data;

            _socketServer.SendReply(ref output, token, EventTypes.EventNone);

            foreach (var (n, _) in output.ToDictionary())
            {
                output.Remove(n);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private bool SendMarshalledEvent<T>(T ev, OperationToken token, bool clientOnly)
        where T : IEvent
    {
        var sent = false;

        try
        {
            _semaphore.Wait();

            (var operationId, var requestId) = GetIdsFromToken(token);

            var (name, data) = ev.ToJsonNode();

            outputEvent["event_id"] = ev.Event.Value;
            outputEvent["event_action"] = ev.Action.ToString().ToLower();
            outputEvent["event"] = new JsonObject() { [name] = data };

            sent = _socketServer.SendReply(ref outputEvent, token, ev.Event, clientOnly);

            foreach (var (n, _) in outputEvent.ToDictionary())
            {
                outputEvent.Remove(n);
            }
        }
        finally
        {
            _semaphore.Release();
        }

        return sent;
    }

    private (long, long) GetIdsFromToken(OperationToken token)
    {
        return token == OperationToken.None ? (0, 0) : (token.OperationId, token.RequestId);
    }
}

internal partial class SocketSession(SocketServer server) : UdsSession(server)
{
    private static readonly JsonSerializerOptions _options = new() { DefaultBufferSize = 8192 };

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            RequestProcessor.ProcessRequest(
                buffer[(int)offset..(int)size],
                this.Id,
                SendReplyToSocket
            );
        }
        catch (Exception ex)
        {
            Output.Event(
                SocketErrors.ErrorJsonRequestParse.AddMetadata("exception", ex.Message),
                OperationToken.None
            );
        }
    }

    protected override void OnDisconnected()
    {
        OperationManager.CancelClientOperations(this.Id);
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Socket error: Could not send reply: {error}");
    }

    private void SendReplyToSocket((OperationToken token, ErrorData error) processedResult)
    {
        if (processedResult.error != Errors.ErrorNone)
            Output.Error(processedResult.error, processedResult.token);
    }
}

internal partial class SocketServer : UdsServer
{
    private readonly CancellationTokenSource _onConnect;

    internal SocketServer(string path, CancellationTokenSource waitForResume)
        : base(path)
    {
        _onConnect = waitForResume;
    }

    internal bool SendReply(
        ref JsonObject reply,
        OperationToken token,
        EventType eventType,
        bool clientOnlyEvent = false
    )
    {
        if (!IsStarted)
        {
            return false;
        }

        try
        {
            // Events are multicast to all listeners.
            if (eventType != EventTypes.EventNone && !clientOnlyEvent)
            {
                Multicast(PackMessage(ref reply));
                return true;
            }

            // Results and client-specific events are sent individually to the session that requested it.
            var session = FindSession(token.ClientId);
            var sentBytes = session?.Send(PackMessage(ref reply));

            return session != null && sentBytes > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Socket error: Could not send reply: {ex.Message}");
            return false;
        }
    }

    private static ReadOnlySpan<byte> PackMessage(ref JsonObject reply)
    {
        ReadOnlySpan<byte> serializedReply = JsonSerializer.SerializeToUtf8Bytes(reply);

        return serializedReply.Concat(new Span<byte>(Encoding.UTF8.GetBytes("\n"))).Span;
    }

    protected override UdsSession CreateSession()
    {
        return new SocketSession(this);
    }

    protected override void OnConnecting(UdsSession _)
    {
        _onConnect.Cancel();
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Socket error: {error}");
    }
}

internal static class RequestProcessor
{
    private record struct Request
    {
        [JsonPropertyName("command")]
        public List<string> Command { get; set; }

        [JsonPropertyName("request_id")]
        public long RequestId { get; set; }
    }

    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly JsonSerializerOptions _options = new() { DefaultBufferSize = 8192 };

    internal static void ProcessRequest(
        byte[] buffer,
        Guid clientId,
        Action<(OperationToken, ErrorData)> sendAction
    )
    {
        foreach (
            var request in JsonSerializer
                .DeserializeAsyncEnumerable<Request>(
                    new ReadOnlySequence<byte>(buffer).AsStream(),
                    true,
                    _options
                )
                .ToBlockingEnumerable()
        )
        {
            _ = Task.Run(() => sendAction(ProcessCommand(request, clientId)));
        }
    }

    private static (OperationToken, ErrorData) ProcessCommand(Request request, Guid clientId)
    {
        var processedResult = (token: OperationToken.None, error: Errors.ErrorNone);

        try
        {
            _semaphore.Wait();

            processedResult.token = OperationManager.GenerateToken(request.RequestId, clientId);

            ParseResult parsed = CommandParser.Parse(
                processedResult.token,
                [.. request.Command],
                true
            );
            if (parsed.Errors.Count > 0)
            {
                throw new ArgumentException($"Command parse error: {parsed.Errors[0].Message}.");
            }

            OperationManager
                .ExecuteHandlerAsync(processedResult.token, parsed)
                .GetAwaiter()
                .GetResult();

            return processedResult;
        }
        catch (Exception ex)
        {
            processedResult.error = SocketErrors.ErrorJsonRequestParse.AddMetadata(
                "exception",
                ex.Message
            );

            return processedResult;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

internal class SocketErrors : Errors
{
    internal static ErrorData ErrorJsonRequestParse = new(
        Code: new ErrorCode("ERROR_JSON_REQUEST_PARSE", -2500),
        Description: "An error occurred while parsing the JSON request",
        Metadata: new() { { "exception", "" } }
    );
}
