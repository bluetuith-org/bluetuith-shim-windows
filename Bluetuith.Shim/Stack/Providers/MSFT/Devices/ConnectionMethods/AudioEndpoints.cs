/*
 * This is a heavily modified version of the AudioDeviceEnumerator and AudioDevice implementations
 * in the "https://github.com/PolarGoose/BluetoothDevicePairing" repository.
 *
 * Source: https://github.com/PolarGoose/BluetoothDevicePairing/tree/master/src/Bluetooth/Devices/AudioDevices.
 */

using System.Runtime.CompilerServices;
using Bluetuith.Shim.Stack.Data.Models;
using Bluetuith.Shim.Types;
using Windows.Devices.Enumeration;
using Windows.Win32;
using Windows.Win32.Media.Audio;
using Windows.Win32.Media.KernelStreaming;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell.PropertiesSystem;

namespace Bluetuith.Shim.Stack.Providers.MSFT.Devices.ConnectionMethods;

internal static class AudioEndpoints
{
    internal static ErrorData Connect(DeviceModel device, DeviceInformation properties)
    {
        try
        {
            var containerId = (Guid)properties.Properties["System.Devices.Aep.ContainerId"];

            if (!AudioDevices.Connect(containerId))
                return Errors.ErrorUnexpected.AddMetadata("exception", "no audio devices found");
        }
        catch (Exception e)
        {
            return Errors.ErrorDeviceNotConnected.AddMetadata("exception", e.Message);
        }

        return Errors.ErrorNone;
    }
}

internal static class AudioDevices
{
    internal static unsafe bool Connect(Guid containerId)
    {
        if (
            PInvoke.CoCreateInstance<IMMDeviceEnumerator>(
                typeof(MMDeviceEnumerator).GUID,
                null,
                CLSCTX.CLSCTX_ALL,
                out var enumerator
            ) != 0
        )
            return false;

        var connectedCount = 0;
        IMMDeviceCollection* pDevices = null;

        if (
            enumerator->EnumAudioEndpoints(
                EDataFlow.eAll,
                DEVICE_STATE.DEVICE_STATE_ACTIVE
                    | DEVICE_STATE.DEVICE_STATE_DISABLED
                    | DEVICE_STATE.DEVICE_STATE_UNPLUGGED
                    | DEVICE_STATE.DEVICE_STATE_NOTPRESENT,
                &pDevices
            ) != 0
        )
        {
            enumerator->Release();
            pDevices = null;

            return false;
        }

        try
        {
            if (pDevices->GetCount(out var devicesCount) != 0 || devicesCount <= 0)
                return false;

            for (uint i = 0; i < devicesCount; i++)
            {
                IDeviceTopology* topology = null;
                IPart* part = null;
                IMMDevice* device = null;

                try
                {
                    if (!GetDeviceTopology(pDevices, i, out topology, out device))
                        continue;

                    if (!GetConnectedToPart(topology, out part))
                        continue;

                    if (!GetKsControl(part, enumerator, out var kscontrol))
                        continue;

                    using (var audioDevice = new AudioDevice(device, kscontrol))
                    {
                        if (!audioDevice.Initialize() || audioDevice.ContainerId != containerId)
                            continue;

                        if (audioDevice.Connect())
                            connectedCount++;
                    }
                }
                catch
                {
                    continue;
                }
                finally
                {
                    if (part != null)
                        part->Release();

                    if (topology != null)
                        topology->Release();

                    if (device != null)
                        device->Release();
                }
            }
        }
        finally
        {
            pDevices->Release();
            enumerator->Release();
        }

        return connectedCount > 0;
    }

    private static unsafe bool GetDeviceTopology(
        IMMDeviceCollection* collection,
        uint index,
        out IDeviceTopology* topology,
        out IMMDevice* device
    )
    {
        topology = null;
        device = null;

        IMMDevice* pDevice = null;

        bool deviceSet = false;
        bool hasConnectors = false;

        try
        {
            if (collection->Item(index, &pDevice) != 0)
                return false;

            deviceSet = true;

            topology = ActivateDeviceTopology(pDevice);
            if (topology == null)
                return false;

            if (topology->GetConnectorCount(out var connectorCount) != 0 || connectorCount <= 0)
                return false;

            device = pDevice;
            hasConnectors = true;
        }
        finally
        {
            if (topology == null || !hasConnectors)
            {
                if (deviceSet)
                {
                    pDevice->Release();
                    device = null;
                }

                if (topology != null)
                {
                    topology->Release();
                    topology = null;
                }
            }
        }

        return topology != null;
    }

    private static unsafe bool GetConnectedToPart(IDeviceTopology* topology, out IPart* part)
    {
        part = null;

        IConnector* pConnectFrom = null;
        IConnector* pConTo = null;

        bool connFrom = false;
        bool connTo = false;

        try
        {
            if (topology->GetConnector(0, &pConnectFrom) != 0)
                return false;

            connFrom = true;
            if (pConnectFrom->GetConnectedTo(&pConTo) != 0)
                return false;

            connTo = true;
            if (pConTo->QueryInterface(typeof(IPart).GUID, out var partData) != 0)
                return false;

            part = (IPart*)partData;
        }
        finally
        {
            if (connFrom)
                pConnectFrom->Release();

            if (connTo)
                pConTo->Release();
        }

        return part != null;
    }

