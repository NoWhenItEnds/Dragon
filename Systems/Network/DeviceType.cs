namespace Dragon.Network
{
    /// <summary> The type of a network device. </summary>
    public enum DeviceType
    {
        /// <summary> A user workstation or personal computer. </summary>
        Workstation,

        /// <summary> A server providing services on the network. </summary>
        Server,

        /// <summary> A router that directs network traffic. </summary>
        Router,

        /// <summary> A firewall controlling network access. </summary>
        Firewall,

        /// <summary> An Internet of Things device. </summary>
        IoTDevice,

        /// <summary> A terminal or console for direct access. </summary>
        Terminal
    }
}
