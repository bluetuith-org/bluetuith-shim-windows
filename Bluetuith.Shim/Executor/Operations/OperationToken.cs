using System.Collections.Concurrent;

namespace Bluetuith.Shim.Executor.Operations;

public readonly struct OperationToken
{
    private readonly Lazy<OperationTokenRef> _reftoken;

    public long OperationId { get; }
    public long RequestId { get; }

    public CancellationTokenSource CancelTokenSource
    {
        get => _reftoken.Value.CancelTokenSource;
    }

    public CancellationTokenSource LinkedCancelTokenSource
    {
        get => _reftoken.Value.LinkedCancelTokenSource;
    }

    public Guid ClientId
    {
        get => _reftoken.Value.ClientId;
    }
    public bool HasClientId
    {
        get => _reftoken.Value.HasClientId;
    }

    public static OperationToken None
    {
        get => default;
    }

    public OperationToken(long operationId, long requestId, bool empty = false)
    {
        OperationId = operationId;
        RequestId = requestId;
        _reftoken = !empty ? new(() => new()) : null;
    }

    public OperationToken(long operationId, long requestId, CancellationToken cancellationToken)
        : this(operationId, requestId)
    {
        _reftoken = new(() => new(cancellationToken));
    }

    public OperationToken(
        long operationId,
        long requestId,
        Guid clientId,
        CancellationToken cancellationToken = default
    )
        : this(operationId, requestId)
    {
        if (cancellationToken == default)
            _reftoken = new(() => new(clientId));
        else
            _reftoken = new(() => new(clientId, cancellationToken));
    }

    public void Release()
    {
        if (IsReleased())
            return;

        OperationManager.Remove(OperationId);
        ReleaseInternal();
    }

    internal void ReleaseInternal()
    {
        _reftoken.Value.Dispose();
    }

    public bool Wait() => _reftoken.Value.Wait();

    public bool ReleaseAfter(int timeout) => _reftoken.Value.ReleaseAfter(timeout);

    public bool IsReleased() => _reftoken.Value.IsReleased();

    public static bool operator ==(OperationToken left, OperationToken right) =>
        left.OperationId.Equals(right.OperationId);

    public static bool operator !=(OperationToken left, OperationToken right) =>
        !left.OperationId.Equals(right.OperationId);

    public override bool Equals(object obj) => obj is OperationToken token && Equals(token);

    public static bool Equals(OperationToken other) => Equals(other);

    public override int GetHashCode()
    {
        return OperationId.GetHashCode();
    }
}

internal partial class OperationTokenRef : IDisposable
{
    private bool _isDisposed = false;

    private readonly CancellationTokenSource _cancelTokenSource;
    private readonly ConcurrentBag<CancellationTokenSource> _linkedTokens = [];

    internal Guid ClientId { get; private set; }
    internal readonly bool HasClientId;

    internal CancellationTokenSource CancelTokenSource
    {
        get =>
            !_isDisposed
                ? _cancelTokenSource
                : throw new NullReferenceException(nameof(CancelTokenSource));
    }

    internal CancellationTokenSource LinkedCancelTokenSource
    {
        get
        {
            if (_isDisposed)
                throw new NullReferenceException(nameof(LinkedCancelTokenSource));

            var cancelToken = CancellationTokenSource.CreateLinkedTokenSource(
                _cancelTokenSource.Token
            );
            _linkedTokens.Add(cancelToken);

            return cancelToken;
        }
    }

    internal OperationTokenRef() => _cancelTokenSource = new();

    internal OperationTokenRef(Guid clientId)
        : this()
    {
        ClientId = clientId;
        HasClientId = true;
    }

    internal OperationTokenRef(CancellationToken cancelToken) =>
        _cancelTokenSource =
            cancelToken == default
                ? throw new Exception("")
                : CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

    internal OperationTokenRef(Guid clientId, CancellationToken cancelToken)
        : this(cancelToken)
    {
        ClientId = clientId;
        HasClientId = true;
    }

    internal bool Wait()
    {
        if (!_isDisposed)
            _cancelTokenSource.Token.WaitHandle.WaitOne();

        return !_isDisposed;
    }

    internal bool ReleaseAfter(int timeout)
    {
        if (!_isDisposed)
            _cancelTokenSource.CancelAfter(timeout);

        return !_isDisposed;
    }

    internal bool IsReleased() => _isDisposed;

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            foreach (var token in _linkedTokens)
            {
                token?.Cancel();
                token?.Dispose();
            }

            _cancelTokenSource?.Cancel();
            _cancelTokenSource?.Dispose();
        }
        catch { }
    }
}
