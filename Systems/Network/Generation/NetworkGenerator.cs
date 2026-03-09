using System;
using System.Collections.Generic;
using System.Linq;
using Dragon.World;

namespace Dragon.Network.Generation
{
    /// <summary> Procedurally generates a realistic global internet topology. </summary>
    public static class NetworkGenerator
    {
        /// <summary> Multiplier for the number of Tier 2 networks generated per region. </summary>
        private const Int32 REGIONAL_SCALE = 10;

        /// <summary> Multiplier for the number of Tier 3 networks generated per region. </summary>
        private const Int32 LOCAL_SCALE = 20;

        /// <summary> Maximum degrees of latitude/longitude jitter applied to generated locations. </summary>
        private const Single LOCATION_JITTER = 3.0f;

        /// <summary> Probability that a Tier 2 network gets a redundant second backbone link. </summary>
        private const Single REDUNDANT_LINK_CHANCE = 0.20f;

        /// <summary> Number of random cross-region shortcut links to generate. </summary>
        private const Int32 SHORTCUT_COUNT = 8;


        /// <summary> Generate a complete network topology from a seed. </summary>
        /// <param name="data"> The loaded generation data (regions, hub cities, cable routes). </param>
        /// <param name="seed"> The random seed for deterministic generation. </param>
        /// <param name="reservedIds"> Network IDs already in use (from story CSVs). </param>
        /// <returns> The generated networks, devices, and links. </returns>
        public static NetworkGenerationResult Generate(NetworkGenerationData data, Int32 seed, IReadOnlySet<String>? reservedIds = null)
        {
            Random rng = new(seed);
            NetworkGenerationResult result = new();
            HashSet<String> usedIds = reservedIds != null ? new(reservedIds) : new();

            // Index of region code -> list of generated network IDs by tier.
            Dictionary<String, List<String>> tier1ByRegion = new();
            Dictionary<String, List<String>> tier2ByRegion = new();
            Dictionary<String, List<String>> tier3ByRegion = new();

            // Map of network ID -> network for nearest-hub lookups.
            Dictionary<String, Network> networkIndex = new();

            foreach (NetworkGenerationData.RegionEntry region in data.Regions)
            {
                tier1ByRegion[region.Code] = new();
                tier2ByRegion[region.Code] = new();
                tier3ByRegion[region.Code] = new();
            }

            // --- Tier 1: Backbone hubs ---
            foreach (NetworkGenerationData.HubCityEntry hub in data.HubCities)
            {
                if (hub.Tier != 1)
                {
                    continue;
                }

                String id = GenerateId("HUB", hub.CityName, usedIds);
                String name = String.Format(
                    NetworkGenerationData.Tier1NameTemplates[rng.Next(NetworkGenerationData.Tier1NameTemplates.Length)],
                    hub.CityName);

                Location location = new(CelestialBody.Earth, hub.Latitude, hub.Longitude);
                Network network = new(id, name, location, NetworkAccessibility.Public);
                result.Networks.Add(network);
                networkIndex[id] = network;
                tier1ByRegion[hub.RegionCode].Add(id);

                AddDevice(result, id, "RTR-001", DeviceType.Router, $"{hub.CityName} Core Router");
                AddDevice(result, id, "SRV-001", DeviceType.Server, $"{hub.CityName} Root Server");
            }

            // --- Backbone mesh: cable routes + intra-region Tier 1 links ---
            foreach (NetworkGenerationData.CableRouteEntry cable in data.CableRoutes)
            {
                ConnectTiersBetweenRegions(result, tier1ByRegion, cable.FromRegionCode, cable.ToRegionCode, cable.Latency);
            }

            // Intra-region Tier 1 mesh (connect all T1 hubs within same region).
            foreach (KeyValuePair<String, List<String>> entry in tier1ByRegion)
            {
                List<String> hubs = entry.Value;
                for (Int32 i = 0; i < hubs.Count; i++)
                {
                    for (Int32 j = i + 1; j < hubs.Count; j++)
                    {
                        Single latency = CalculateLatency(networkIndex[hubs[i]].Location, networkIndex[hubs[j]].Location);
                        result.Links.Add((hubs[i], new NetworkLink(hubs[j], LinkType.Standard, latency)));
                    }
                }
            }

            // --- Tier 2: Regional networks ---
            foreach (NetworkGenerationData.RegionEntry region in data.Regions)
            {
                NetworkGenerationData.HubCityEntry[] regionHubs = data.HubCities.Where(h => h.RegionCode == region.Code).ToArray();
                if (regionHubs.Length == 0)
                {
                    continue;
                }

                Int32 count = Math.Max(1, (Int32)MathF.Floor(region.InternetWeight * REGIONAL_SCALE));

                for (Int32 i = 0; i < count; i++)
                {
                    NetworkGenerationData.HubCityEntry nearestHub = regionHubs[rng.Next(regionHubs.Length)];
                    Boolean isPrivate = rng.NextSingle() < region.PrivateNetworkRatio;
                    NetworkAccessibility accessibility = isPrivate ? NetworkAccessibility.Private : NetworkAccessibility.Public;

                    String id = GenerateId("REG", $"{region.Code}-{i}", usedIds);
                    String[] templates = isPrivate ? NetworkGenerationData.Tier2PrivateNameTemplates : NetworkGenerationData.Tier2PublicNameTemplates;
                    String name = String.Format(templates[rng.Next(templates.Length)], nearestHub.CityName);

                    Location location = JitterLocation(rng, nearestHub.Latitude, nearestHub.Longitude, LOCATION_JITTER);
                    Network network = new(id, name, location, accessibility);
                    result.Networks.Add(network);
                    networkIndex[id] = network;
                    tier2ByRegion[region.Code].Add(id);

                    // Devices: 1-3.
                    Int32 deviceCount = rng.Next(1, 4);
                    AddDevice(result, id, "RTR-001", DeviceType.Router, $"{name} Router");
                    if (deviceCount >= 2)
                    {
                        AddDevice(result, id, "SRV-001", DeviceType.Server, $"{name} Server");
                    }
                    if (deviceCount >= 3)
                    {
                        DeviceType extraType = rng.Next(2) == 0 ? DeviceType.Firewall : DeviceType.Server;
                        AddDevice(result, id, "DEV-001", extraType, $"{name} {extraType}");
                    }

                    // Connect to nearest Tier 1 hub in this region (or any T1 if none in region).
                    String? nearestT1 = FindNearestNetwork(networkIndex, tier1ByRegion[region.Code], location);
                    if (nearestT1 == null)
                    {
                        nearestT1 = FindNearestNetwork(networkIndex, tier1ByRegion.Values.SelectMany(x => x).ToList(), location);
                    }

                    if (nearestT1 != null)
                    {
                        Single latency = CalculateLatency(location, networkIndex[nearestT1].Location) + 1f;
                        result.Links.Add((id, new NetworkLink(nearestT1, LinkType.Standard, latency)));

                        // Redundant second link for some networks.
                        if (rng.NextSingle() < REDUNDANT_LINK_CHANCE)
                        {
                            List<String> allT1 = tier1ByRegion.Values.SelectMany(x => x).Where(x => x != nearestT1).ToList();
                            String? secondT1 = FindNearestNetwork(networkIndex, allT1, location);
                            if (secondT1 != null)
                            {
                                Single latency2 = CalculateLatency(location, networkIndex[secondT1].Location) + 2f;
                                result.Links.Add((id, new NetworkLink(secondT1, LinkType.Standard, latency2)));
                            }
                        }
                    }
                }
            }

            // --- Tier 3: Local/edge networks ---
            foreach (NetworkGenerationData.RegionEntry region in data.Regions)
            {
                List<String> regionT2 = tier2ByRegion[region.Code];
                if (regionT2.Count == 0)
                {
                    continue;
                }

                NetworkGenerationData.HubCityEntry[] regionHubs = data.HubCities.Where(h => h.RegionCode == region.Code).ToArray();
                Int32 count = Math.Max(1, (Int32)MathF.Floor(region.InternetWeight * LOCAL_SCALE));

                for (Int32 i = 0; i < count; i++)
                {
                    // Higher private ratio for Tier 3.
                    Single adjustedPrivateRatio = Math.Min(1f, region.PrivateNetworkRatio + 0.2f);
                    Boolean isPrivate = rng.NextSingle() < adjustedPrivateRatio;
                    NetworkAccessibility accessibility = isPrivate ? NetworkAccessibility.Private : NetworkAccessibility.Public;

                    String hubCityName = regionHubs.Length > 0
                        ? regionHubs[rng.Next(regionHubs.Length)].CityName
                        : region.Name;

                    String id = GenerateId("LOC", $"{region.Code}-{i}", usedIds);
                    String[] templates = isPrivate ? NetworkGenerationData.Tier3PrivateNameTemplates : NetworkGenerationData.Tier3PublicNameTemplates;
                    String name = String.Format(templates[rng.Next(templates.Length)], hubCityName);

                    // Jitter around a random Tier 2 network in the region.
                    Network parentT2 = networkIndex[regionT2[rng.Next(regionT2.Count)]];
                    Location location = JitterLocation(rng, parentT2.Location.Latitude, parentT2.Location.Longitude, LOCATION_JITTER * 0.5f);
                    Network network = new(id, name, location, accessibility);
                    result.Networks.Add(network);
                    networkIndex[id] = network;
                    tier3ByRegion[region.Code].Add(id);

                    // Devices: 1-5.
                    Int32 deviceCount = rng.Next(1, 6);
                    DeviceType[] edgeTypes = { DeviceType.Workstation, DeviceType.Terminal, DeviceType.IoTDevice, DeviceType.Server };
                    for (Int32 d = 0; d < deviceCount; d++)
                    {
                        DeviceType type = d == 0 ? DeviceType.Router : edgeTypes[rng.Next(edgeTypes.Length)];
                        String deviceId = $"DEV-{d + 1:D3}";
                        AddDevice(result, id, deviceId, type, $"{name} {type} {d + 1}");
                    }

                    // Connect to nearest Tier 2 in region (leaf node — single link).
                    String? nearestT2 = FindNearestNetwork(networkIndex, regionT2, location);
                    if (nearestT2 != null)
                    {
                        Single latency = CalculateLatency(location, networkIndex[nearestT2].Location) + 0.5f;
                        result.Links.Add((id, new NetworkLink(nearestT2, LinkType.Standard, latency)));
                    }
                }
            }

            // --- Off-world networks ---
            GenerateOffWorldNetworks(rng, result, networkIndex, usedIds, tier1ByRegion);

            // --- Shortcuts: cross-region Tier 2 links ---
            List<String> allT2Ids = tier2ByRegion.Values.SelectMany(x => x).ToList();
            for (Int32 i = 0; i < SHORTCUT_COUNT && allT2Ids.Count >= 2; i++)
            {
                Int32 indexA = rng.Next(allT2Ids.Count);
                Int32 indexB = rng.Next(allT2Ids.Count);
                if (indexA == indexB)
                {
                    continue;
                }

                String a = allT2Ids[indexA];
                String b = allT2Ids[indexB];
                Single latency = CalculateLatency(networkIndex[a].Location, networkIndex[b].Location) * 0.7f;
                result.Links.Add((a, new NetworkLink(b, LinkType.Standard, Math.Max(2f, latency))));
            }

            return result;
        }


