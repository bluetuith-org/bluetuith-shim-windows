using Bluetuith.Shim.DataTypes;

namespace Bluetuith.Shim.Operations;

public interface IServer
{
    public bool IsAdministrator();
    public ErrorData StartServer(string socketPath, string parameter);
    public ErrorData StopServer(string parameter);
}
