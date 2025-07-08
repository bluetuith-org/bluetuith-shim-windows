using Bluetuith.Shim.DataTypes;
using ConsoleAppFramework;

namespace Bluetuith.Shim.Operations;

[Hidden]
/// <summary>Perform functions related to an ongoing RPC operation within a session.</summary>
[RegisterCommands("rpc")]
public class Rpc
{
    [Hidden]
    /// <summary>Get the platform-specific information of the Bluetooth stack and the Operating System the server is running on.</summary>
    public int PlatformInfo(ConsoleAppContext context)
    {
        return Output.Result(
            BluetoothStack.CurrentStack.GetPlatformInfo(),
            Errors.ErrorNone,
            (OperationToken)context.State
        );
    }

    [Hidden]
    /// <summary>Show the features of the RPC server.</summary>
    public int FeatureFlags(ConsoleAppContext context)
    {
        var features = BluetoothStack.CurrentStack.GetFeatureFlags();

        return Output.Result(features, Errors.ErrorNone, (OperationToken)context.State);
    }

    [Hidden]
    /// <summary>Show the current version information of the RPC server.</summary>
    public int Version(ConsoleAppContext context)
    {
        return Output.Result(
            ConsoleApp.Version.ToResult("Version", "version"),
            Errors.ErrorNone,
            (OperationToken)context.State
        );
    }

    [Hidden]
    /// <summary>Set the response for a pending authentication request attached to an operation ID.</summary>
    /// <param name="response">-r, The response to sent to the authentication request.</param>
    /// <param name="authenticationId">-a, The ID of the authentication request.</param>
    public int Auth(ConsoleAppContext context, ushort authenticationId, string response)
    {
        if (
            !Output.ReplyToAuthenticationRequest(
                (OperationToken)context.State,
                authenticationId,
                response
            )
        )
            return Output.Error(
                Errors.ErrorOperationCancelled.AddMetadata(
                    "exception",
                    "No authentication ID or registered agent found: " + authenticationId
                ),
                (OperationToken)context.State
            );

        return Output.Error(Errors.ErrorNone, (OperationToken)context.State);
    }
}

[Hidden]
/// <summary>Set a primary agent for a client to receive authentication events.</summary>
[RegisterCommands("rpc agent")]
public class Agent
{
    [Hidden]
    /// <summary>Register an agent for pairing or file-transfer related authentication events.</summary>
    /// <param name="agentType">-a, The type of agent that the client requests to be registered as.</param>
    public int Register(ConsoleAppContext context, AuthenticationManager.AuthAgentType agentType)
    {
        var err = AuthenticationManager.RegisterAuthAgent(
            agentType,
            ((OperationToken)context.State).ClientId
        );
        return Output.Error(err, (OperationToken)context.State);
    }

    [Hidden]
    /// <summary>Unregister a registered agent for pairing or file-transfer related authentication events.</summary>
    /// <param name="agentType">-a, The type of agent that the client requests to be unregistered from.</param>
    public int Unregister(ConsoleAppContext context, AuthenticationManager.AuthAgentType agentType)
    {
        var err = AuthenticationManager.UnregisterAuthAgent(
            agentType,
            ((OperationToken)context.State).ClientId
        );
        return Output.Error(err, (OperationToken)context.State);
    }
}
