using System;
using static Dragon.Utilities.Extensions.CsvExtensions;

namespace Dragon.Network
{
    /// <summary> A connection between two networks. </summary>
    /// <param name="TargetNetworkId"> The ID of the network this link connects to. </param>
    /// <param name="Type"> Whether this is a standard or relay link. </param>
    /// <param name="Latency"> Gameplay cost of traversing this link. </param>
    public readonly record struct NetworkLink(
        String TargetNetworkId,
        LinkType Type,
        Single Latency) : IParseable<NetworkLink>
    {
        /// <inheritdoc/>
        public static NetworkLink Parse(String[] header, String[] data)
        {
            String targetNetworkId = String.Empty;
            LinkType type = LinkType.Standard;
            Single latency = 1f;

            for (Int32 i = 0; i < header.Length; i++)
            {
                switch (header[i])
                {
                    case "TargetNetworkId":
                        targetNetworkId = data[i];
                        break;
                    case "Type":
                        type = Enum.Parse<LinkType>(data[i]);
                        break;
                    case "Latency":
                        latency = Single.Parse(data[i]);
                        break;
                }
            }

            return new NetworkLink(targetNetworkId, type, latency);
        }
    }
}
