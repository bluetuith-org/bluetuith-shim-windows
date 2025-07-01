using Bluetuith.Shim.DataTypes;
using Bluetuith.Shim.Operations;
using InTheHand.Net;
using Windows.Devices.Bluetooth;
using Windows.Media.Audio;

namespace Bluetuith.Shim.Stack.Microsoft;

internal static class A2dp
{
    private static bool _clientInProgress = false;

    internal static async Task<ErrorData> StartAudioSessionAsync(
        OperationToken token,
        string address
    )
    {
        if (_clientInProgress)
        {
            return Errors.ErrorOperationInProgress.WrapError(
                new()
                {
                    { "operation", "A2dp Client" },
                    {
                        "exception",
                        $"An Advanced Audio Distribution Profile client session is in progress"
                    },
                }
            );
        }

        try
        {
            AdapterMethods.ThrowIfRadioNotOperable();

            var audioDeviceId = await DeviceUtils.GetDeviceIdBySelector(
                address,
                AudioPlaybackConnection.GetDeviceSelector()
            );

            var audio = AudioPlaybackConnection.TryCreateFromId(audioDeviceId);
            if (audio.State == AudioPlaybackConnectionState.Opened)
                return Errors.ErrorDeviceAlreadyConnected;

            await audio.StartAsync();

            AudioPlaybackConnectionOpenResult openResult = await audio.OpenAsync();
            if (openResult.Status != AudioPlaybackConnectionOpenResultStatus.Success)
            {
                return Errors.ErrorDeviceA2dpClient.AddMetadata(
                    "audio-connection",
                    $"{openResult.Status}"
                );
            }

            _clientInProgress = true;
            OperationManager.SetOperationProperties(token);

            _ = Task.Run(async () =>
            {
                using (audio)
                {
                    try
                    {
                        using var windowsDevice = await BluetoothDevice.FromBluetoothAddressAsync(
                            BluetoothAddress.Parse(address)
                        );
                        windowsDevice.ConnectionStatusChanged += (s, e) =>
                        {
                            if (s.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                                token.Release();
                        };
                        if (
                            windowsDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected
                        )
                            return;

                        token.Wait();
                    }
                    finally
                    {
                        token.Release();
                        _clientInProgress = false;
                    }
                }
            });
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceA2dpClient.WrapError(new() { { "exception", e.Message } });
        }

        return Errors.ErrorNone;
    }
}
