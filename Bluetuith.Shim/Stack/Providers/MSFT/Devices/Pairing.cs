using System.Collections.Concurrent;
using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Executor.OutputStream;
using Bluetuith.Shim.Stack.Events;
using Bluetuith.Shim.Types;
using InTheHand.Net;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices;

internal class Pairing
{
    private static readonly ConcurrentDictionary<BluetoothAddress, OperationToken> _pendingPairing =
        new();

    private int _timeout;

    internal async Task<ErrorData> PairAsync(
        OperationToken operationToken,
        string address,
        int timeout = 10
    )
    {
        try
        {
            _timeout = timeout * 1000;
            _pendingPairing.TryAdd(BluetoothAddress.Parse(address), operationToken);

            using BluetoothDevice device = await DeviceUtils.GetBluetoothDevice(address);

            if (device.DeviceInformation.Pairing.IsPaired)
            {
                return Errors.ErrorNone;
            }

            OperationManager.MarkAsExtended(operationToken);

            device.DeviceInformation.Pairing.Custom.PairingRequested += PairingRequestEventHandler;
            DevicePairingResult pairedStatus =
                await device.DeviceInformation.Pairing.Custom.PairAsync(
                    DevicePairingKinds.DisplayPin
                        | DevicePairingKinds.ConfirmPinMatch
                        | DevicePairingKinds.ConfirmOnly
                );
            device.DeviceInformation.Pairing.Custom.PairingRequested -= PairingRequestEventHandler;

            if (pairedStatus.Status != DevicePairingResultStatus.Paired)
            {
                throw new Exception($"Device pairing status is: {pairedStatus.Status}");
            }
        }
        catch (Exception e)
        {
            return Errors.ErrorDevicePairing.WrapError(
                new() { { "device-address", address }, { "exception", e.Message } }
            );
        }
        finally
        {
            CancelPairing(address);
        }

        return Errors.ErrorNone;
    }

    internal static ErrorData CancelPairing(string address)
    {
        if (_pendingPairing.TryRemove(BluetoothAddress.Parse(address), out var token))
            token.Release();
        else
            return Errors.ErrorDeviceNotFound;

        return Errors.ErrorNone;
    }

    private void PairingRequestEventHandler(
        DeviceInformationCustomPairing sender,
        DevicePairingRequestedEventArgs args
    )
    {
        switch (args.PairingKind)
        {
            case DevicePairingKinds.DisplayPin:
            case DevicePairingKinds.ConfirmOnly:
            case DevicePairingKinds.ConfirmPinMatch:
                Deferral deferral = args.GetDeferral();

                DeviceUtils.ParseAepId(args.DeviceInformation.Id, out var _, out var deviceAddress);
                if (GetAuthResponse(deviceAddress, args.Pin, args.PairingKind))
                {
                    args.Accept();
                }

                deferral.Complete();
                break;

            default:
                args.Accept();
                break;
        }
    }

    private bool GetAuthResponse(string _address, string pin, DevicePairingKinds pairingKinds)
    {
        if (!_pendingPairing.TryGetValue(BluetoothAddress.Parse(_address), out var token))
            return false;

        PairingAuthenticationEvent authEvent = new(_address, pin, _timeout, pairingKinds, token);

        return Output.ConfirmAuthentication(authEvent, token);
    }
}
