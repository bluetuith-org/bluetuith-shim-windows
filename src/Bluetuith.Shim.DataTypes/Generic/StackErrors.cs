namespace Bluetuith.Shim.DataTypes;

internal record class StackErrorCode : ErrorCode
{
    internal enum ErrorCodeValue : int
    {
        ERR_ADAPTER_NOT_FOUND,
        ERR_ADAPTER_POWER_MODE_ACCESS,
        ERR_ADAPTER_SERVICES_NOT_SUPPORTED,
        ERR_DEVICE_DISCOVERY,
        ERR_PAIRING_RESET,
        ERR_DEVICE_SERVICES_NOT_FOUND,
        ERR_DEVICE_SERVICES_NOT_SUPPORTED,
        ERR_DEVICE_NOT_PAIRED,
        ERR_DEVICE_UNPAIRING,
        ERR_DEVICE_NOT_FOUND,
        ERR_DEVICE_NOT_CONNECTED,
        ERR_DEVICE_ALREADY_CONNECTED,
        ERR_DEVICE_DISCONNECT,
        ERR_DEVICE_FILE_TRANSFER_CLIENT,
        ERR_DEVICE_FILE_TRANSFER_SERVER,
        ERR_DEVICE_PHONEBOOK_CLIENT,
        ERR_DEVICE_MESSAGE_ACCESS_CLIENT,
        ERR_DEVICE_MESSAGE_ACCESS_SERVER,
        ERR_DEVICE_A2DP_CLIENT,
    }

    internal StackErrorCode(ErrorCodeValue value)
        : base(value.ToString(), (int)value) { }

    internal static ErrorCode ERR_ADAPTER_NOT_FOUND = new StackErrorCode(
        ErrorCodeValue.ERR_ADAPTER_NOT_FOUND
    );
    internal static ErrorCode ERR_ADAPTER_POWER_MODE_ACCESS = new StackErrorCode(
        ErrorCodeValue.ERR_ADAPTER_POWER_MODE_ACCESS
    );
    internal static ErrorCode ERR_ADAPTER_SERVICES_NOT_SUPPORTED = new StackErrorCode(
        ErrorCodeValue.ERR_ADAPTER_SERVICES_NOT_SUPPORTED
    );
    internal static ErrorCode ERR_DEVICE_DISCOVERY = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_DISCOVERY
    );
    internal static ErrorCode ERR_DEVICE_PAIRING = new StackErrorCode(
        ErrorCodeValue.ERR_PAIRING_RESET
    );
    internal static ErrorCode ERR_DEVICE_SERVICES_NOT_FOUND = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_SERVICES_NOT_FOUND
    );
    internal static ErrorCode ERR_DEVICE_SERVICES_NOT_SUPPORTED = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_SERVICES_NOT_SUPPORTED
    );
    internal static ErrorCode ERR_DEVICE_NOT_PAIRED = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_NOT_PAIRED
    );
    internal static ErrorCode ERR_DEVICE_UNPAIRING = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_UNPAIRING
    );
    internal static ErrorCode ERR_DEVICE_NOT_FOUND = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_NOT_FOUND
    );
    internal static ErrorCode ERR_DEVICE_NOT_CONNECTED = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_NOT_CONNECTED
    );
    internal static ErrorCode ERR_DEVICE_ALREADY_CONNECTED = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_ALREADY_CONNECTED
    );
    internal static ErrorCode ERR_DEVICE_DISCONNECT = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_DISCONNECT
    );
    internal static ErrorCode ERR_DEVICE_FILE_TRANSFER_SESSION = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_FILE_TRANSFER_CLIENT
    );
    internal static ErrorCode ERR_DEVICE_FILE_TRANSFER_SERVER = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_FILE_TRANSFER_SERVER
    );
    internal static ErrorCode ERR_DEVICE_PHONEBOOK_CLIENT = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_PHONEBOOK_CLIENT
    );
    internal static ErrorCode ERR_DEVICE_MESSAGE_ACCESS_CLIENT = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_MESSAGE_ACCESS_CLIENT
    );
    internal static ErrorCode ERR_DEVICE_MESSAGE_ACCESS_SERVER = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_MESSAGE_ACCESS_SERVER
    );
    internal static ErrorCode ERR_DEVICE_A2DP_CLIENT = new StackErrorCode(
        ErrorCodeValue.ERR_DEVICE_A2DP_CLIENT
    );
}

