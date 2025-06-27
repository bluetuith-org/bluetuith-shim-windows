using System.Text.Json;
using System.Text.Json.Serialization;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Extensions;
using Bluetuith.Shim.Stack.Data.Models;
using Bluetuith.Shim.Stack.Providers.MSFT;
using Bluetuith.Shim.Types;

namespace Bluetuith.Shim.Stack;

public static class ApiInformation
{
    public static readonly byte ApiVersion = 1;
}

public interface IStack
{
    public enum FeatureFlags : uint
    {
        FeatureConnection = 1 << 1,
        FeaturePairing = 1 << 2,
        FeatureSendFile = 1 << 3,
        FeatureReceiveFile = 1 << 4,
    }

    public struct PlatformInfo : IResult
    {
        [JsonPropertyName("stack")]
        public string Stack { get; set; }

        [JsonPropertyName("os_info")]
        public string OsInfo { get; set; }

        public readonly string ToConsoleString()
        {
            return $"Stack: {Stack} ({OsInfo})";
        }

        private static readonly JsonEncodedText PlatformPropertyNme = JsonEncodedText.Encode(
            "platform"
        );

        public readonly void WriteJsonToStream(Utf8JsonWriter writer)
        {
            writer.WritePropertyName(PlatformPropertyNme);
            this.SerializeAll(writer, SerializableContext.Default);
        }
    }

    public Task<ErrorData> SetupWatchers(
        OperationToken token,
        CancellationTokenSource waitForResume
    );

    public FeatureFlags GetFeatureFlags();
    public PlatformInfo GetPlatformInfo();

    #region Adapter Methods
    public (AdapterModel, ErrorData) GetAdapter(OperationToken token);
    public ErrorData SetPoweredState(bool enable);
    public ErrorData SetPairableState(bool enable);
    public ErrorData SetDiscoverableState(bool enable);
    public (GenericResult<List<DeviceModel>>, ErrorData) GetPairedDevices();
    public ErrorData DisconnectDevice(string address);
    public ErrorData StartDeviceDiscovery(OperationToken token, int timeout = 0);
    public ErrorData StopDeviceDiscovery(OperationToken token);
    public Task<ErrorData> RemoveDeviceAsync(string address);
    #endregion

    #region Device Methods
    public (DeviceModel, ErrorData) GetDevice(OperationToken token, string address);

    public ErrorData Connect(OperationToken token, string address);
    public ErrorData ConnectProfile(OperationToken token, string address, Guid profileGuid);
    public ErrorData Disconnect(OperationToken token, string address);
    public ErrorData DisconnectProfile(OperationToken token, string address, Guid profileGuid);

    public Task<ErrorData> PairAsync(OperationToken token, string address, int timeout = 10);
    public ErrorData CancelPairing(string address);

    // Advanced Audio Distribution (A2DP) based operations
    public Task<ErrorData> StartAudioSessionAsync(OperationToken token, string address);

    // Message Access (MAP) based operations
    public Task<ErrorData> StartNotificationEventServerAsync(OperationToken token, string address);
    public Task<(MessageListingModel, ErrorData)> GetMessagesAsync(
        OperationToken token,
        string address,
        string folderPath = "telecom",
        ushort fromIndex = 0,
        ushort maxMessageCount = 0
    );
    public Task<(BMessagesModel, ErrorData)> ShowMessageAsync(
        OperationToken token,
        string address,
        string handle
    );
    public Task<(GenericResult<int>, ErrorData)> GetTotalMessageCountAsync(
        OperationToken token,
        string address,
        string folderPath
    );
    public Task<(GenericResult<List<string>>, ErrorData)> GetMessageFoldersAsync(
        OperationToken token,
        string address,
        string folderPath = ""
    );

    // Phonebook Access profile (PBAP) based operations
    public Task<(VcardModel, ErrorData)> GetAllContactsAsync(OperationToken token, string address);
    public Task<(VcardModel, ErrorData)> GetCombinedCallHistoryAsync(
        OperationToken token,
        string address
    );
    public Task<(VcardModel, ErrorData)> GetIncomingCallsHistoryAsync(
        OperationToken token,
        string address
    );
    public Task<(VcardModel, ErrorData)> GetOutgoingCallsHistoryAsync(
        OperationToken token,
        string address
    );
    public Task<(VcardModel, ErrorData)> GetMissedCallsAsync(OperationToken token, string address);
    public Task<(VcardModel, ErrorData)> GetSpeedDialAsync(OperationToken token, string address);

    // Object Push profile (OPP) based operations
    public Task<ErrorData> StartFileTransferSessionAsync(
        OperationToken token,
        string address,
        bool startQueue
    );
    public (FileTransferModel, ErrorData) QueueFileSend(string filepath);
    public Task<ErrorData> SendFileAsync(string filepath);
    public ErrorData CancelFileTransfer(string address);
    public ErrorData StopFileTransferSession();

    public Task<ErrorData> StartFileTransferServerAsync(
        OperationToken token,
        string destinationDirectory
    );
    public ErrorData StopFileTransferServer();
    #endregion
}

public static class AvailableStacks
{
    public static IStack[] Stacks { get; } = [new MSFTStack()];
    public static IStack CurrentStack { get; set; } = Stacks[0];
}