        /// <summary> Generate off-world networks for orbital stations, the Moon, and Mars. </summary>
        /// <param name="rng"> The seeded random number generator. </param>
        /// <param name="result"> The generation result to populate. </param>
        /// <param name="networkIndex"> Index of all generated networks by ID. </param>
        /// <param name="usedIds"> Set of network IDs already in use. </param>
        /// <param name="tier1ByRegion"> Tier 1 backbone hub IDs grouped by region. </param>
        private static void GenerateOffWorldNetworks(
            Random rng,
            NetworkGenerationResult result,
            Dictionary<String, Network> networkIndex,
            HashSet<String> usedIds,
            Dictionary<String, List<String>> tier1ByRegion)
        {
            (CelestialBody body, Int32 count, Single relayLatency)[] offWorldBodies =
            {
                (CelestialBody.OrbitalStation, 4, 50f),
                (CelestialBody.Moon, 3, 100f),
                (CelestialBody.Mars, 3, 500f),
            };

            // Find a well-connected Earth hub for relay attachment.
            List<String> earthHubs = tier1ByRegion.Values.SelectMany(x => x).ToList();

            foreach ((CelestialBody body, Int32 count, Single relayLatency) in offWorldBodies)
            {
                String bodyName = body.ToString();

                for (Int32 i = 0; i < count; i++)
                {
                    Boolean isPrivate = rng.NextSingle() < 0.7f;
                    NetworkAccessibility accessibility = isPrivate ? NetworkAccessibility.Private : NetworkAccessibility.Public;

                    String id = GenerateId("OW", $"{bodyName}-{i}", usedIds);
                    String name = String.Format(
                        NetworkGenerationData.OffWorldNameTemplates[rng.Next(NetworkGenerationData.OffWorldNameTemplates.Length)],
                        bodyName);

                    // Random location on the body surface.
                    Single lat = (rng.NextSingle() - 0.5f) * 180f;
                    Single lon = (rng.NextSingle() - 0.5f) * 360f;
                    Location location = new(body, lat, lon);

                    Network network = new(id, name, location, accessibility);
                    result.Networks.Add(network);
                    networkIndex[id] = network;

                    // Devices: 2-4.
                    Int32 deviceCount = rng.Next(2, 5);
                    for (Int32 d = 0; d < deviceCount; d++)
                    {
                        DeviceType type = d == 0 ? DeviceType.Router : (d == 1 ? DeviceType.Server : DeviceType.Terminal);
                        String deviceId = $"DEV-{d + 1:D3}";
                        AddDevice(result, id, deviceId, type, $"{name} {type} {d + 1}");
                    }

                    // Connect to a random Earth backbone hub via relay.
                    if (earthHubs.Count > 0)
                    {
                        String earthHub = earthHubs[rng.Next(earthHubs.Count)];
                        result.Links.Add((id, new NetworkLink(earthHub, LinkType.Relay, relayLatency)));
                    }
                }

                // Connect off-world networks of the same body to each other.
                List<String> bodyNetworks = result.Networks
                    .Where(n => n.Location.Body == body)
                    .Select(n => n.Id)
                    .ToList();

                for (Int32 i = 0; i < bodyNetworks.Count; i++)
                {
                    for (Int32 j = i + 1; j < bodyNetworks.Count; j++)
                    {
                        result.Links.Add((bodyNetworks[i], new NetworkLink(bodyNetworks[j], LinkType.Standard, 2f)));
                    }
                }
            }
        }


