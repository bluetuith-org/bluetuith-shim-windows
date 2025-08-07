using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Stack.Microsoft;

internal partial class OppServerMonitor : AdapterStateMonitor
{
    public override MonitorName Name => MonitorName.OppServerStateMonitor;

    public override bool RestartOnPowerStateChanged => true;

    public override bool IsRunning => IsCreated;

    public override bool IsCreated =>
        radio != null && Opp.IsServerSessionExist(_token, AdapterAddress, out _);

    public override void Create(OperationToken token)
    {
        base.Create(token);
    }

    public override bool Start()
    {
        if (base.Start())
            Opp.StartFileTransferServerAsync(_token).GetAwaiter().GetResult();

        return IsCreated;
    }

    public override void Stop()
    {
        Opp.StopFileTransferServer(_token);
        base.Stop();
    }

    public override void Dispose()
    {
        base.Dispose();
    }
}
