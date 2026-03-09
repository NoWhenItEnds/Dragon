using System;
using static Dragon.Utilities.Extensions.CsvExtensions;

namespace Dragon.Network
{
    /// <summary> A single device on a network. </summary>
    public sealed class Device : IParseable<Device>
    {
        /// <summary> The device's unique network address. </summary>
        public NetworkAddress Address { get; }

        /// <summary> The type of this device. </summary>
        public DeviceType Type { get; }

        /// <summary> The device's display name. </summary>
        public String Name { get; }


        /// <summary> Creates a new device. </summary>
        /// <param name="address"> The device's unique network address. </param>
        /// <param name="type"> The type of this device. </param>
        /// <param name="name"> The device's display name. </param>
        public Device(NetworkAddress address, DeviceType type, String name)
        {
            Address = address;
            Type = type;
            Name = name;
        }


        /// <inheritdoc/>
        public static Device Parse(String[] header, String[] data)
        {
            String networkId = String.Empty;
            String deviceId = String.Empty;
            String name = String.Empty;
            DeviceType type = DeviceType.Workstation;

            for (Int32 i = 0; i < header.Length; i++)
            {
                switch (header[i])
                {
                    case "NetworkId":
                        networkId = data[i];
                        break;
                    case "DeviceId":
                        deviceId = data[i];
                        break;
                    case "Name":
                        name = data[i];
                        break;
                    case "Type":
                        type = Enum.Parse<DeviceType>(data[i]);
                        break;
                }
            }

            return new Device(new NetworkAddress(networkId, deviceId), type, name);
        }
    }
}