        /// <summary> Create a backbone link between the first Tier 1 hub in each of two regions. </summary>
        /// <param name="result"> The generation result to populate. </param>
        /// <param name="tier1ByRegion"> Tier 1 backbone hub IDs grouped by region. </param>
        /// <param name="regionA"> The source region code. </param>
        /// <param name="regionB"> The destination region code. </param>
        /// <param name="latency"> The latency cost for this corridor. </param>
        private static void ConnectTiersBetweenRegions(
            NetworkGenerationResult result,
            Dictionary<String, List<String>> tier1ByRegion,
            String regionA,
            String regionB,
            Single latency)
        {
            if (!tier1ByRegion.TryGetValue(regionA, out List<String>? hubsA) || hubsA.Count == 0)
            {
                return;
            }
            if (!tier1ByRegion.TryGetValue(regionB, out List<String>? hubsB) || hubsB.Count == 0)
            {
                return;
            }

            // Connect first hub in each region (deterministic).
            result.Links.Add((hubsA[0], new NetworkLink(hubsB[0], LinkType.Standard, latency)));
        }


        /// <summary> Find the geographically nearest network from a list of candidates. </summary>
        /// <param name="index"> Index of all networks by ID. </param>
        /// <param name="candidates"> The candidate network IDs to search. </param>
        /// <param name="from"> The location to measure distance from. </param>
        /// <returns> The ID of the nearest network, or null if candidates is empty. </returns>
        private static String? FindNearestNetwork(
            Dictionary<String, Network> index,
            IList<String> candidates,
            Location from)
        {
            if (candidates.Count == 0)
            {
                return null;
            }

            String? nearest = null;
            Single bestDistance = Single.MaxValue;

            foreach (String id in candidates)
            {
                Single distance = ApproximateDistance(from, index[id].Location);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = id;
                }
            }

