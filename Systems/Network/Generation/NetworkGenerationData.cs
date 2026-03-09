using System;
using System.Collections.Generic;
using System.Linq;
using Dragon.Utilities.Extensions;
using Godot;
using static Dragon.Utilities.Extensions.CsvExtensions;

namespace Dragon.Network.Generation
{
    /// <summary> Loaded generation data representing real-world internet infrastructure distribution. </summary>
    public sealed class NetworkGenerationData
    {
        /// <summary> Godot resource path to the regions CSV file. </summary>
        private const String REGIONS_PATH = "res://Data/Generation/regions.csv";

        /// <summary> Godot resource path to the hub cities CSV file. </summary>
        private const String HUB_CITIES_PATH = "res://Data/Generation/hub_cities.csv";

        /// <summary> Godot resource path to the cable routes CSV file. </summary>
        private const String CABLE_ROUTES_PATH = "res://Data/Generation/cable_routes.csv";


        /// <summary> All loaded regions. </summary>
        public RegionEntry[] Regions { get; }

        /// <summary> All loaded hub cities. </summary>
        public HubCityEntry[] HubCities { get; }

        /// <summary> All loaded cable routes. </summary>
        public CableRouteEntry[] CableRoutes { get; }


        /// <summary> Creates a new instance with the given pre-loaded data. </summary>
        /// <param name="regions"> The loaded regions. </param>
        /// <param name="hubCities"> The loaded hub cities. </param>
        /// <param name="cableRoutes"> The loaded cable routes. </param>
        private NetworkGenerationData(RegionEntry[] regions, HubCityEntry[] hubCities, CableRouteEntry[] cableRoutes)
        {
            Regions = regions;
            HubCities = hubCities;
            CableRoutes = cableRoutes;
        }


        /// <summary> Load generation data from CSV files and validate it. </summary>
        /// <returns> The loaded and validated generation data, or null if validation failed. </returns>
        public static NetworkGenerationData? Load()
        {
            RegionEntry[] regions = CsvExtensions.LoadData<RegionEntry>(REGIONS_PATH);
            HubCityEntry[] hubCities = CsvExtensions.LoadData<HubCityEntry>(HUB_CITIES_PATH);
            CableRouteEntry[] cableRoutes = CsvExtensions.LoadData<CableRouteEntry>(CABLE_ROUTES_PATH);

            NetworkGenerationData data = new(regions, hubCities, cableRoutes);
            return data.Validate() ? data : null;
        }


