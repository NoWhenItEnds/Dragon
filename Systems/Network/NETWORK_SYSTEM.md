# Network System Documentation

## Overview

The Dragon network system simulates a global internet topology for a rogue AI simulator. It combines hand-authored **story networks** (loaded from CSVs) with **procedurally generated** infrastructure based on real-world internet statistics. The system supports routing across Earth-based and off-world networks.

### Architecture at a Glance

```
Systems/
  World/
    CelestialBody.cs          # Enum: Earth, Moon, Mars, OrbitalStation
    Location.cs                # Record struct: Body + Latitude + Longitude

  Network/
    DeviceType.cs              # Enum: Workstation, Server, Router, etc.
    LinkType.cs                # Enum: Standard, Relay
    NetworkAccessibility.cs    # Enum: Public, Private
    NetworkAddress.cs          # Record struct: NetworkId + DeviceId
    NetworkLink.cs             # Record struct: TargetNetworkId + Type + Latency
    Device.cs                  # Class: a device on a network
    Network.cs                 # Class: a network with devices and links
    NetworkRoute.cs            # Class: a resolved route between two devices
    NetworkRouter.cs           # Static: Dijkstra pathfinding
    NetworkManager.cs          # Singleton: loads data, builds topology, routing API

    Generation/
      NetworkGenerationData.cs # Loads and validates generation CSVs
      NetworkGenerationResult.cs # Container for generated output
      NetworkGenerator.cs      # Static: procedural topology generation

Data/
  Networks/                    # Story network CSVs (hand-authored)
    networks.csv
    devices.csv
    links.csv

  Generation/                  # Generation parameter CSVs (real-world data)
    regions.csv
    hub_cities.csv
    cable_routes.csv
```

---

## Core Concepts

### Location (`Dragon.World`)

A `Location` is an immutable record struct combining a `CelestialBody` with latitude/longitude coordinates. It is intentionally separated from the network namespace so other systems (map UI, missions, lore) can reuse it.

```csharp
Location loc = new(CelestialBody.Earth, 51.5f, -0.1f); // London
Location mars = new(CelestialBody.Mars, 4.5f, 137.4f);
```

### Network Address

Devices are identified by a `NetworkAddress` containing a `NetworkId` and a `DeviceId`. The display format uses `::` as a separator:

```
PENTAGON-LAN::SRV-001
GEN-HUB-ASHBURN::RTR-001
```

### Networks and Devices

A `Network` has:
- A unique string ID
- A human-readable name
- A physical `Location`
- An `Accessibility` (Public or Private)
- A list of `Device` objects
- A list of `NetworkLink` connections to other networks

Devices inherit their location from their parent network. A device has a type (Server, Workstation, Router, etc.), a name, and a unique `NetworkAddress`.

### Links

A `NetworkLink` connects one network to another. It has:
- `TargetNetworkId` - the network on the other end
- `Type` - `Standard` (wired/wireless) or `Relay` (long-range, e.g. Earth-to-Mars)
- `Latency` - a gameplay cost value (not real milliseconds)

Links are **bidirectional**. When loaded from CSVs or generated procedurally, both networks receive a link entry pointing to each other.

### Routing

`NetworkRouter.FindRoute()` uses Dijkstra's algorithm to find the lowest-latency path between two devices. Routing rules:

1. The source and destination networks are always accessible (you are "on" one and have the address of the other).
2. **Private networks cannot be transited** unless they are the destination or listed in the optional `accessiblePrivateNetworks` parameter.
3. The algorithm minimises total latency.
4. Same-network routes return immediately with zero latency.

```csharp
NetworkRoute? route = NetworkManager.Instance.FindRoute(
    new NetworkAddress("GEN-HUB-ASHBURN", "RTR-001"),
    new NetworkAddress("GEN-HUB-TOKYO", "SRV-001"));

if (route != null)
{
    // route.Path       -> ["GEN-HUB-ASHBURN", "GEN-HUB-SAN-JOSE", "GEN-HUB-TOKYO"]
    // route.TotalLatency -> 42.0
    // route.UsesRelay   -> false
}
```

To allow routing through compromised private networks:

```csharp
HashSet<String> hacked = new() { "PENTAGON-LAN" };
NetworkRoute? route = nm.FindRoute(from, to, hacked);
```

---

## Data Loading

### Story Networks

Story networks are hand-authored CSVs placed in `res://Data/Networks/`. These are loaded first and take priority over generated networks.

**networks.csv:**
```
Id,Name,Body,Latitude,Longitude,Accessibility
PENTAGON-LAN,Pentagon Internal,Earth,38.87,-77.06,Private
AWS-US-EAST,AWS US East,Earth,39.04,-77.49,Public
```

**devices.csv:**
```
NetworkId,DeviceId,Name,Type
PENTAGON-LAN,SRV-001,Main Server,Server
PENTAGON-LAN,WS-001,Analyst Terminal,Workstation
```

