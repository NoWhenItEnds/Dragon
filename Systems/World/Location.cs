using System;

namespace Dragon.World
{
    /// <summary> A geographical or orbital location in the game world. </summary>
    /// <param name="Body"> The celestial body this location is on. </param>
    /// <param name="Latitude"> Latitude on the body surface (-90 to 90). </param>
    /// <param name="Longitude"> Longitude on the body surface (-180 to 180). </param>
    public readonly record struct Location(CelestialBody Body, Single Latitude, Single Longitude);
}
