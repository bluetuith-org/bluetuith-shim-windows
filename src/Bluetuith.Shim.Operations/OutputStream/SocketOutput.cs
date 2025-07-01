using System.Buffers;
using System.Collections.Concurrent;
using System.CommandLine;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.DataTypes;
using DotNext.IO;
using DotNext.Threading;
using NetCoreServer;

namespace Bluetuith.Shim.Operations;

public sealed class SocketOutput : OutputBase
{
    private readonly string _socketPath;
    private readonly SocketServer _socketServer;
    private readonly OperationToken _operationToken;

    private readonly ConcurrentDictionary<long, AuthenticationEvent> authenticationEvents = new();

    public SocketOutput(
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

    public override int EmitResult<T>(T result, OperationToken token)
    {
        SendMarshalledResult(result, token);

        return base.EmitResult(result, token);
    }

    public override int EmitError(ErrorData error, OperationToken token)
    {
        SendMarshalledError(error, token);

        return base.EmitError(error, token);
    }

    public override void EmitEvent<T>(T ev, OperationToken token, bool clientOnly = false)
    {
        SendMarshalledEvent(ev, token, clientOnly);
    }

    public override void EmitAuthenticationRequest<T>(T authEvent, OperationToken token)
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
    public override bool SetAuthenticationResponse(long operationId, string response)
    {
        if (authenticationEvents.TryRemove(operationId, out var authEvent))
        {
            authEvent.SetResponse(response);
            return true;
        }

        return false;
    }

#nullable disable

    public override void WaitForClose()
    {
        Console.WriteLine(
            $"[+] Server started at '{Path.GetFileName(_socketPath)}' ({_socketServer.Id})"
        );

        _operationToken.Wait();
        _socketServer.Stop();

        Console.WriteLine("[+] Server stopped");
    }

    private bool SendMarshalledResult<T>(T reply, OperationToken token, bool hasData = true)
        where T : IResult
    {
        return _socketServer.SendReply(
            token,
            true,
            (token, writer) =>
            {
                reply.WriteResult(token, false, hasData, writer);
            }
        );
    }

    private bool SendMarshalledError(ErrorData error, OperationToken token)
    {
        if (error == Errors.ErrorNone)
            return SendMarshalledResult(error, token, false);

        return _socketServer.SendReply(
            token,
            true,
            (token, writer) =>
            {
                error.WriteResult(token, true, true, writer);
            }
        );
    }

    private bool SendMarshalledEvent<T>(T ev, OperationToken token, bool clientOnly)
        where T : IEvent
    {
        return _socketServer.SendReply(
            token,
            clientOnly,
            (token, writer) =>
            {
                ev.WriteEvent(writer);
            }
        );
    }
}

public partial class SocketSession(SocketServer server) : UdsSession(server)
{
    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            RequestProcessor.ProcessRequest(buffer[(int)offset..(int)size], this.Id);
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

    public static class RequestProcessor
    {
        public record struct Request
        {
            [JsonPropertyName("command")]
            public List<string> Command { get; set; }

            [JsonPropertyName("request_id")]
            public long RequestId { get; set; }
        }

        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        public static void ProcessRequest(byte[] buffer, Guid clientId)
        {
            foreach (
                var request in JsonSerializer
                    .DeserializeAsyncEnumerable(
                        new ReadOnlySequence<byte>(buffer).AsStream(),
                        RequestSerializable.Default.Request,
                        true
                    )
                    .ToBlockingEnumerable()
            )
            {
                _ = Task.Run(() =>
                {
                    var (token, error) = ProcessCommand(request, clientId);
                    if (error != Errors.ErrorNone)
                        Output.Error(error, token);
                });
            }
        }

        private static (OperationToken token, ErrorData error) ProcessCommand(
            Request request,
            Guid clientId
        )
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
                    throw new ArgumentException(
                        $"Command parse error: {parsed.Errors[0].Message}."
                    );
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
}

public partial class SocketServer : UdsServer
{
    private readonly CancellationTokenSource _onConnect;
    protected static readonly ConcurrentDictionary<Guid, SessionStream> sessionStreams = new();

    public SocketServer(string path, CancellationTokenSource waitForResume)
        : base(path)
    {
        _onConnect = waitForResume;
    }

    public bool SendReply(
        OperationToken token,
        bool clientOnlyData,
        Action<OperationToken, Utf8JsonWriter> sendAction
    )
    {
        if (!IsStarted)
            return false;

        int unsent = 0;
        List<Exception> socketExceptions = null;

        try
        {
            bool foundClient = false;

            foreach (var (_, session) in sessionStreams)
            {
                if (session == null || session.IsDisposed)
                    continue;

                if (clientOnlyData)
                {
                    if (session.Id == token.ClientId)
                        foundClient = true;
                    else
                        continue;
                }

                using (session.AcquireWriteLock())
                {
                    try
                    {
                        sendAction(token, session.Writer);
                        session.Flush();
                    }
                    catch (Exception ex)
                    {
                        socketExceptions ??= [];
                        socketExceptions.Add(ex);

                        if (ex is NullReferenceException || !session.Reset())
                            unsent++;
                    }
                }

                if (foundClient)
                    break;
            }

            if (socketExceptions != null)
                throw new AggregateException(socketExceptions);

            return unsent == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Socket error: Could not send reply: {ex.Message}");
            return false;
        }
    }

    protected override UdsSession CreateSession()
    {
        return new SocketSession(this);
    }

    protected override void OnConnecting(UdsSession session)
    {
        _onConnect.Cancel();
        sessionStreams.TryAdd(session.Id, new SessionStream(session));
    }

    protected override void OnDisconnecting(UdsSession session)
    {
        if (sessionStreams.TryRemove(session.Id, out var sessionStream))
            sessionStream.Dispose();
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Socket error: {error}");
    }

    protected class SessionStream
    {
        private UdsSession _session;
        private NetworkStream _stream;
        private Utf8JsonWriter _writer;

        private static readonly byte[] NewLine = Encoding.UTF8.GetBytes("\n");
        private static readonly JsonWriterOptions _options = new() { SkipValidation = true };

        public Utf8JsonWriter Writer
        {
            get => _writer;
        }
        public bool IsDisposed
        {
            get => _session.IsDisposed || _session.IsSocketDisposed;
        }
        public Guid Id
        {
            get => _session.Id;
        }

        private void Initialize(UdsSession session)
        {
            _session = session;
            _stream = new NetworkStream(_session.Socket, FileAccess.Write, false);
            _writer = new Utf8JsonWriter(_stream, _options);
        }

        public SessionStream(UdsSession session)
        {
            Initialize(session);
        }

        public bool Reset()
        {
            if (_session == null || IsDisposed)
                return false;

            try
            {
                _stream?.Dispose();
                _writer?.Dispose();

                Initialize(_session);
            }
            catch
            {
                sessionStreams.TryRemove(Id, out var sessionStream);
                return false;
            }

            return true;
        }

        public void Flush()
        {
            _writer.Flush();
            _stream.Write(NewLine, 0, 1);

            _writer.Reset();
        }

        public void Dispose()
        {
            try
            {
                _writer.Dispose();
                _stream.Dispose();
            }
            catch { }
        }
    }
}

[JsonSerializable(typeof(SocketSession.RequestProcessor.Request))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    DefaultBufferSize = 8192
)]
public partial class RequestSerializable : JsonSerializerContext { }

public class SocketErrors : Errors
{
    public static ErrorData ErrorJsonRequestParse = new(
        Code: new ErrorCode("ERROR_JSON_REQUEST_PARSE", -2500),
        Description: "An error occurred while parsing the JSON request"
    );
}
