using System.Collections.Concurrent;
using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Operations;

public static class AuthenticationManager
{
    private static readonly ConcurrentDictionary<AuthAgentType, Guid> agents = new();
    private static readonly ConcurrentDictionary<Guid, AuthenticationEvent> events = new();

    public enum AuthAgentType : int
    {
        None = 0,
        Pairing,
        Obex,
    }

    public static ErrorData RegisterAuthAgent(AuthAgentType authAgentType, Guid clientId)
    {
        return Errors.ErrorNone;
    }

    public static ErrorData UnregisterAuthAgent(AuthAgentType authAgentType, Guid clientId)
    {
        return Errors.ErrorNone;
    }

    public static void ClearAuthentications(Guid clientId) { }

    public static bool GetAuthAgent(AuthAgentType authAgentType, out Guid clientId)
    {
        clientId = Guid.Empty;
        return true;
    }

    public static bool AddEvent(OperationToken token, AuthenticationEvent authenticationEvent)
    {
        return true;
    }

    public static bool RemoveEvent(
        OperationToken token,
        out AuthenticationEvent authenticationEvent
    )
    {
        authenticationEvent = null;
        return true;
    }
}
