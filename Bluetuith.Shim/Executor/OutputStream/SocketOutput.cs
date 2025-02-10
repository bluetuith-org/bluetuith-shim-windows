using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Immutable;
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
using Cysharp.Collections;
using DotNext;
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

        if (File.Exists(socketPath))
        {
            File.Delete(socketPath);
        }

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

    internal override void EmitEvent<T>(T ev, OperationToken token)
    {
        SendMarshalledEvent(ev, token);
    }

    internal override void EmitAuthenticationRequest<T>(T authEvent, OperationToken token)
    {
        EmitEvent(authEvent, token);

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
    internal override void SetAuthenticationResponse(long operationId, string response)
    {
        if (authenticationEvents.TryRemove(operationId, out var authEvent))
        {
            authEvent.SetResponse(response);
        }
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

    private void SendMarshalledEvent<T>(T ev, OperationToken token)
        where T : IEvent
    {
        try
        {
            _semaphore.Wait();

            (var operationId, var requestId) = GetIdsFromToken(token);

            var (name, data) = ev.ToJsonNode();

            outputEvent["event_id"] = ev.Event.Value;
            outputEvent["event_action"] = ev.Action.ToString().ToLower();
            outputEvent["event"] = new JsonObject() { [name] = data };

            _socketServer.SendReply(ref outputEvent, token, ev.Event);

            foreach (var (n, _) in outputEvent.ToDictionary())
            {
                outputEvent.Remove(n);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private (long, long) GetIdsFromToken(OperationToken token)
    {
        return token == OperationToken.None ? (0, 0) : (token.OperationId, token.RequestId);
    }
}

internal partial class SocketSession(SocketServer server) : UdsSession(server)
{
    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            var array = new NativeMemoryArray<byte>(size);
            array.CreateBufferWriter().Write(buffer[(int)offset..(int)size].AsSpan());

            Task.Run(async () => await CommandProcessor.ProcessRequest(SendReplyToSocket, array));
        }
        catch (Exception ex)
        {
            Output.Event(
                SocketErrors.ErrorJsonRequestParse.AddMetadata("exception", ex.Message),
                OperationToken.None
            );
        }
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Socket error: Could not send reply: {error}");
    }

    private void SendReplyToSocket((OperationToken token, ErrorData error) processedResult)
    {
        server.QueueWaitReply(
            processedResult.token,
            Id,
            () =>
            {
                if (processedResult.error != Errors.ErrorNone)
                    Output.Error(processedResult.error, processedResult.token);

                return processedResult.error == Errors.ErrorNone;
            }
        );
    }
}

internal partial class SocketServer : UdsServer
{
    private readonly CancellationTokenSource _onConnect;
    private readonly ConcurrentDictionary<long, Guid> replyQueue = new();

    internal SocketServer(string path, CancellationTokenSource waitForResume)
        : base(path)
    {
        _onConnect = waitForResume;
    }

    internal void QueueWaitReply(OperationToken token, Guid sessionId, Func<bool> canQueueOperation)
    {
        if (!IsStarted)
        {
            return;
        }

        if (replyQueue.TryAdd(token.OperationId, sessionId))
        {
            if (!canQueueOperation())
            {
                replyQueue.Remove(token.OperationId, out _);
                return;
            }
        }
    }

    internal void SendReply(ref JsonObject reply, OperationToken token, EventType eventType)
    {
        if (!IsStarted)
        {
            return;
        }

        try
        {
            // Events are multicast to all listeners.
            if (eventType != EventTypes.EventNone)
            {
                Multicast(PackMessage(ref reply));
                return;
            }

            // Results are sent individually to the session that requested it.
            if (GetSession(token, out var session))
            {
                session.Send(PackMessage(ref reply));
                replyQueue.Remove(token.OperationId, out _);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Socket error: Could not send reply: {ex.Message}");
        }
    }

    private bool GetSession(OperationToken token, out UdsSession session)
    {
        session = null;

        if (replyQueue.TryGetValue(token.OperationId, out var sessionId))
        {
            session = FindSession(sessionId);
        }

        return session != null;
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

internal static class CommandProcessor
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

    internal static async Task ProcessRequest(
        Action<(OperationToken token, ErrorData error)> sendAction,
        NativeMemoryArray<byte> buffer
    )
    {
        try
        {
            await foreach (
                var request in JsonSerializer.DeserializeAsyncEnumerable<Request>(
                    buffer.AsStream(),
                    true,
                    _options
                )
            )
            {
                sendAction(ProcessCommand(request));
            }
        }
        catch (Exception ex)
        {
            Output.Event(
                SocketErrors.ErrorJsonRequestParse.AddMetadata("exception", ex.Message),
                OperationToken.None
            );
        }
        finally
        {
            buffer.Dispose();
        }
    }

    private static (OperationToken, ErrorData) ProcessCommand(Request request)
    {
        var processedResult = (token: OperationToken.None, error: Errors.ErrorNone);

        try
        {
            _semaphore.Wait();

            processedResult.token = OperationManager.GenerateToken(request.RequestId);

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
