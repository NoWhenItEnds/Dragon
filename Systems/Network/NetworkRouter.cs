using System;
using System.Collections.Generic;
using System.Linq;

namespace Dragon.Network
{
    /// <summary> Finds routes between devices across connected networks. </summary>
    public static class NetworkRouter
    {
        /// <summary> Attempt to find the lowest-latency route between two devices. </summary>
        /// <param name="networks"> All known networks, keyed by ID. </param>
        /// <param name="from"> The source device address. </param>
        /// <param name="to"> The destination device address. </param>
        /// <param name="accessiblePrivateNetworks"> Optional set of private network IDs that may be transited. </param>
        /// <returns> The resolved route, or null if no path exists. </returns>
        public static NetworkRoute? FindRoute(
            IReadOnlyDictionary<String, Network> networks,
            NetworkAddress from,
            NetworkAddress to,
            IReadOnlySet<String>? accessiblePrivateNetworks = null)
        {
            if (!networks.ContainsKey(from.NetworkId) || !networks.ContainsKey(to.NetworkId))
            {
                return null;
            }

            // Same network — direct route with zero latency.
            if (from.NetworkId == to.NetworkId)
            {
                return new NetworkRoute(new[] { from.NetworkId }, 0f, false);
            }

            // Dijkstra's algorithm.
            Dictionary<String, Single> distances = new();
            Dictionary<String, String?> previous = new();
            Dictionary<String, NetworkLink?> previousLink = new();
            PriorityQueue<String, Single> queue = new();

            foreach (String id in networks.Keys)
            {
                distances[id] = Single.MaxValue;
                previous[id] = null;
                previousLink[id] = null;
            }

            distances[from.NetworkId] = 0f;
            queue.Enqueue(from.NetworkId, 0f);

            while (queue.Count > 0)
            {
                String current = queue.Dequeue();

                if (current == to.NetworkId)
                {
                    break;
                }

                if (distances[current] == Single.MaxValue)
                {
                    continue;
                }

                Network currentNetwork = networks[current];
                foreach (NetworkLink link in currentNetwork.Links)
                {
                    if (!networks.TryGetValue(link.TargetNetworkId, out Network? neighbor))
                    {
                        continue;
                    }

                    // Skip private networks unless they are the destination or explicitly accessible.
                    if (neighbor.Accessibility == NetworkAccessibility.Private
                        && link.TargetNetworkId != to.NetworkId
                        && (accessiblePrivateNetworks == null
                            || !accessiblePrivateNetworks.Contains(link.TargetNetworkId)))
                    {
                        continue;
                    }

                    Single newDistance = distances[current] + link.Latency;
                    if (newDistance < distances[link.TargetNetworkId])
                    {
                        distances[link.TargetNetworkId] = newDistance;
                        previous[link.TargetNetworkId] = current;
                        previousLink[link.TargetNetworkId] = link;
                        queue.Enqueue(link.TargetNetworkId, newDistance);
                    }
                }
            }

            // No path found.
            if (distances[to.NetworkId] == Single.MaxValue)
            {
                return null;
            }

            // Reconstruct path.
            List<String> path = new();
            Boolean usesRelay = false;
            String? step = to.NetworkId;

            while (step != null)
            {
                path.Add(step);
                NetworkLink? link = previousLink[step];
                if (link.HasValue && link.Value.Type == LinkType.Relay)
                {
                    usesRelay = true;
                }
                step = previous[step];
            }

            path.Reverse();

            return new NetworkRoute(path, distances[to.NetworkId], usesRelay);
        }
    }
}
