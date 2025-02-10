using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Stack.Providers.MSFT.Adapters;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices.ConnectionMethods;
using Bluetuith.Shim.Stack.Providers.MSFT.Devices.Profiles;
using Bluetuith.Shim.Types;
using DotNext;
using DotNext.Collections.Generic;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using Windows.Devices.Bluetooth;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices;

internal static class Connection
{
    private static bool _operationInProgress;

    private static readonly Guid[] AppSupportedProfiles =
    [
        BluetoothService.AudioSource,
        BluetoothService.AudioSink,
        BluetoothService.Handsfree,
        BluetoothService.Headset,
    ];

    internal static ErrorData Connect(OperationToken token, string address)
    {
        if (_operationInProgress)
            return Errors.ErrorOperationInProgress;

        _operationInProgress = true;

        try
        {
            AdapterMethods.ThrowIfRadioNotOperable();

            if (!IsAdapterSupported(token, out var error, AppSupportedProfiles))
                return error;

            if (
                !IsDeviceSupported(
                    out var device,
                    out var supportedProfiles,
                    out error,
                    token,
                    address,
                    AppSupportedProfiles
                )
            )
                return error;

            return TryConnectionMethods(token, device, supportedProfiles);
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceNotConnected.AddMetadata("exception", e.Message);
        }
        finally
        {
            _operationInProgress = false;
        }
    }

    internal static ErrorData ConnectProfile(OperationToken token, string address, Guid profileGuid)
    {
        if (_operationInProgress)
            return Errors.ErrorOperationInProgress;

        _operationInProgress = true;

        try
        {
            AdapterMethods.ThrowIfRadioNotOperable();

            if (!IsAdapterSupported(token, out var error, profileGuid))
                return error;

            if (!IsDeviceSupported(out var device, out _, out error, token, address, profileGuid))
                return error;

            return TryConnectionMethods(token, device, profileGuid);
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceNotConnected.AddMetadata("exception", e.Message);
        }
        finally
        {
            _operationInProgress = false;
        }
    }

    internal static ErrorData Disconnect(OperationToken token, string address)
    {
        if (_operationInProgress)
            return Errors.ErrorOperationInProgress;

        _operationInProgress = true;

        try
        {
            return AdapterMethods.DisconnectDevice(address);
        }
        finally
        {
            _operationInProgress = false;
        }
    }

    internal static ErrorData DisconnectProfile(
        OperationToken token,
        string address,
        Guid profileGuid
    )
    {
        return Disconnect(token, address);
    }

    private static ErrorData TryConnectionMethods(
        OperationToken token,
        DeviceModel device,
        params Guid[] supportedProfiles
    )
    {
        var error = Errors.ErrorDeviceNotConnected;
        var timeout = ConnectionSettings.TimeoutCancelToken;

        Func<ErrorData, bool> checkError = (e) =>
        {
            if (error != Errors.ErrorNone)
                error = e;

            return e == Errors.ErrorNone;
        };

        using var windowsDevice = BluetoothDevice
            .FromBluetoothAddressAsync(BluetoothAddress.Parse(device.Address))
            .GetAwaiter()
            .GetResult();
        windowsDevice.ConnectionStatusChanged += (s, e) => timeout.Cancel();

        if (
            supportedProfiles.Contains(AppSupportedProfiles[0])
            && A2dp.StartAudioSessionAsync(token, device.Address).Result is var a2dpError
        )
            if (checkError(a2dpError))
                goto FinishConnection;

        if (
            supportedProfiles.Count() > 0
            && AudioEndpoints.Connect(device, windowsDevice.DeviceInformation) is var audioError
        )
            if (checkError(audioError))
                goto FinishConnection;

        if (PnPDeviceStates.Toggle(device) is var toggleError)
            checkError(toggleError);

        FinishConnection:
        if (error != Errors.ErrorNone)
            return error;

        timeout.Token.WaitHandle.WaitOne();
        if (windowsDevice.ConnectionStatus != BluetoothConnectionStatus.Connected)
            error = Errors.ErrorDeviceNotConnected;

        return error;
    }

    private static bool IsDeviceSupported(
        out DeviceModel device,
        out Guid[] supportedProfiles,
        out ErrorData errors,
        in OperationToken token,
        in string address,
        params Guid[] profiles
    )
    {
        errors = Errors.ErrorNone;
        supportedProfiles = null;
        device = null;

        if (token.CancelTokenSource.IsCancellationRequested)
        {
            errors = Errors.ErrorOperationCancelled;
            goto FinishChecks;
        }

        (device, errors) = DeviceModelExt.ConvertToDeviceModel(address);
        if (errors != Errors.ErrorNone)
        {
            goto FinishChecks;
        }

        if (
            !device.OptionConnected.TryGet(out var connected)
            || !device.OptionUUIDs.TryGet(out var uuids)
        )
        {
            errors = Errors.ErrorUnexpected.AddMetadata("exception", "properties not found");
            goto FinishChecks;
        }

        if (connected)
        {
            errors = Errors.ErrorDeviceAlreadyConnected;
            goto FinishChecks;
        }

        supportedProfiles = uuids.ToList().Intersect(profiles).ToArray();
        if (supportedProfiles.Count() == 0)
            errors = Errors.ErrorAdapterServicesNotSupported.AddMetadata(
                "profiles",
                profiles.ToString(", ")
            );

        FinishChecks:
        return errors == Errors.ErrorNone;
    }

    private static bool IsAdapterSupported(
        in OperationToken token,
        out ErrorData errors,
        params Guid[] profiles
    )
    {
        AdapterMethods.ThrowIfRadioNotOperable();

        errors = Errors.ErrorNone;
        if (token.CancelTokenSource.IsCancellationRequested)
        {
            errors = Errors.ErrorOperationCancelled;
            goto FinishChecks;
        }

        var (adapter, convertError) = AdapterModelExt.ConvertToAdapterModel();
        if (convertError != Errors.ErrorNone)
        {
            errors = convertError;
            goto FinishChecks;
        }

        var supportedProfiles = adapter.UUIDs.ToList().Intersect(profiles);
        if (supportedProfiles.Count() == 0)
            errors = Errors.ErrorAdapterServicesNotSupported.AddMetadata(
                "profiles",
                profiles.ToString(", ")
            );

        FinishChecks:
        return errors == Errors.ErrorNone;
    }
}
