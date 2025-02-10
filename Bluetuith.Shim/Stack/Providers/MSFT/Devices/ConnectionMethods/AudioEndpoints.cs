/*MIT License

Copyright (c) 2020 PolarGoose

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

using System.Runtime.InteropServices;
using System.Security;
using Bluetuith.Shim.Stack.Models;
using Bluetuith.Shim.Types;
using Vanara.PInvoke;
using Windows.Devices.Enumeration;
using static Vanara.PInvoke.CoreAudio;
using static Vanara.PInvoke.Ole32;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices.ConnectionMethods;

internal static class AudioEndpoints
{
    internal static ErrorData Connect(DeviceModel device, DeviceInformation properties)
    {
        try
        {
            var containerId = (Guid)properties.Properties["System.Devices.Aep.ContainerId"];

            var audioDevices = AudioEnumerator.GetAudioDevices(containerId);
            if (audioDevices.Count() == 0)
                return Errors.ErrorUnexpected.AddMetadata("exception", "no audio devices found");

            foreach (var audioDevice in AudioEnumerator.GetAudioDevices(containerId))
            {
                audioDevice.Connect();
            }
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceNotConnected.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }
}

internal static class AudioEnumerator
{
    internal static IEnumerable<AudioDevice> GetAudioDevices(Guid containerId)
    {
        var audioEndpointsEnumerator = new IMMDeviceEnumerator();
        foreach (var audioEndPoint in EnumerateAudioEndpoints(audioEndpointsEnumerator))
        {
            foreach (var connector in EnumerateConnectors(audioEndPoint))
            {
                if (!connector.TryGetConnectedToPart(out var connectedToPart))
                    continue;

                var connectedToDeviceId = (string)connectedToPart.GetTopologyObject().GetDeviceId();
                if (!connectedToDeviceId.StartsWith(@"{2}.\\?\bth"))
                    continue;

                var connectedToDevice = audioEndpointsEnumerator.GetDevice(connectedToDeviceId);
                var ksControl = Activate<IKsControl>(connectedToDevice);
                yield return new AudioDevice(audioEndPoint, ksControl);
            }
        }
    }

    private static IEnumerable<IMMDevice> EnumerateAudioEndpoints(IMMDeviceEnumerator enumerator)
    {
        var deviceCollection = enumerator.EnumAudioEndpoints(
            EDataFlow.eAll,
            DEVICE_STATE.DEVICE_STATEMASK_ALL
        );
        for (uint i = 0; i < deviceCollection.GetCount(); i++)
        {
            deviceCollection.Item(i, out var device);
            yield return device;
        }
    }

    private static IEnumerable<IConnector> EnumerateConnectors(IMMDevice audioEndPoint)
    {
        var topology = Activate<IDeviceTopology>(audioEndPoint);
        for (uint i = 0; i < topology.GetConnectorCount(); i++)
        {
            yield return topology.GetConnector(i);
        }
    }

    private static bool TryGetConnectedToPart(this IConnector connector, out IPart part)
    {
        part = null;

        try
        {
            part = (IPart)connector.GetConnectedTo();
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    private static T Activate<T>(IMMDevice device)
    {
        device.Activate(typeof(T).GUID, Ole32.CLSCTX.CLSCTX_ALL, null, out var itf);
        return (T)itf;
    }
}

internal sealed class AudioDevice
{
    private readonly IMMDevice device;
    private readonly IKsControl ksControl;

    internal readonly Guid ContainerId;
    internal bool IsConnected
    {
        get => device.GetState() == DEVICE_STATE.DEVICE_STATE_ACTIVE;
    }

    internal AudioDevice(IMMDevice device, IKsControl ksControl)
    {
        this.ksControl = ksControl;
        this.device = device;

        var propertyStore = device.OpenPropertyStore(STGM.STGM_READ);
        ContainerId = (Guid)propertyStore.GetValue(PROPERTYKEY.System.Devices.ContainerId);
    }

    internal void Connect()
    {
        GetKsProperty(KSPROPERTY_BLUETOOTHAUDIO.KSPROPERTY_ONESHOT_RECONNECT);
    }

    internal void Disconnect()
    {
        GetKsProperty(KSPROPERTY_BLUETOOTHAUDIO.KSPROPERTY_ONESHOT_DISCONNECT);
    }

    private void GetKsProperty(KSPROPERTY_BLUETOOTHAUDIO BLUETOOTHAUDIOProperty)
    {
        var ksProperty = new KsProperty(
            KsPropertyId.KSPROPSETID_BLUETOOTHAUDIO,
            BLUETOOTHAUDIOProperty,
            KsPropertyKind.KSPROPERTY_TYPE_GET
        );
        var dwReturned = 0;
        ksControl.KsProperty(
            ksProperty,
            Marshal.SizeOf(ksProperty),
            IntPtr.Zero,
            0,
            ref dwReturned
        );
    }
}

internal enum KsPropertyKind : uint
{
    KSPROPERTY_TYPE_GET = 0x00000001,
    KSPROPERTY_TYPE_SET = 0x00000002,
    KSPROPERTY_TYPE_TOPOLOGY = 0x10000000,
}

internal enum KSPROPERTY_BLUETOOTHAUDIO : uint
{
    KSPROPERTY_ONESHOT_RECONNECT = 0,
    KSPROPERTY_ONESHOT_DISCONNECT = 1,
}

internal static class KsPropertyId
{
    internal static readonly Guid KSPROPSETID_BLUETOOTHAUDIO = new(
        "7fa06c40-b8f6-4c7e-8556-e8c33a12e54d"
    );
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct KsProperty(Guid set, KSPROPERTY_BLUETOOTHAUDIO id, KsPropertyKind flags)
{
    internal Guid Set { get; } = set;
    internal KSPROPERTY_BLUETOOTHAUDIO Id { get; } = id;
    internal KsPropertyKind Flags { get; } = flags;
}

[
    ComImport,
    SuppressUnmanagedCodeSecurity,
    Guid("28F54685-06FD-11D2-B27A-00A0C9223196"),
    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)
]
internal interface IKsControl
{
    [PreserveSig]
    int KsProperty(
        [In] ref KsProperty Property,
        [In] int PropertyLength,
        [In, Out] IntPtr PropertyData,
        [In] int DataLength,
        [In, Out] ref int BytesReturned
    );

    [PreserveSig]
    int KsMethod(
        [In] ref KsProperty Method,
        [In] int MethodLength,
        [In, Out] IntPtr MethodData,
        [In] int DataLength,
        [In, Out] ref int BytesReturned
    );

    [PreserveSig]
    int KsEvent(
        [In, Optional] ref KsProperty Event,
        [In] int EventLength,
        [In, Out] IntPtr EventData,
        [In] int DataLength,
        [In, Out] ref int BytesReturned
    );
}