public partial class Errors
{
    public static ErrorData ErrorAdapterNotFound = new(
        Code: StackErrorCode.ERR_ADAPTER_NOT_FOUND,
        Description: "An adapter was not found"
    );

    public static readonly ErrorData ErrorAdapterStateAccess = new(
        Code: StackErrorCode.ERR_ADAPTER_POWER_MODE_ACCESS,
        Description: "An error occurred while accessing adapter states"
    );

    public static readonly ErrorData ErrorAdapterServicesNotSupported = new(
        Code: StackErrorCode.ERR_ADAPTER_SERVICES_NOT_SUPPORTED,
        Description: "These services is not supported by the adapter."
    );

    public static readonly ErrorData ErrorDeviceDiscovery = new(
        Code: StackErrorCode.ERR_DEVICE_DISCOVERY,
        Description: "An unexpected error occurred during device discovery"
    );

    public static readonly ErrorData ErrorDeviceNotConnected = new(
        Code: StackErrorCode.ERR_DEVICE_NOT_CONNECTED,
        Description: "The device is not connected"
    );

    public static readonly ErrorData ErrorDeviceAlreadyConnected = new(
        Code: StackErrorCode.ERR_DEVICE_ALREADY_CONNECTED,
        Description: "The device is already connected"
    );

    public static readonly ErrorData ErrorDeviceDisconnect = new(
        Code: StackErrorCode.ERR_DEVICE_DISCONNECT,
        Description: "The device cannot be disconnected"
    );

    public static readonly ErrorData ErrorDevicePairing = new(
        Code: StackErrorCode.ERR_DEVICE_PAIRING,
        Description: "Cannot pair device"
    );

    public static readonly ErrorData ErrorDeviceNotFound = new(
        Code: StackErrorCode.ERR_DEVICE_NOT_FOUND,
        Description: "Cannot find device with the specified address"
    );

    public static readonly ErrorData ErrorDeviceUnpairing = new(
        Code: StackErrorCode.ERR_DEVICE_UNPAIRING,
        Description: "The device cannot be unpaired"
    );

    public static readonly ErrorData ErrorDeviceServicesNotSupported = new(
        Code: StackErrorCode.ERR_DEVICE_SERVICES_NOT_SUPPORTED,
        Description: "These services is not supported by the device."
    );

    public static readonly ErrorData ErrorDeviceServicesNotFound = new(
        Code: StackErrorCode.ERR_DEVICE_SERVICES_NOT_FOUND,
        Description: "The device's list of services was not found"
    );

    public static readonly ErrorData ErrorDeviceFileTransferSession = new(
        Code: StackErrorCode.ERR_DEVICE_FILE_TRANSFER_SESSION,
        Description: "An error occurred during a file transfer client session"
    );

    public static readonly ErrorData ErrorDevicePhonebookClient = new(
        Code: StackErrorCode.ERR_DEVICE_PHONEBOOK_CLIENT,
        Description: "An error occurred during a phonebook download session"
    );

    public static readonly ErrorData ErrorDeviceMessageAccessClient = new(
        Code: StackErrorCode.ERR_DEVICE_MESSAGE_ACCESS_CLIENT,
        Description: "An error occurred during a message access client session"
    );

    public static readonly ErrorData ErrorDeviceMessageAccessServer = new(
        Code: StackErrorCode.ERR_DEVICE_MESSAGE_ACCESS_SERVER,
        Description: "An error occurred during a message access server session"
    );

    public static readonly ErrorData ErrorDeviceA2dpClient = new(
        Code: StackErrorCode.ERR_DEVICE_A2DP_CLIENT,
        Description: "An error occurred during an A2DP client session"
    );
}
