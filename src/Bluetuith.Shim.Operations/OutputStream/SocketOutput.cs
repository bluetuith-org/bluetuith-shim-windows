using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.DataTypes;
using DotNext.Collections.Generic;
using DotNext.IO;
using DotNext.Threading;
using NetCoreServer;
using static Bluetuith.Shim.Operations.AuthenticationManager;

namespace Bluetuith.Shim.Operations;

internal sealed class SocketOutput : OutputBase
{
    private readonly string _socketPath;
    private readonly SocketServer _socketServer;
    private readonly OperationToken _operationToken;

    internal SocketOutput(string socketPath, OperationToken token)
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

        _socketServer = new SocketServer(socketPath);
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

    internal override void EmitAuthenticationRequest<T>(
        T authEvent,
        OperationToken token,
        AuthAgentType authAgentType = AuthAgentType.None
    )
    {
        if (AuthenticationManager.AddEvent(token, authEvent, authAgentType))
            if (!SendMarshalledEvent(authEvent, token, true))
                SetAuthenticationResponse(token, authEvent.CurrentAuthId, "");
    }

#nullable enable
    internal override bool SetAuthenticationResponse(
        OperationToken token,
        long authId,
        string response
    )
    {
        return AuthenticationManager.SetEventResponse(token, authId, response);
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

    private bool SendMarshalledResult<T>(T reply, OperationToken token, bool hasData = true)
        where T : IResult
    {
        return _socketServer.SendReply(
            token,
            true,
            new ResultMarshaller<IResult>(reply, hasData, false)
        );
    }

    private bool SendMarshalledError(ErrorData error, OperationToken token)
    {
        if (error == Errors.ErrorNone)
            return SendMarshalledResult(error, token, false);

        return _socketServer.SendReply(
            token,
            true,
            new ResultMarshaller<IResult>(error, true, true)
        );
    }

    private bool SendMarshalledEvent<T>(T ev, OperationToken token, bool clientOnly)
        where T : IEvent
    {
        return _socketServer.SendReply(token, clientOnly, new EventMarshaller<IEvent>(ev));
    }
}

internal partial class SocketSession(SocketServer server) : UdsSession(server)
{
    record class ParseRequest
    {
        public Request ClientRequest;
        public OperationToken Token;
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try
        {
            foreach (
                var request in JsonSerializer
                    .DeserializeAsyncEnumerable(
                        new ReadOnlySequence<byte>(buffer[(int)offset..(int)size]).AsStream(),
                        RequestSerializable.Default.Request,
                        true
                    )
                    .ToBlockingEnumerable()
            )
            {
                var token = OperationManager.GenerateToken(request.RequestId, Id);

                Task.Factory.StartNew(
                    Execute,
                    new ParseRequest() { ClientRequest = request, Token = token }
                );
            }
        }
        catch (Exception ex)
        {
            Output.Event(
                SocketErrors.ErrorJsonRequestParse.AddMetadata("exception", ex.Message),
                OperationToken.None
            );
        }
    }

    private static void Execute(object obj)
    {
        var result = obj as ParseRequest;
        OperationManager.ExecuteHandler(result.Token, result.ClientRequest.Command);
    }

    protected override void OnDisconnected()
    {
        OperationManager.CancelClientOperations(this.Id);
        AuthenticationManager.RemoveAgentsAndEvents(this.Id);
    }

    protected override void OnError(SocketError error)
    {
        Console.WriteLine($"Socket error: Could not send reply: {error}");
    }
}

internal partial class SocketServer : UdsServer
{
    private static readonly ConcurrentDictionary<Guid, SessionStream> sessionStreams = new();
    private static readonly Func<IEnumerable<SessionStream>> values = (
        sessionStreams as IDictionary<Guid, SessionStream>
    ).ValuesGetter();

    internal SocketServer(string path)
        : base(path) { }

    internal bool SendReply<T>(OperationToken token, bool clientOnlyData, T marshaller)
        where T : IMarshaller, allows ref struct
    {
        if (!IsStarted)
            return false;

        int unsent = 0;
        List<Exception> socketExceptions = null;

        try
        {
            bool foundClient = false;

            foreach (var session in values())
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
                        marshaller.Write(token, session.Writer);
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

    private class SessionStream
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
                _writer?.Dispose();
                _stream?.Dispose();

                Initialize(_session);
            }
            catch
            {
                sessionStreams.TryRemove(Id, out _);
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

internal record struct Request
{
    [JsonPropertyName("command")]
    public string[] Command { get; set; }

    [JsonPropertyName("request_id")]
    public long RequestId { get; set; }
}

[JsonSerializable(typeof(Request))]
[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    DefaultBufferSize = 8192
)]
internal partial class RequestSerializable : JsonSerializerContext { }

internal class SocketErrors : Errors
{
    internal static ErrorData ErrorJsonRequestParse = new(
        Code: new ErrorCode("ERROR_JSON_REQUEST_PARSE", -2500),
        Description: "An error occurred while parsing the JSON request"
    );
}
