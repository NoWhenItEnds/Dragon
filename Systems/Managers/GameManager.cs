using System;
using System.Linq;
using Dragon.Network;
using Dragon.Utilities.Singletons;
using Dragon.World;
using Godot;

namespace Dragon.Managers
{
    /// <summary> The game world's central manager singleton. </summary>
    public partial class GameManager : SingletonNode<GameManager>
    {
        /// <inheritdoc/>
        public override void _Ready()
        {
            // Wait one frame so child nodes (NetworkManager) have run _Ready().
            CallDeferred(MethodName.TestNetwork);
        }


        /// <summary> Temporary test method that prints network topology stats and sample routes. </summary>
        private void TestNetwork()
        {
            NetworkManager nm = NetworkManager.Instance;

            // Summary stats.
            var allNetworks = nm.GetAllNetworks();
            GD.Print($"\n=== Network Topology Summary ===");
            GD.Print($"Total networks: {allNetworks.Count}");

            foreach (CelestialBody body in Enum.GetValues<CelestialBody>())
            {
                Int32 count = nm.GetNetworksByBody(body).Count();
                if (count > 0)
                {
                    GD.Print($"  {body}: {count} networks");
                }
            }

            // Test a route: Ashburn -> Tokyo.
            NetworkAddress from = new("GEN-HUB-ASHBURN", "RTR-001");
            NetworkAddress to = new("GEN-HUB-TOKYO", "SRV-001");
            NetworkRoute? route = nm.FindRoute(from, to);

            GD.Print($"\n=== Route: {from} -> {to} ===");
            if (route != null)
            {
                GD.Print($"Hops: {route.Path.Count}, Latency: {route.TotalLatency:F1}, Relay: {route.UsesRelay}");
                GD.Print($"Path: {String.Join(" -> ", route.Path)}");
            }
            else
            {
                GD.Print("No route found!");
            }

            // Test a route to Mars.
            Dragon.Network.Network? marsNetwork = nm.GetNetworksByBody(CelestialBody.Mars).FirstOrDefault();
            if (marsNetwork != null)
            {
                Device? marsDevice = marsNetwork.Devices.FirstOrDefault();
                if (marsDevice != null)
                {
                    NetworkRoute? marsRoute = nm.FindRoute(from, marsDevice.Address);
                    GD.Print($"\n=== Route: {from} -> {marsDevice.Address} ===");
                    if (marsRoute != null)
                    {
                        GD.Print($"Hops: {marsRoute.Path.Count}, Latency: {marsRoute.TotalLatency:F1}, Relay: {marsRoute.UsesRelay}");
                        GD.Print($"Path: {String.Join(" -> ", marsRoute.Path)}");
                    }
                    else
                    {
                        GD.Print("No route found!");
                    }
                }
            }
        }
    }
}
