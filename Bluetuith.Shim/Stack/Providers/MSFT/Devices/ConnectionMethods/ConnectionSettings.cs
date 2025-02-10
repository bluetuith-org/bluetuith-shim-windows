namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices.ConnectionMethods;

internal static class ConnectionSettings
{
    public static readonly int ConnectionTimeout = 6000;

    public static CancellationTokenSource TimeoutCancelToken
    {
        get
        {
            var token = new CancellationTokenSource();
            token.CancelAfter(ConnectionTimeout);

            return token;
        }
    }
}
