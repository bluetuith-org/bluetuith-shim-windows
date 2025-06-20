using System.Runtime.InteropServices;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices.Profiles;
using Bluetuith.Shim.Stack.Providers.MSFT.Monitors;
using Bluetuith.Shim.Types;
using static Bluetuith.Shim.Stack.IStack;

namespace Bluetuith.Shim.Stack.Providers.MSFT;

public sealed class MSFTStack : IStack
{
    public Task<ErrorData> SetupWatchers(
        OperationToken token,
        CancellationTokenSource waitForResume
    ) => Watchers.Setup(token, waitForResume);

    public PlatformInfo GetPlatformInfo() =>
        new()
        {
            Stack = "Microsoft MSFT",
            OsInfo = $"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
        };

    public FeatureFlags GetFeatureFlags() =>
        FeatureFlags.FeatureConnection
        | FeatureFlags.FeaturePairing
        | FeatureFlags.FeatureSendFile
        | FeatureFlags.FeatureReceiveFile;

    #region Adapter Methods
    public (AdapterModel, ErrorData) GetAdapter(OperationToken token) =>
        AdapterModelExt.ConvertToAdapterModel(token);

    public ErrorData SetPoweredState(bool enable) => AdapterMethods.SetPoweredState(enable);

    public ErrorData SetPairableState(bool enable) => AdapterMethods.SetPairableState(enable);

    public ErrorData SetDiscoverableState(bool enable) =>
        AdapterMethods.SetDiscoverableState(enable);

    public (GenericResult<List<DeviceModel>>, ErrorData) GetPairedDevices() =>
        AdapterMethods.GetPairedDevices();

    public ErrorData StartDeviceDiscovery(OperationToken token, int timeout = 0) =>
        AdapterMethods.StartDeviceDiscovery(token, timeout);

    public ErrorData StopDeviceDiscovery(OperationToken token) =>
        AdapterMethods.StopDeviceDiscovery(token);

    public ErrorData DisconnectDevice(string address) => AdapterMethods.DisconnectDevice(address);

    public Task<ErrorData> RemoveDeviceAsync(string address) =>
        AdapterMethods.RemoveDeviceAsync(address);
    #endregion

    #region Device Methods
    public (DeviceModel, ErrorData) GetDevice(OperationToken token, string address) =>
        DeviceModelExt.ConvertToDeviceModel(address);

    public ErrorData Connect(OperationToken token, string address) =>
        Connection.Connect(token, address);

    public ErrorData ConnectProfile(OperationToken token, string address, Guid profileGuid) =>
        Connection.ConnectProfile(token, address, profileGuid);

    public ErrorData Disconnect(OperationToken token, string address) =>
        Connection.Disconnect(token, address);

    public ErrorData DisconnectProfile(OperationToken token, string address, Guid profileGuid) =>
        Connection.DisconnectProfile(token, address, profileGuid);

    public Task<ErrorData> PairAsync(OperationToken token, string address, int timeout = 10) =>
        new Pairing().PairAsync(token, address, timeout);

    public ErrorData CancelPairing(string address) => Pairing.CancelPairing(address);

    // Advanced Audio Distribution (A2DP) based operations
    public Task<ErrorData> StartAudioSessionAsync(OperationToken token, string address) =>
        A2dp.StartAudioSessionAsync(token, address);

    // Message Access (MAP) based operations
    public Task<ErrorData> StartNotificationEventServerAsync(
        OperationToken token,
        string address
    ) => Map.StartNotificationEventServerAsync(token, address);

    public Task<(MessageListingModel, ErrorData)> GetMessagesAsync(
        OperationToken token,
        string address,
        string folderPath = "telecom",
        ushort fromIndex = 0,
        ushort maxMessageCount = 0
    ) => Map.GetMessagesAsync(token, address, folderPath, fromIndex, maxMessageCount);

    public Task<(BMessagesModel, ErrorData)> ShowMessageAsync(
        OperationToken token,
        string address,
        string handle
    ) => Map.ShowMessageAsync(token, address, handle);

    public Task<(GenericResult<int>, ErrorData)> GetTotalMessageCountAsync(
        OperationToken token,
        string address,
        string folderPath
    ) => Map.GetTotalMessageCountAsync(token, address, folderPath);

    public Task<(GenericResult<List<string>>, ErrorData)> GetMessageFoldersAsync(
        OperationToken token,
        string address,
        string folderPath = ""
    ) => Map.GetMessageFoldersAsync(token, address, folderPath);

    // Phonebook Access PSE (PBAP) based operations
    public Task<(VcardModel, ErrorData)> GetAllContactsAsync(
        OperationToken token,
        string address
    ) => Pbap.GetAllContactsAsync(token, address);

    public Task<(VcardModel, ErrorData)> GetCombinedCallHistoryAsync(
        OperationToken token,
        string address
    ) => Pbap.GetCombinedCallHistoryAsync(token, address);

    public Task<(VcardModel, ErrorData)> GetIncomingCallsHistoryAsync(
        OperationToken token,
        string address
    ) => Pbap.GetIncomingCallsHistoryAsync(token, address);

    public Task<(VcardModel, ErrorData)> GetOutgoingCallsHistoryAsync(
        OperationToken token,
        string address
    ) => Pbap.GetOutgoingCallsHistoryAsync(token, address);

    public Task<(VcardModel, ErrorData)> GetMissedCallsAsync(
        OperationToken token,
        string address
    ) => Pbap.GetMissedCallsAsync(token, address);

    public Task<(VcardModel, ErrorData)> GetSpeedDialAsync(OperationToken token, string address) =>
        Pbap.GetSpeedDialAsync(token, address);

    // Object Push (OPP) based operations
    public Task<ErrorData> StartFileTransferSessionAsync(
        OperationToken token,
        string address,
        bool startQueue
    ) => Opp.StartFileTransferSessionAsync(token, address, startQueue);

    public Task<ErrorData> StartFileTransferServerAsync(
        OperationToken token,
        string destinationDirectory
    ) => Opp.StartFileTransferServerAsync(token, destinationDirectory);

    public (FileTransferModel, ErrorData) QueueFileSend(string filepath) =>
        Opp.QueueFileSend(filepath);

    public Task<ErrorData> SendFileAsync(string filepath) => Opp.SendFileAsync(filepath);

    public ErrorData CancelFileTransfer(string address) => Opp.CancelFileTransfer(address);

    public ErrorData StopFileTransferSession() => Opp.StopFileTransferSession();

    public ErrorData StopFileTransferServer() => Opp.StopFileTransferServer();
    #endregion
}
