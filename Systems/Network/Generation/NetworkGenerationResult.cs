using System;
using System.Collections.Generic;

namespace Dragon.Network.Generation
{
    /// <summary> The output of procedural network generation. </summary>
    public sealed class NetworkGenerationResult
    {
        /// <summary> All generated networks. </summary>
        public List<Network> Networks { get; } = new();

        /// <summary> All generated devices. </summary>
        public List<Device> Devices { get; } = new();

        /// <summary> All generated links as (SourceNetworkId, Link) pairs. </summary>
        public List<(String SourceId, NetworkLink Link)> Links { get; } = new();
    }
}