**links.csv** (only define each link once; bidirectional is automatic):
```
SourceNetworkId,TargetNetworkId,Type,Latency
PENTAGON-LAN,AWS-US-EAST,Standard,5
```

The `Body` column accepts any `CelestialBody` enum value. The `Accessibility` column accepts `Public` or `Private`. The `Type` columns accept the corresponding enum values (`DeviceType`, `LinkType`).

### Generation Data

Generation parameters are in `res://Data/Generation/`. These describe the real-world internet infrastructure that drives procedural generation.

**regions.csv** - Geographic regions with infrastructure weighting:
```
Code,Name,Latitude,Longitude,InternetWeight,PrivateNetworkRatio
US-EAST,US East Coast,38.9,-77.0,1.00,0.40
```

- `InternetWeight` (0-1): Controls how many networks are generated. US-EAST at 1.0 gets the most; AFRICA-W at 0.1 gets the fewest.
- `PrivateNetworkRatio` (0-1): Proportion of generated networks that are Private.

**hub_cities.csv** - Real IXP locations:
```
CityName,RegionCode,Latitude,Longitude,Tier
Ashburn,US-EAST,39.0,-77.5,1
Chicago,US-CENT,41.9,-87.6,2
```

- `Tier 1`: Major backbone hubs (Ashburn, Frankfurt, Tokyo, etc.). These form the global backbone mesh.
- `Tier 2`: Regional hubs. These become connection points for regional and local networks.

**cable_routes.csv** - Backbone corridors between regions:
```
FromRegionCode,ToRegionCode,Latency
US-EAST,UK,20
US-EAST,EU-WEST,22
```

---

## Procedural Generation

### How It Works

`NetworkGenerator.Generate()` takes a `NetworkGenerationData` instance, a seed, and a set of reserved IDs (from story networks). It produces a `NetworkGenerationResult` containing networks, devices, and links.

The generation follows a **three-tier hierarchical topology** that mirrors real internet structure:

#### Tier 1: Backbone Hubs (~13 networks)
- One network per Tier 1 hub city (Ashburn, Frankfurt, Tokyo, etc.)
- Always **Public**
- Each gets a Router and a Server
- Connected via cable route definitions (submarine cables, continental backbones)
- All Tier 1 hubs in the same region are fully meshed
- ID format: `GEN-HUB-{CITYNAME}`

#### Tier 2: Regional Networks (~80 networks)
- Per region: `floor(InternetWeight * 10)` networks (minimum 1)
- Mix of **Public** and **Private** based on region's `PrivateNetworkRatio`
- 1-3 devices each (Router mandatory, optional Server/Firewall)
- Connected to nearest Tier 1 hub in the region
- 20% chance of a redundant second link to a different Tier 1 hub
- Location jittered around hub cities (up to 3 degrees)
- ID format: `GEN-REG-{REGION}-{INDEX}`

