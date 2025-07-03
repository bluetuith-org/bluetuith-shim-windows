using Bluetuith.Shim.DataTypes;
using DotNext.Threading;

namespace Bluetuith.Shim.Operations;

public static class AuthenticationManager
{
    private static readonly Dictionary<Guid, Dictionary<long, AuthenticationEvent>> events = new();

    private static Atomic<Guid> _pairingAgent = new();
    private static Atomic<Guid> _obexAgent = new();

    static AuthenticationManager()
    {
        _pairingAgent.Write(Guid.Empty);
        _obexAgent.Write(Guid.Empty);
    }

    public enum AuthAgentType : int
    {
        None = 0,
        Pairing,
        Obex,
    }

    internal static ErrorData RegisterAuthAgent(AuthAgentType authAgentType, Guid clientId)
    {
        if (clientId == default)
            return Errors.ErrorUnexpected.AddMetadata("exception", "Invalid client ID");

        var registered = false;

        switch (authAgentType)
        {
            case AuthAgentType.Pairing:
                registered = _pairingAgent.CompareAndSet(Guid.Empty, clientId);
                break;
            case AuthAgentType.Obex:
                registered = _obexAgent.CompareAndSet(Guid.Empty, clientId);
                break;
            case AuthAgentType.None:
                return Errors.ErrorUnexpected.AddMetadata(
                    "exception",
                    "No authentication agent specified"
                );
        }

        if (!registered)
            return Errors.ErrorUnexpected.AddMetadata(
                "exception",
                "Authentication agent is already registered"
            );

        return Errors.ErrorNone;
    }

    internal static ErrorData UnregisterAuthAgent(AuthAgentType authAgentType, Guid clientId)
    {
        if (clientId == default)
            return Errors.ErrorUnexpected.AddMetadata("exception", "Invalid client ID");

        switch (authAgentType)
        {
            case AuthAgentType.Pairing:
                _pairingAgent.CompareAndSet(clientId, Guid.Empty);
                break;
            case AuthAgentType.Obex:
                _obexAgent.CompareAndSet(clientId, Guid.Empty);
                break;
            case AuthAgentType.None:
                return Errors.ErrorUnexpected.AddMetadata(
                    "exception",
                    "No authentication agent specified"
                );
        }

        return Errors.ErrorNone;
    }

    internal static void RemoveAgentsAndEvents(Guid clientId)
    {
        UnregisterAuthAgent(AuthAgentType.Pairing, clientId);
        UnregisterAuthAgent(AuthAgentType.Obex, clientId);

        using var _ = events.AcquireWriteLock();

        if (events.TryGetValue(clientId, out var authList) && authList != null)
        {
            foreach (var authEvent in authList.Values)
            {
                authEvent.Deny();
            }
        }
    }

    internal static bool AddEvent(
        OperationToken token,
        AuthenticationEvent authenticationEvent,
        AuthAgentType authAgentType = AuthAgentType.None
    )
    {
        using var _ = events.AcquireWriteLock();

        Guid clientId = Guid.Empty;

        switch (authAgentType)
        {
            case AuthAgentType.Pairing:
                _pairingAgent.Read(out clientId);
                break;

            case AuthAgentType.Obex:
                _obexAgent.Read(out clientId);
                break;

            case AuthAgentType.None:
                clientId = token.ClientId;
                break;
        }

        if (clientId == Guid.Empty)
        {
            authenticationEvent.Deny();
            return false;
        }

        token.ClientId = clientId;

        if (!events.TryGetValue(clientId, out var authList))
        {
            authList = [];
            events.Add(clientId, authList);
        }

        return authList.TryAdd(authenticationEvent.CurrentAuthId, authenticationEvent);
    }

    internal static bool SetEventResponse(OperationToken token, long authId, string response)
    {
        using var _ = events.AcquireWriteLock();

        var found = true;

        if (events.TryGetValue(token.ClientId, out var authList))
        {
            if (authList != null && authList.Remove(authId, out var authEvent))
            {
                authEvent.TryAccept(response);
                found = true;
            }

            if (authList == null || authList?.Count == 0)
                events.Remove(token.ClientId);
        }

        return found;
    }
}
