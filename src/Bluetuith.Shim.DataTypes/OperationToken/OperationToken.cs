using System.Collections.Concurrent;

namespace Bluetuith.Shim.DataTypes;

public readonly struct OperationToken : IEquatable<OperationToken>
{
    private readonly Lazy<OperationTokenRef> _reftoken;
    private readonly Action<long> _releaseFunc = delegate { };

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
        set => _reftoken.Value.ClientId = value;
    }
    public bool HasClientId
    {
        get => _reftoken.Value.HasClientId;
    }

    public static OperationToken None
    {
        get => default;
    }

    public OperationToken(
        long operationId,
        long requestId,
        Action<long> releaseFunc = default,
        bool empty = false
    )
    {
        OperationId = operationId;
        RequestId = requestId;
        _reftoken = !empty ? new(() => new()) : null;

        if (releaseFunc != null)
            _releaseFunc = releaseFunc;
    }

    public OperationToken(
        long operationId,
        long requestId,
        CancellationToken cancellationToken,
        Action<long> releaseFunc = default
    )
        : this(operationId, requestId, releaseFunc, true)
    {
        _reftoken = new(() => new(cancellationToken));
    }

    public OperationToken(
        long operationId,
        long requestId,
        Guid clientId,
        CancellationToken cancellationToken = default,
        Action<long> releaseFunc = default
    )
        : this(operationId, requestId, releaseFunc, true)
    {
        _reftoken = new(() => new(clientId, cancellationToken));
    }

    public void Release()
    {
        if (IsReleased())
            return;

        _releaseFunc(OperationId);
        ReleaseInternal();
    }

    public void ReleaseInternal()
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

    public bool Equals(OperationToken other) => Equals(other);

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

    internal Guid ClientId { get; set; }
    internal bool HasClientId
    {
        get => ClientId != Guid.Empty;
    }

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

    internal OperationTokenRef(Guid clientId, CancellationToken cancelToken = default)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentNullException(nameof(clientId));

        ClientId = clientId;

        if (cancelToken != default)
            _cancelTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
        else
            _cancelTokenSource = new();
    }

    internal OperationTokenRef(CancellationToken cancelToken) =>
        _cancelTokenSource =
            cancelToken == default
                ? throw new ArgumentNullException(nameof(cancelToken))
                : CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

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
