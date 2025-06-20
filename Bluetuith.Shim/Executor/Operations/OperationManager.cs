using System.Collections.Concurrent;
using System.CommandLine;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Types;

namespace Bluetuith.Shim.Executor.Operations;

public static class OperationManager
{
    private class OperationInstance(
        OperationToken newToken,
        bool markedAsTask,
        bool cancelOnDisconnect
    )
    {
        public OperationToken token = newToken;
        public bool IsTask = markedAsTask;
        public bool CancelWhenDisconnected = cancelOnDisconnect;
    }

    private static readonly ConcurrentDictionary<long, OperationInstance> operations = new();
    private static long _operationId = 0;

    public static async Task ExecuteHandlerAsync(OperationToken token, ParseResult parsed)
    {
        if (operations.TryAdd(token.OperationId, new(token, false, false)))
        {
            await parsed.InvokeAsync(token.CancelTokenSource.Token);
        }
    }

    private static async Task<int> ExecuteThenCancelAsync(
        OperationToken token,
        Func<OperationToken, Task<int>> func
    )
    {
        try
        {
            return await func(token);
        }
        finally
        {
            if (operations.TryGetValue(token.OperationId, out var instance) && !instance.IsTask)
                Cancel(token.OperationId);
        }
    }

    private static int ExecuteThenCancel(OperationToken token, Func<OperationToken, int> func)
    {
        try
        {
            return func(token);
        }
        finally
        {
            if (operations.TryGetValue(token.OperationId, out var instance) && !instance.IsTask)
                Cancel(token.OperationId);
        }
    }

    public static int Offload(long operationId, Func<OperationToken, int> func)
    {
        if (!GetToken(operationId, out var token))
        {
            var err = Errors.ErrorUnexpected.AddMetadata(
                "exception",
                "no operation exists with ID: " + operationId
            );
            Output.Event(err, OperationToken.None);

            return err.Code.Value;
        }

        if (!Output.IsOnSocket)
            return ExecuteThenCancel(token, func);

        _ = Task.Run(() => ExecuteThenCancel(token, func));

        return 0;
    }

    public static async Task<int> OffloadAsync(
        long operationId,
        Func<OperationToken, Task<int>> func
    )
    {
        if (!GetToken(operationId, out var token))
        {
            var err = Errors.ErrorUnexpected.AddMetadata(
                "exception",
                "no operation exists with ID: " + operationId
            );
            Output.Event(err, OperationToken.None);

            return err.Code.Value;
        }

        if (!Output.IsOnSocket)
            return await ExecuteThenCancelAsync(token, func);

        _ = Task.Run(async () => await ExecuteThenCancelAsync(token, func));

        return 0;
    }

    public static bool GetToken(long operationId, out OperationToken token)
    {
        token = OperationToken.None;

        if (operations.TryGetValue(operationId, out OperationInstance instance))
        {
            token = instance.token;
            return true;
        }

        return false;
    }

    public static void Cancel(long operationId)
    {
        if (operations.TryRemove(operationId, out OperationInstance instance))
            instance.token.ReleaseInternal();
    }

    public static async Task CancelAllAsync()
    {
        foreach (OperationInstance instance in operations.Values)
            instance.token.ReleaseInternal();

        await Task.Delay(100);
    }

    public static void CancelClientOperations(Guid clientId)
    {
        foreach (OperationInstance instance in operations.Values)
        {
            if (
                instance.CancelWhenDisconnected
                && instance.token.HasClientId
                && instance.token.ClientId == clientId
            )
            {
                instance.token.Release();
            }
        }
    }

    public static void SetOperationProperties(
        OperationToken token,
        bool isExtendedTask = true,
        bool cancelOnClientDisconnect = false
    )
    {
        if (operations.TryGetValue(token.OperationId, out var instance))
        {
            instance.IsTask = isExtendedTask;
            instance.CancelWhenDisconnected = cancelOnClientDisconnect;
        }
    }

    public static void WaitForExtendedOperations()
    {
        foreach (OperationInstance instance in operations.Values)
            if (instance.IsTask)
                instance.token.Wait();
    }

    public static OperationToken GenerateToken(long requestId, Guid clientId = default)
    {
        var operationId = ++_operationId;

        if (clientId != default)
            return new OperationToken(operationId, requestId, clientId);

        return new OperationToken(operationId, requestId);
    }

    public static void AddToken(OperationToken token)
    {
        operations.TryAdd(token.OperationId, new OperationInstance(token, false, false));
    }

    internal static void Remove(long operationId)
    {
        operations.TryRemove(operationId, out _);
    }
}