        /// <summary> Validate all loaded data for consistency and correctness. </summary>
        /// <returns> True if validation passed, false if errors were found. </returns>
        private Boolean Validate()
        {
            Boolean valid = true;
            HashSet<String> regionCodes = new();

            // Validate regions.
            foreach (RegionEntry region in Regions)
            {
                if (!regionCodes.Add(region.Code))
                {
                    GD.PrintErr($"NetworkGenerationData: Duplicate region code '{region.Code}'.");
                    valid = false;
                }

                if (region.Latitude < -90f || region.Latitude > 90f)
                {
                    GD.PrintErr($"NetworkGenerationData: Region '{region.Code}' has invalid latitude {region.Latitude}.");
                    valid = false;
                }

                if (region.Longitude < -180f || region.Longitude > 180f)
                {
                    GD.PrintErr($"NetworkGenerationData: Region '{region.Code}' has invalid longitude {region.Longitude}.");
                    valid = false;
                }

                if (region.InternetWeight < 0f || region.InternetWeight > 1f)
                {
                    GD.PrintErr($"NetworkGenerationData: Region '{region.Code}' has InternetWeight {region.InternetWeight} outside [0, 1].");
                    valid = false;
                }

                if (region.PrivateNetworkRatio < 0f || region.PrivateNetworkRatio > 1f)
                {
                    GD.PrintErr($"NetworkGenerationData: Region '{region.Code}' has PrivateNetworkRatio {region.PrivateNetworkRatio} outside [0, 1].");
                    valid = false;
                }
            }

            // Validate hub cities.
            Boolean hasTier1 = false;
            foreach (HubCityEntry hub in HubCities)
            {
                if (!regionCodes.Contains(hub.RegionCode))
                {
                    GD.PrintErr($"NetworkGenerationData: Hub city '{hub.CityName}' references unknown region '{hub.RegionCode}'.");
                    valid = false;
                }

                if (hub.Tier != 1 && hub.Tier != 2)
                {
                    GD.PrintErr($"NetworkGenerationData: Hub city '{hub.CityName}' has invalid tier {hub.Tier} (must be 1 or 2).");
                    valid = false;
                }

                if (hub.Tier == 1)
                {
                    hasTier1 = true;
                }

                if (hub.Latitude < -90f || hub.Latitude > 90f)
                {
                    GD.PrintErr($"NetworkGenerationData: Hub city '{hub.CityName}' has invalid latitude {hub.Latitude}.");
                    valid = false;
                }

                if (hub.Longitude < -180f || hub.Longitude > 180f)
                {
                    GD.PrintErr($"NetworkGenerationData: Hub city '{hub.CityName}' has invalid longitude {hub.Longitude}.");
                    valid = false;
                }
            }

            if (!hasTier1 && HubCities.Length > 0)
            {
                GD.PrintErr("NetworkGenerationData: No Tier 1 hub cities found. At least one is required for backbone generation.");
                valid = false;
            }

            // Validate cable routes.
            foreach (CableRouteEntry cable in CableRoutes)
            {
                if (!regionCodes.Contains(cable.FromRegionCode))
                {
                    GD.PrintErr($"NetworkGenerationData: Cable route references unknown source region '{cable.FromRegionCode}'.");
                    valid = false;
                }

                if (!regionCodes.Contains(cable.ToRegionCode))
                {
                    GD.PrintErr($"NetworkGenerationData: Cable route references unknown destination region '{cable.ToRegionCode}'.");
                    valid = false;
                }

                if (cable.Latency <= 0f)
                {
                    GD.PrintErr($"NetworkGenerationData: Cable route '{cable.FromRegionCode}' -> '{cable.ToRegionCode}' has non-positive latency {cable.Latency}.");
                    valid = false;
                }
            }

            // Check that every region has at least one hub city.
            HashSet<String> regionsWithHubs = new(HubCities.Select(h => h.RegionCode));
            foreach (String code in regionCodes)
            {
                if (!regionsWithHubs.Contains(code))
                {
                    GD.PrintErr($"NetworkGenerationData: Region '{code}' has no hub cities. It will not generate any networks.");
                    valid = false;
                }
            }

            if (valid)
            {
                GD.Print($"NetworkGenerationData: Validated {Regions.Length} regions, {HubCities.Length} hub cities, {CableRoutes.Length} cable routes.");
            }

            return valid;
        }


        /// <summary> A geographical region with internet infrastructure weighting. </summary>
        /// <param name="Code"> Short unique region code. </param>
        /// <param name="Name"> Human-readable region name. </param>
        /// <param name="Latitude"> Centroid latitude. </param>
        /// <param name="Longitude"> Centroid longitude. </param>
        /// <param name="InternetWeight"> Normalised infrastructure density (0-1). </param>
        /// <param name="PrivateNetworkRatio"> Proportion of networks that are private (0-1). </param>
        public readonly record struct RegionEntry(
            String Code, String Name,
            Single Latitude, Single Longitude,
            Single InternetWeight, Single PrivateNetworkRatio) : IParseable<RegionEntry>
        {
            /// <inheritdoc/>
            public static RegionEntry Parse(String[] header, String[] data)
            {
                String code = String.Empty;
                String name = String.Empty;
                Single latitude = 0f;
                Single longitude = 0f;
                Single internetWeight = 0f;
                Single privateNetworkRatio = 0f;

                for (Int32 i = 0; i < header.Length; i++)
                {
                    switch (header[i])
                    {
                        case "Code":
                            code = data[i];
                            break;
                        case "Name":
                            name = data[i];
                            break;
                        case "Latitude":
                            latitude = Single.Parse(data[i]);
                            break;
                        case "Longitude":
                            longitude = Single.Parse(data[i]);
                            break;
                        case "InternetWeight":
                            internetWeight = Single.Parse(data[i]);
                            break;
                        case "PrivateNetworkRatio":
                            privateNetworkRatio = Single.Parse(data[i]);
                            break;
                    }
                }

                return new RegionEntry(code, name, latitude, longitude, internetWeight, privateNetworkRatio);
            }
        }