            return nearest;
        }


        /// <summary> Calculate a gameplay latency value between two locations based on distance. </summary>
        /// <param name="a"> The first location. </param>
        /// <param name="b"> The second location. </param>
        /// <returns> A latency value clamped between 1 and 40. </returns>
        private static Single CalculateLatency(Location a, Location b)
        {
            Single distance = ApproximateDistance(a, b);
            // Scale: ~1 latency per degree of distance, clamped.
            return Math.Clamp(distance * 0.3f, 1f, 40f);
        }


        /// <summary> Approximate the distance between two locations using equirectangular projection. </summary>
        /// <param name="a"> The first location. </param>
        /// <param name="b"> The second location. </param>
        /// <returns> An approximate distance in degrees. </returns>
        private static Single ApproximateDistance(Location a, Location b)
        {
            // Simple equirectangular approximation — sufficient for gameplay.
            Single avgLatRad = (a.Latitude + b.Latitude) * 0.5f * MathF.PI / 180f;
            Single dx = (b.Longitude - a.Longitude) * MathF.Cos(avgLatRad);
            Single dy = b.Latitude - a.Latitude;
            return MathF.Sqrt(dx * dx + dy * dy);
        }


        /// <summary> Create a new Earth location with random offset from the given coordinates. </summary>
        /// <param name="rng"> The seeded random number generator. </param>
        /// <param name="lat"> The base latitude. </param>
        /// <param name="lon"> The base longitude. </param>
        /// <param name="jitter"> The maximum offset in degrees. </param>
        /// <returns> A jittered location clamped to valid coordinate ranges. </returns>
        private static Location JitterLocation(Random rng, Single lat, Single lon, Single jitter)
        {
            Single jitteredLat = Math.Clamp(lat + (rng.NextSingle() - 0.5f) * 2f * jitter, -90f, 90f);
            Single jitteredLon = Math.Clamp(lon + (rng.NextSingle() - 0.5f) * 2f * jitter, -180f, 180f);
            return new Location(CelestialBody.Earth, jitteredLat, jitteredLon);
        }


