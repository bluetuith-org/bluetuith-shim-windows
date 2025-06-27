namespace Bluetuith.Shim.Types;

public partial class EventTypes
{
    protected const byte Adapter = 2,
        Device = 3,
        FileTransfer = 4,
        MediaPlayer = 5,
        Authentication = 6;

    public static readonly EventType EventAdapter = new(Adapter);
    public static readonly EventType EventDevice = new(Device);
    public static readonly EventType EventFileTransfer = new(FileTransfer);
    public static readonly EventType EventMediaPlayer = new(MediaPlayer);
    public static readonly EventType EventAuthentication = new(Authentication);
}
