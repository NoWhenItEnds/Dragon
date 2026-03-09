using System;
using System.Collections.Generic;
using System.Linq;
using Dragon.Network.Generation;
using Dragon.Utilities.Extensions;
using Dragon.Utilities.Singletons;
using Dragon.World;
using Godot;
using static Dragon.Utilities.Extensions.CsvExtensions;

namespace Dragon.Network
{
    /// <summary> Manages the global network topology, device registry, and routing. </summary>
    public partial class NetworkManager : SingletonNode<NetworkManager>
    {
        /// <summary> Godot resource path to the networks CSV file. </summary>
        private const String NETWORKS_PATH = "res://Data/Networks/networks.csv";

        /// <summary> Godot resource path to the devices CSV file. </summary>
        private const String DEVICES_PATH = "res://Data/Networks/devices.csv";

        /// <summary> Godot resource path to the links CSV file. </summary>
        private const String LINKS_PATH = "res://Data/Networks/links.csv";


        /// <summary> The random seed used for procedural generation. </summary>
        [Export] public Int32 Seed { get; set; } = 42;


        /// <summary> All registered networks keyed by their unique ID. </summary>
        private readonly Dictionary<String, Network> _networks = new();

        /// <summary> All registered devices keyed by their network address. </summary>
        private readonly Dictionary<NetworkAddress, Device> _devices = new();


        /// <inheritdoc/>
        public override void _Ready()
        {
            LoadNetworks();
            LoadDevices();
            LoadLinks();
            GenerateWorld();
        }


        /// <summary> Look up a network by its ID. </summary>
        /// <param name="networkId"> The unique network identifier. </param>
        /// <returns> The network, or null if not found. </returns>
        public Network? GetNetwork(String networkId)
        {
            return _networks.GetValueOrDefault(networkId);
        }


        /// <summary> Look up a device by its full address. </summary>
        /// <param name="address"> The device's network address. </param>
        /// <returns> The device, or null if not found. </returns>
        public Device? GetDevice(NetworkAddress address)
        {
            return _devices.GetValueOrDefault(address);
        }


        /// <summary> Get all registered networks. </summary>
        /// <returns> A read-only view of all networks keyed by ID. </returns>
        public IReadOnlyDictionary<String, Network> GetAllNetworks()
        {
            return _networks;
        }


        /// <summary> Get all networks on a given celestial body. </summary>
        /// <param name="body"> The celestial body to filter by. </param>
        /// <returns> All networks located on that body. </returns>
        public IEnumerable<Network> GetNetworksByBody(CelestialBody body)
        {
            return _networks.Values.Where(n => n.Location.Body == body);
        }


        /// <summary> Find the lowest-latency route between two devices. </summary>
        /// <param name="from"> The source device address. </param>
        /// <param name="to"> The destination device address. </param>
        /// <param name="accessiblePrivateNetworks"> Optional set of private network IDs that may be transited. </param>
        /// <returns> The resolved route, or null if no path exists. </returns>
        public NetworkRoute? FindRoute(
            NetworkAddress from,
            NetworkAddress to,
            IReadOnlySet<String>? accessiblePrivateNetworks = null)
        {
            return NetworkRouter.FindRoute(_networks, from, to, accessiblePrivateNetworks);
        }


        /// <summary> Load story networks from the networks CSV file. </summary>
        private void LoadNetworks()
        {
            Network[] networks = CsvExtensions.LoadData<Network>(NETWORKS_PATH);
            foreach (Network network in networks)
            {
                _networks[network.Id] = network;
            }

            GD.Print($"NetworkManager: Loaded {networks.Length} networks.");
        }


        /// <summary> Load story devices from the devices CSV file and attach them to their networks. </summary>
        private void LoadDevices()
        {
            Device[] devices = CsvExtensions.LoadData<Device>(DEVICES_PATH);
            foreach (Device device in devices)
            {
                _devices[device.Address] = device;

                if (_networks.TryGetValue(device.Address.NetworkId, out Network? network))
                {
                    network.AddDevice(device);
                }
                else
                {
                    GD.PrintErr($"NetworkManager: Device {device.Address} references unknown network '{device.Address.NetworkId}'.");
                }
            }

            GD.Print($"NetworkManager: Loaded {devices.Length} devices.");
        }


        /// <summary> Load story links from the links CSV file and create bidirectional connections. </summary>
        private void LoadLinks()
        {
            // Links CSV has SourceNetworkId and TargetNetworkId columns.
            // We parse the raw CSV ourselves to get the source ID, then create bidirectional links.
            using FileAccess? file = FileAccess.Open(LINKS_PATH, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GD.PrintErr(FileAccess.GetOpenError());
                return;
            }

            String[] header = file.GetCsvLine();
            Int32 linkCount = 0;

            while (!file.EofReached())
            {
                String[] line = file.GetCsvLine();
                if (line.Length == 0 || String.IsNullOrWhiteSpace(line[0]))
                {
                    continue;
                }

                String sourceNetworkId = String.Empty;
                String targetNetworkId = String.Empty;
                LinkType type = LinkType.Standard;
                Single latency = 1f;

                for (Int32 i = 0; i < header.Length; i++)
                {
                    switch (header[i])
                    {
                        case "SourceNetworkId":
                            sourceNetworkId = line[i];
                            break;
                        case "TargetNetworkId":
                            targetNetworkId = line[i];
                            break;
                        case "Type":
                            type = Enum.Parse<LinkType>(line[i]);
                            break;
                        case "Latency":
                            latency = Single.Parse(line[i]);
                            break;
                    }
                }

                // Create bidirectional links.
                if (_networks.TryGetValue(sourceNetworkId, out Network? source))
                {
                    source.AddLink(new NetworkLink(targetNetworkId, type, latency));
                }
                else
                {
                    GD.PrintErr($"NetworkManager: Link references unknown source network '{sourceNetworkId}'.");
                }

                if (_networks.TryGetValue(targetNetworkId, out Network? target))
                {
                    target.AddLink(new NetworkLink(sourceNetworkId, type, latency));
                }
                else
                {
                    GD.PrintErr($"NetworkManager: Link references unknown target network '{targetNetworkId}'.");
                }

                linkCount++;
            }

            GD.Print($"NetworkManager: Loaded {linkCount} links (bidirectional).");
        }


        /// <summary> Procedurally generate the global network topology and merge it into the registry. </summary>
        private void GenerateWorld()
        {
            NetworkGenerationData? data = NetworkGenerationData.Load();
            if (data == null)
            {
                GD.PrintErr("NetworkManager: Generation data failed validation. Skipping world generation.");
                return;
            }

            HashSet<String> reservedIds = new(_networks.Keys);
            NetworkGenerationResult generated = NetworkGenerator.Generate(data, Seed, reservedIds);

            foreach (Network network in generated.Networks)
            {
                _networks[network.Id] = network;
            }

            foreach (Device device in generated.Devices)
            {
                _devices[device.Address] = device;
                if (_networks.TryGetValue(device.Address.NetworkId, out Network? network))
                {
                    network.AddDevice(device);
                }
            }

            foreach ((String sourceId, NetworkLink link) in generated.Links)
            {
                if (_networks.TryGetValue(sourceId, out Network? source))
                {
                    source.AddLink(link);
                }
                if (_networks.TryGetValue(link.TargetNetworkId, out Network? target))
                {
                    target.AddLink(new NetworkLink(sourceId, link.Type, link.Latency));
                }
            }

            GD.Print($"NetworkManager: Generated {generated.Networks.Count} networks, {generated.Devices.Count} devices, {generated.Links.Count} links.");
        }
    }
}
