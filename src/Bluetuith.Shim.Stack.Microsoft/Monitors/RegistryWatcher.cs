using System.Text;
using WmiLight;

namespace Bluetuith.Shim.Stack.Microsoft;

internal sealed partial class RegistryWatcher : IWatcher
{
    private readonly WmiConnection _connection;

    private readonly WmiEventWatcher _eventWatcher;

    private readonly EventHandler<WmiEventArrivedEventArgs> _onChange;

    private bool _started = false;

    public bool IsRunning => _started;
    public bool IsCreated => _connection != null && _eventWatcher != null;

    internal RegistryWatcher(
        string hive,
        string rootPath,
        EventHandler<WmiEventArrivedEventArgs> onChange
    )
    {
        var query =
            $@"SELECT * FROM RegistryTreeChangeEvent WHERE Hive = '{hive}' AND RootPath = '{rootPath.Replace(@"\", @"\\")}'";

        _connection = new WmiConnection();
        _eventWatcher = _connection.CreateEventWatcher(query);
        _onChange = onChange;
    }

    internal RegistryWatcher(
        string hive,
        string keyPath,
        string[] valueNames,
        EventHandler<WmiEventArrivedEventArgs> onChange
    )
    {
        var sb = new StringBuilder();
        sb.Append(
            $@"SELECT * FROM RegistryValueChangeEvent WHERE Hive = '{hive}' AND KeyPath = '{keyPath.Replace(@"\", @"\\")}'"
        );

        var count = 0;
        sb.Append("AND (");
        foreach (var valueName in valueNames)
        {
            if (count > 0)
                sb.Append(" OR ");

            sb.Append($@"ValueName = '{valueName}'");
            count++;
        }
        sb.Append(')');

        _connection = new WmiConnection();
        _eventWatcher = _connection.CreateEventWatcher(sb.ToString());
        _onChange = onChange;
    }

    public bool Start()
    {
        _eventWatcher.EventArrived += _onChange;
        _eventWatcher.Start();
        _started = true;

        return true;
    }

    public void Stop()
    {
        if (!_started)
            return;

        _started = false;

        _eventWatcher.EventArrived -= _onChange;
        _eventWatcher.Stop();
        _connection.Close();
    }

    public void Dispose()
    {
        try
        {
            _started = false;

            _eventWatcher.EventArrived -= _onChange;
            _eventWatcher?.Dispose();
            _connection?.Dispose();
        }
        catch { }
    }
}