        /// <summary> Generate a unique network ID with the given prefix and hint, avoiding collisions. </summary>
        /// <param name="prefix"> The ID category prefix (e.g. HUB, REG, LOC, OW). </param>
        /// <param name="hint"> A descriptive hint incorporated into the ID. </param>
        /// <param name="usedIds"> Set of already-used IDs; the new ID is added to this set. </param>
        /// <returns> A unique ID in the format GEN-{prefix}-{hint}. </returns>
        private static String GenerateId(String prefix, String hint, HashSet<String> usedIds)
        {
            // Sanitise hint: uppercase, replace spaces with hyphens.
            String sanitised = hint.ToUpperInvariant().Replace(' ', '-');
            String id = $"GEN-{prefix}-{sanitised}";

            if (usedIds.Add(id))
            {
                return id;
            }

            // Collision — append a numeric suffix.
            Int32 suffix = 2;
            while (!usedIds.Add($"{id}-{suffix}"))
            {
                suffix++;
            }

            return $"{id}-{suffix}";
        }


        /// <summary> Create a device and add it to the generation result. </summary>
        /// <param name="result"> The generation result to populate. </param>
        /// <param name="networkId"> The network this device belongs to. </param>
        /// <param name="deviceId"> The local device identifier. </param>
        /// <param name="type"> The type of device. </param>
        /// <param name="name"> The device's display name. </param>
        private static void AddDevice(
            NetworkGenerationResult result,
            String networkId,
            String deviceId,
            DeviceType type,
            String name)
        {
            Device device = new(new NetworkAddress(networkId, deviceId), type, name);
            result.Devices.Add(device);
        }
    }
}
