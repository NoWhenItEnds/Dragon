using System;
using System.Collections.Generic;

namespace Dragon.Network
{
    /// <summary> A resolved route between two devices across networks. </summary>
    public sealed class NetworkRoute
    {
        /// <summary> The ordered list of network IDs traversed from source to destination. </summary>
        public IReadOnlyList<String> Path { get; }

        /// <summary> The total latency cost of the route. </summary>
        public Single TotalLatency { get; }

        /// <summary> Whether the route passes through any relay links. </summary>
        public Boolean UsesRelay { get; }


        /// <summary> Creates a new network route. </summary>
        /// <param name="path"> The ordered list of network IDs traversed. </param>
        /// <param name="totalLatency"> The total latency cost. </param>
        /// <param name="usesRelay"> Whether the route passes through any relay links. </param>
        public NetworkRoute(IReadOnlyList<String> path, Single totalLatency, Boolean usesRelay)
        {
            Path = path;
            TotalLatency = totalLatency;
            UsesRelay = usesRelay;
        }
    }
}