#### Tier 3: Local/Edge Networks (~200 networks)
- Per region: `floor(InternetWeight * 20)` networks (minimum 1)
- Mostly **Private** (region's private ratio + 20%)
- 1-5 devices (Router + Workstations, Terminals, IoT, Servers)
- Connected to nearest Tier 2 network only (leaf nodes - dead ends)
- Location jittered around parent Tier 2 network (up to 1.5 degrees)
- ID format: `GEN-LOC-{REGION}-{INDEX}`

#### Off-World Networks (~10 networks)
- 4 Orbital Station, 3 Moon, 3 Mars networks
- 70% Private
- Connected to a random Earth Tier 1 hub via **Relay** links
- All networks on the same body are meshed together with Standard links
- ID format: `GEN-OW-{BODY}-{INDEX}`

#### Shortcuts (8 links)
- Random cross-region links between Tier 2 networks
- Represent CDN tunnels, VPN connections, corporate WANs
- Latency discounted by 30% (they are optimised paths)

### Latency Values

These are gameplay units, not real milliseconds:

| Connection Type | Latency Range |
|----------------|---------------|
| Intra-region | 1-3 |
| Same continent | 5-15 |
| Intercontinental | 15-40 |
| Earth to Orbital Station | 50 |
| Earth to Moon | 100 |
| Earth to Mars | 500 |

### Determinism

Generation is fully deterministic given the same seed and data. The seed is an `[Export]` property on `NetworkManager`, configurable from the Godot editor.

### Network Naming

Names are generated from template pools based on tier and accessibility:

- **Tier 1**: "Frankfurt IX", "Tokyo Internet Exchange"
- **Tier 2 Public**: "Chicago Telecom", "NetServ Paris"
- **Tier 2 Private**: "London Corp", "Sentinel Dubai"
- **Tier 3 Public**: "CyberCafe Seoul", "University of Mumbai"
- **Tier 3 Private**: "Tokyo Research Lab", "IoT Cluster Lagos"
- **Off-world**: "Mars Colony Net", "Moon Research Array"

---

## Validation

When generation data is loaded, `NetworkGenerationData.Validate()` checks:

- No duplicate region codes
- Latitude in [-90, 90] and longitude in [-180, 180] for all entries
- `InternetWeight` and `PrivateNetworkRatio` in [0, 1]
- Every hub city references an existing region code
- Hub tier is 1 or 2
- At least one Tier 1 hub exists
- Every cable route references valid region codes
- Cable route latency is positive
- Every region has at least one hub city

If validation fails, specific error messages are printed and world generation is skipped. Story networks still load normally.

---

## NetworkManager API

`NetworkManager` is a singleton accessed via `NetworkManager.Instance`. It initialises in `_Ready()`:

1. Load story networks from CSVs
2. Load story devices and attach to networks
3. Load story links (bidirectional)
4. Load generation data, validate, and generate world

### Public Methods

```csharp
// Look up by ID
Network? net = nm.GetNetwork("GEN-HUB-ASHBURN");
Device? dev = nm.GetDevice(new NetworkAddress("GEN-HUB-ASHBURN", "RTR-001"));

// Get all networks
IReadOnlyDictionary<String, Network> all = nm.GetAllNetworks();

// Filter by celestial body
IEnumerable<Network> marsNets = nm.GetNetworksByBody(CelestialBody.Mars);

// Find a route
NetworkRoute? route = nm.FindRoute(fromAddress, toAddress);
NetworkRoute? route = nm.FindRoute(fromAddress, toAddress, accessiblePrivateNetworks);
```

### Scene Setup

Add `NetworkManager` as a child Node in your scene with its script attached:

```
Game (Node) - GameManager.cs
  NetworkManager (Node) - NetworkManager.cs
```

The `Seed` property is exported and editable in the Godot inspector.

---

## Extending the System

### Adding New Regions

Edit `Data/Generation/regions.csv`. Add a row with:
- A unique region code
- At least one corresponding hub city in `hub_cities.csv`
- At least one cable route connecting it to another region in `cable_routes.csv`

The validation pass will catch missing hub cities or dangling region references.

### Adding New Hub Cities

Edit `Data/Generation/hub_cities.csv`. The `RegionCode` must match an existing region. Tier 1 cities become backbone nodes; Tier 2 cities are used for regional network placement.

### Adding New Cable Routes

Edit `Data/Generation/cable_routes.csv`. Both region codes must exist. The latency value is a gameplay unit - use existing routes as reference.

### Adding New Celestial Bodies

1. Add the value to the `CelestialBody` enum in `Systems/World/CelestialBody.cs`
2. Add a generation entry in `NetworkGenerator.GenerateOffWorldNetworks()` in the `offWorldBodies` array with the body, count, and relay latency

### Adding New Device Types

Add the value to the `DeviceType` enum in `Systems/Network/DeviceType.cs`. To use it in generation, update the device type arrays in `NetworkGenerator`.

### Adding New Name Templates

Edit the static `String[]` arrays in `NetworkGenerationData.cs` (e.g. `Tier2PublicNameTemplates`). Use `{0}` as a placeholder for the city name.

### Custom Routing Constraints

`FindRoute()` accepts `IReadOnlySet<String>? accessiblePrivateNetworks` to allow transit through specific private networks. This supports gameplay mechanics like hacking or gaining access to restricted networks.

### Tuning Generation Scale

The constants at the top of `NetworkGenerator.cs` control topology density:

| Constant | Default | Effect |
|----------|---------|--------|
| `REGIONAL_SCALE` | 10 | Tier 2 networks per region (multiplied by InternetWeight) |
| `LOCAL_SCALE` | 20 | Tier 3 networks per region (multiplied by InternetWeight) |
| `LOCATION_JITTER` | 3.0 | Max degrees of coordinate randomisation |
| `REDUNDANT_LINK_CHANCE` | 0.20 | Probability of Tier 2 getting a second backbone link |
| `SHORTCUT_COUNT` | 8 | Number of random cross-region Tier 2 shortcuts |

---

## Limitations

- **No dynamic topology at runtime.** Networks, devices, and links are created at startup. There is no API to add/remove networks or links after initialisation. Supporting this would require mutation methods on `NetworkManager` and potentially event signals for topology changes.

- **No bandwidth or capacity modelling.** Links have latency but no throughput limit. All routes are equivalent in terms of data capacity.

- **No DNS or service discovery.** To reach a device, you need its exact `NetworkAddress`. There is no name resolution system.

- **Flat device model.** Devices have a type and name but no state, health, security level, or running services. These would need to be layered on top.

- **Off-world generation is hardcoded.** The number of off-world networks and relay latencies are constants in the generator, not driven by CSV data. Moving these to a CSV would require a new data type.

- **No multi-path or load-balanced routing.** Dijkstra returns a single lowest-latency path. There is no support for finding alternative routes or distributing traffic.

- **Approximate distance calculation.** The equirectangular projection used for distance is inaccurate near the poles. This is acceptable for gameplay but not for simulation fidelity.

- **Name template collisions.** Two networks in the same city can get the same generated name. IDs are always unique, but display names may not be.

- **No persistence.** The generated topology exists only in memory. Saving/loading game state would require serialising the network graph.
