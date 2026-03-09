namespace Dragon.Network
{
    /// <summary> Whether a network is publicly routable or private. </summary>
    public enum NetworkAccessibility
    {
        /// <summary> Routable from the public internet. </summary>
        Public,

        /// <summary> Accessible only from within the network or via special access. </summary>
        Private
    }
}
