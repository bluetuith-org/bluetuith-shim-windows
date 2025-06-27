using Bluetuith.Shim.Executor.Operations;
using Bluetuith.Shim.Stack.Data.Models;
using Bluetuith.Shim.Types;
using GoodTimeStudio.MyPhone.OBEX.Pbap;
using InTheHand.Net.Bluetooth;
using Windows.Devices.Bluetooth;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices.Profiles;

internal static class Pbap
{
    private static bool _clientInProgress = false;

    internal static async Task<(VcardModel, ErrorData)> GetPhonebookAsync(
        OperationToken token,
        string address,
        string phonebookId
    )
    {
        if (_clientInProgress)
        {
            return (
                new(),
                Errors.ErrorOperationInProgress.WrapError(
                    new()
                    {
                        { "operation", "Phonebook Client" },
                        { "exception", "A Phonebook client session is in progress" },
                    }
                )
            );
        }

        try
        {
            _clientInProgress = true;

            using BluetoothDevice device = await DeviceUtils.GetBluetoothDeviceWithService(
                address,
                BluetoothService.PhonebookAccessPse
            );
            using BluetoothPbapClientSession pbap = new(device, token.LinkedCancelTokenSource);

            await pbap.ConnectAsync();
            if (pbap.ObexClient == null)
            {
                throw new Exception("PBAP ObexClient is null on connect");
            }

            var phonebook = await pbap.ObexClient.PullPhoneBookAsync(phonebookId);
            return (new VcardModel(phonebookId, phonebook), Errors.ErrorNone);
        }
        catch (Exception e)
        {
            return (
                new(),
                Errors.ErrorDevicePhonebookClient.WrapError(new() { { "exception", e.Message } })
            );
        }
        finally
        {
            _clientInProgress = false;
        }
    }

    internal static async Task<(VcardModel, ErrorData)> GetAllContactsAsync(
        OperationToken token,
        string address
    )
    {
        return await GetPhonebookAsync(token, address, "telecom/pb.vcf");
    }

    internal static async Task<(VcardModel, ErrorData)> GetCombinedCallHistoryAsync(
        OperationToken token,
        string address
    )
    {
        return await GetPhonebookAsync(token, address, "telecom/cch.vcf");
    }

    internal static async Task<(VcardModel, ErrorData)> GetIncomingCallsHistoryAsync(
        OperationToken token,
        string address
    )
    {
        return await GetPhonebookAsync(token, address, "telecom/ich.vcf");
    }

    internal static async Task<(VcardModel, ErrorData)> GetOutgoingCallsHistoryAsync(
        OperationToken token,
        string address
    )
    {
        return await GetPhonebookAsync(token, address, "telecom/och.vcf");
    }

    internal static async Task<(VcardModel, ErrorData)> GetMissedCallsAsync(
        OperationToken token,
        string address
    )
    {
        return await GetPhonebookAsync(token, address, "telecom/mch.vcf");
    }

    internal static async Task<(VcardModel, ErrorData)> GetSpeedDialAsync(
        OperationToken token,
        string address
    )
    {
        return await GetPhonebookAsync(token, address, "telecom/spd.vcf");
    }
}
