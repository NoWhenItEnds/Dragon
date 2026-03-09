using System;
using System.Collections.Generic;
using Dragon.World;
using static Dragon.Utilities.Extensions.CsvExtensions;

namespace Dragon.Network
{
    /// <summary> A local network containing devices and connected to other networks. </summary>
    public sealed class Network : IParseable<Network>
    {
        /// <summary> The unique identifier for this network. </summary>
        public String Id { get; }

        /// <summary> Human-readable name for display. </summary>
        public String Name { get; }

        /// <summary> The physical location of this network. </summary>
        public Location Location { get; }

        /// <summary> Whether this network is publicly routable. </summary>
        public NetworkAccessibility Accessibility { get; }

        /// <summary> The devices on this network. </summary>
        public IReadOnlyList<Device> Devices => _devices;

        /// <summary> Links to other networks. </summary>
        public IReadOnlyList<NetworkLink> Links => _links;


        /// <summary> Backing list for devices on this network. </summary>
        private readonly List<Device> _devices = new();

        /// <summary> Backing list for links to other networks. </summary>
        private readonly List<NetworkLink> _links = new();


        /// <summary> Creates a new network. </summary>
        /// <param name="id"> The unique identifier for this network. </param>
        /// <param name="name"> Human-readable name. </param>
        /// <param name="location"> The physical location of this network. </param>
        /// <param name="accessibility"> Whether this network is publicly routable. </param>
        public Network(String id, String name, Location location, NetworkAccessibility accessibility)
        {
            Id = id;
            Name = name;
            Location = location;
            Accessibility = accessibility;
        }


        /// <summary> Add a device to this network. </summary>
        /// <param name="device"> The device to add. </param>
        public void AddDevice(Device device)
        {
            _devices.Add(device);
        }


        /// <summary> Add a link to another network. </summary>
        /// <param name="link"> The link to add. </param>
        public void AddLink(NetworkLink link)
        {
            _links.Add(link);
        }


        /// <inheritdoc/>
        public static Network Parse(String[] header, String[] data)
        {
            String id = String.Empty;
            String name = String.Empty;
            CelestialBody body = CelestialBody.Earth;
            Single latitude = 0f;
            Single longitude = 0f;
            NetworkAccessibility accessibility = NetworkAccessibility.Public;

            for (Int32 i = 0; i < header.Length; i++)
            {
                switch (header[i])
                {
                    case "Id":
                        id = data[i];
                        break;
                    case "Name":
                        name = data[i];
                        break;
                    case "Body":
                        body = Enum.Parse<CelestialBody>(data[i]);
                        break;
                    case "Latitude":
                        latitude = Single.Parse(data[i]);
                        break;
                    case "Longitude":
                        longitude = Single.Parse(data[i]);
                        break;
                    case "Accessibility":
                        accessibility = Enum.Parse<NetworkAccessibility>(data[i]);
                        break;
                }
            }

            return new Network(id, name, new Location(body, latitude, longitude), accessibility);
        }
    }
}
