using System;

namespace Dragon.Network
{
    /// <summary> A unique address identifying a device within the network. </summary>
    /// <param name="NetworkId"> The identifier of the network this device belongs to. </param>
    /// <param name="DeviceId"> The local identifier of the device on its network. </param>
    public readonly record struct NetworkAddress(String NetworkId, String DeviceId)
    {
        /// <summary> Returns the full address in display format. </summary>
        public override String ToString() => $"{NetworkId}::{DeviceId}";
    }
}
