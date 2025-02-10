using Nefarius.Utilities.DeviceManagement.PnP;

namespace Bluetuith.Shim.Stack.Providers.MSFT;

public static class PnPInformation
{
    public static Guid BluetoothRadiosInterfaceGuid
    {
        get => Guid.Parse("{92383b0e-f90e-4ac9-8d44-8c2d0d0ebda2}");
    }

    public static Guid BluetoothDevicesInterfaceGuid
    {
        get => Guid.Parse("{00f40965-e89d-4487-9890-87c3abb211f4}");
    }

    public static Guid HFEnumeratorInterfaceGuid
    {
        get => Guid.Parse("{bd41df2d-addd-4fc9-a194-b9881d2a2efa}");
    }

    public static string HFEnumeratorRegistryKey
    {
        get => $@"{DeviceClassesRegistryPath}\{{{HFEnumeratorInterfaceGuid}}}";
    }

    public static string DeviceClassesRegistryPath
    {
        get => @"SYSTEM\CurrentControlSet\Control\DeviceClasses";
    }

    public static class Adapter
    {
        public static string ServicesRegistryPath
        {
            get => $@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Services";
        }

        public static string DiscoverableRegistryKey
        {
            get => "Write Scan Enable";
        }

        public static string RadioStateRegistryKey
        {
            get => "RadioState";
        }

        public static string DeviceParametersRegistryPath(string deviceId)
        {
            return $@"SYSTEM\CurrentControlSet\Enum\{deviceId}\Device Parameters";
        }

        public static DevicePropertyKey Address
        {
            get =>
                CustomDeviceProperty.CreateCustomDeviceProperty(
                    Guid.Parse("{A92F26CA-EDA7-4B1D-9DB2-27B68AA5A2EB}"),
                    1,
                    typeof(ulong)
                );
        }
    }

    public static class Device
    {
        public static Guid DEVPKEY_BluetoothPropertiesGuid
        {
            get => Guid.Parse("{2bd67d8b-8beb-48d5-87e0-6cda3428040a}");
        }

        public static DevicePropertyKey Name
        {
            get => DevicePropertyKey.NAME;
        }

        public static DevicePropertyKey AepId
        {
            get =>
                CustomDeviceProperty.CreateCustomDeviceProperty(
                    Guid.Parse("{3B2CE006-5E61-4FDE-BAB8-9B8AAC9B26DF}"),
                    8,
                    typeof(string)
                );
        }

        public static DevicePropertyKey Class
        {
            get =>
                CustomDeviceProperty.CreateCustomDeviceProperty(
                    DEVPKEY_BluetoothPropertiesGuid,
                    10,
                    typeof(uint)
                );
        }

        public static DevicePropertyKey IsConnected
        {
            get =>
                CustomDeviceProperty.CreateCustomDeviceProperty(
                    Guid.Parse("{83DA6326-97A6-4088-9453-A1923F573B29}"),
                    15,
                    typeof(bool)
                );
        }

        public static DevicePropertyKey BatteryPercentage
        {
            get =>
                CustomDeviceProperty.CreateCustomDeviceProperty(
                    Guid.Parse("{104EA319-6EE2-4701-BD47-8DDBF425BBE5}"),
                    2,
                    typeof(byte)
                );
        }

        public static string ServicesRegistryPath(string adapterAddress, string deviceAddress)
        {
            adapterAddress = adapterAddress.Replace(":", "");
            deviceAddress = deviceAddress.Replace(":", "");

            return $@"SYSTEM\CurrentControlSet\Services\BTHPORT\Parameters\Devices\{deviceAddress}\ServicesFor{adapterAddress}";
        }
    }
}

public static class PnPUtils
{
    public static bool GetAddressFromInterfaceId(string interfaceId, out string address)
    {
        address = "";

        try
        {
            var addr = interfaceId.Substring(interfaceId.LastIndexOf('&') + 1).Substring(0, 12);
            if (addr.Length != 12)
                return false;

            address = string.Join(
                ":",
                Enumerable.Range(0, 6).Select(i => addr.Substring(i * 2, 2).ToLower()).ToArray()
            );
        }
        catch
        {
            return false;
        }

        return true;
    }
}
