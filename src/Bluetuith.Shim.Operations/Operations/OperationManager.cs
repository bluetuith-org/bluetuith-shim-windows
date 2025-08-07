using System.Collections.Concurrent;
using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Operations;

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

    public static void ExecuteHandler(OperationToken token, string[] args)
    {
        if (operations.TryAdd(token.OperationId, new(token, false, false)))
        {
            CommandParser.Parse(token, args);
        }
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

    public static void RemoveToken(OperationToken token)
    {
        if (operations.TryGetValue(token.OperationId, out var instance) && !instance.IsTask)
            Cancel(token.OperationId);
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

    public static OperationToken GenerateToken(
        long requestId,
        Guid clientId,
        CancellationToken token = default
    )
    {
        var operationId = ++_operationId;

        return new OperationToken(operationId, requestId, clientId, token, Remove);
    }

    public static OperationToken GenerateToken(long requestId, CancellationToken token = default)
    {
        var operationId = ++_operationId;

        if (token == default)
            return new OperationToken(operationId, requestId);

        return new OperationToken(operationId, requestId, token, Remove);
    }

    public static void AddToken(OperationToken token)
    {
        operations.TryAdd(token.OperationId, new OperationInstance(token, false, false));
    }

    public static void Remove(long operationId)
    {
        operations.TryRemove(operationId, out _);
    }
}

public class ParseResult
{
    internal async Task InvokeAsync(CancellationToken token)
    {
        await Task.CompletedTask;
    }
}
