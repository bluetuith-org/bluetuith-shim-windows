using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Stack.Microsoft;

internal record class WindowsOppAuthEvent : OppAuthenticationEvent
{
    public WindowsOppAuthEvent(
        int timeout,
        IFileTransferEvent fileTransferEvent,
        OperationToken token
    )
        : base(timeout, fileTransferEvent, token) { }
}