        /// <summary> A hub city representing a major internet exchange point or data centre. </summary>
        /// <param name="CityName"> The city's name. </param>
        /// <param name="RegionCode"> The region this city belongs to. </param>
        /// <param name="Latitude"> City latitude. </param>
        /// <param name="Longitude"> City longitude. </param>
        /// <param name="Tier"> 1 for backbone hubs, 2 for regional hubs. </param>
        public readonly record struct HubCityEntry(
            String CityName, String RegionCode,
            Single Latitude, Single Longitude,
            Int32 Tier) : IParseable<HubCityEntry>
        {
            /// <inheritdoc/>
            public static HubCityEntry Parse(String[] header, String[] data)
            {
                String cityName = String.Empty;
                String regionCode = String.Empty;
                Single latitude = 0f;
                Single longitude = 0f;
                Int32 tier = 2;

                for (Int32 i = 0; i < header.Length; i++)
                {
                    switch (header[i])
                    {
                        case "CityName":
                            cityName = data[i];
                            break;
                        case "RegionCode":
                            regionCode = data[i];
                            break;
                        case "Latitude":
                            latitude = Single.Parse(data[i]);
                            break;
                        case "Longitude":
                            longitude = Single.Parse(data[i]);
                            break;
                        case "Tier":
                            tier = Int32.Parse(data[i]);
                            break;
                    }
                }

                return new HubCityEntry(cityName, regionCode, latitude, longitude, tier);
            }
        }


        /// <summary> A major backbone corridor connecting two regions. </summary>
        /// <param name="FromRegionCode"> The source region. </param>
        /// <param name="ToRegionCode"> The destination region. </param>
        /// <param name="Latency"> Gameplay latency cost for this corridor. </param>
        public readonly record struct CableRouteEntry(
            String FromRegionCode, String ToRegionCode, Single Latency) : IParseable<CableRouteEntry>
        {
            /// <inheritdoc/>
            public static CableRouteEntry Parse(String[] header, String[] data)
            {
                String fromRegionCode = String.Empty;
                String toRegionCode = String.Empty;
                Single latency = 1f;

                for (Int32 i = 0; i < header.Length; i++)
                {
                    switch (header[i])
                    {
                        case "FromRegionCode":
                            fromRegionCode = data[i];
                            break;
                        case "ToRegionCode":
                            toRegionCode = data[i];
                            break;
                        case "Latency":
                            latency = Single.Parse(data[i]);
                            break;
                    }
                }

                return new CableRouteEntry(fromRegionCode, toRegionCode, latency);
            }
        }


        /// <summary> Name templates for Tier 1 backbone networks. </summary>
        public static readonly String[] Tier1NameTemplates =
        {
            "{0} IX",
            "{0} Internet Exchange",
            "{0} Data Exchange",
            "{0} Global Hub",
        };

        /// <summary> Name templates for Tier 2 public networks. </summary>
        public static readonly String[] Tier2PublicNameTemplates =
        {
            "{0} Telecom",
            "{0} Broadband",
            "NetServ {0}",
            "{0} Communications",
            "{0} Digital",
            "{0} NetLink",
        };

        /// <summary> Name templates for Tier 2 private networks. </summary>
        public static readonly String[] Tier2PrivateNameTemplates =
        {
            "{0} Corp",
            "{0} Gov Systems",
            "Sentinel {0}",
            "{0} Defense Net",
            "{0} Financial Systems",
            "{0} Industrial",
        };

        /// <summary> Name templates for Tier 3 public networks. </summary>
        public static readonly String[] Tier3PublicNameTemplates =
        {
            "{0} Public WiFi",
            "CyberCafe {0}",
            "University of {0}",
            "{0} Library Net",
            "{0} Community Net",
            "{0} Open Access",
        };

        /// <summary> Name templates for Tier 3 private networks. </summary>
        public static readonly String[] Tier3PrivateNameTemplates =
        {
            "{0} Research Lab",
            "{0} Office LAN",
            "IoT Cluster {0}",
            "{0} Medical Systems",
            "{0} Campus Net",
            "{0} Warehouse Systems",
            "{0} Smart Grid",
        };

        /// <summary> Name templates for off-world networks. </summary>
        public static readonly String[] OffWorldNameTemplates =
        {
            "{0} Station Comms",
            "{0} Colony Net",
            "{0} Outpost Alpha",
            "{0} Research Array",
            "{0} Habitat Systems",
        };
    }
}