    private static unsafe bool GetKsControl(
        IPart* part,
        IMMDeviceEnumerator* enumerator,
        out IKsControl* kscontrol
    )
    {
        kscontrol = null;

        IDeviceTopology* pTopology = null;
        IMMDevice* pDev = null;

        bool topologySet = false;
        bool deviceSet = false;

        try
        {
            if (part->GetTopologyObject(&pTopology) != 0)
                return false;

            topologySet = true;
            if (pTopology->GetDeviceId(out var id) != 0)
                return false;

            var connectorId = new string(id.AsSpan());
            if (!connectorId.StartsWith(@"{2}.\\?\bth"))
                return false;

            if (enumerator->GetDevice(connectorId, &pDev) != 0)
                return false;

            deviceSet = true;
            kscontrol = ActivateKsControl(pDev);
        }
        finally
        {
            if (topologySet)
                pTopology->Release();

            if (deviceSet)
                pDev->Release();
        }

        return kscontrol != null;
    }

    private static unsafe IDeviceTopology* ActivateDeviceTopology(IMMDevice* device)
    {
        device->Activate(typeof(IDeviceTopology).GUID, CLSCTX.CLSCTX_ALL, null, out var itf);
        return (IDeviceTopology*)itf;
    }

    private static unsafe IKsControl* ActivateKsControl(IMMDevice* device)
    {
        device->Activate(typeof(IKsControl).GUID, CLSCTX.CLSCTX_ALL, null, out var itf);
        return (IKsControl*)itf;
    }
}

internal sealed unsafe partial class AudioDevice : IDisposable
{
    private IMMDevice* device;
    private IKsControl* ksControl;

    internal Guid ContainerId;

    private static readonly PROPERTYKEY ContainerIdProperty = new()
    {
        fmtid = new Guid("{8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C}"),
        pid = 2,
    };

    private static readonly Guid KsPropSetId = new("7fa06c40-b8f6-4c7e-8556-e8c33a12e54d");

    internal bool IsConnected
    {
        get
        {
            if (device->GetState(out var state) == 0)
                return state == DEVICE_STATE.DEVICE_STATE_ACTIVE;

            return false;
        }
    }

    internal unsafe AudioDevice(IMMDevice* device, IKsControl* ksControl)
    {
        this.ksControl = ksControl;
        this.device = device;
    }

    internal unsafe bool Initialize()
    {
        if (device == null || ksControl == null)
            return false;

        IPropertyStore* pProperties = null;

        bool propertyStoreSet = false;

        try
        {
            if (device->OpenPropertyStore(STGM.STGM_READ, &pProperties) != 0)
                return false;

            propertyStoreSet = true;

            if (pProperties->GetValue(ContainerIdProperty, out var variant) != 0)
                return false;

            if (PInvoke.PropVariantToGUID(variant, out ContainerId) != 0)
                return false;

            return true;
        }
        finally
        {
            if (propertyStoreSet)
                pProperties->Release();
        }
    }

    internal bool Connect()
    {
        return GetKsProperty(KsPropertyId.KSPROPERTY_ONESHOT_RECONNECT);
    }

    internal bool Disconnect()
    {
        return GetKsProperty(KsPropertyId.KSPROPERTY_ONESHOT_DISCONNECT);
    }

    private bool GetKsProperty(KsPropertyId property)
    {
        var ksIdentifier = new KSIDENTIFIER();
        ksIdentifier.Anonymous.Anonymous.Id = (uint)property;
        ksIdentifier.Anonymous.Anonymous.Set = KsPropSetId;
        ksIdentifier.Anonymous.Anonymous.Flags = (uint)KsPropertyKind.KSPROPERTY_TYPE_GET;

        return ksControl->KsProperty(
                ksIdentifier,
                (uint)Unsafe.SizeOf<KSIDENTIFIER._Anonymous_e__Union._Anonymous_e__Struct>(),
                null,
                0,
                out _
            ) == 0;
    }

    public void Dispose()
    {
        if (ksControl != null)
        {
            ksControl->Release();
            ksControl = null;
        }

        if (device != null)
        {
            device->Release();
            device = null;
        }
    }
}

internal enum KsPropertyKind : uint
{
    KSPROPERTY_TYPE_GET = 0x00000001,
    KSPROPERTY_TYPE_SET = 0x00000002,
    KSPROPERTY_TYPE_TOPOLOGY = 0x10000000,
}

internal enum KsPropertyId : uint
{
    KSPROPERTY_ONESHOT_RECONNECT = 0,
    KSPROPERTY_ONESHOT_DISCONNECT = 1,
}
