namespace SGIsland.NoiseGenerators
{
    /// <summary>
    /// Interface for 2D coherent periodic noise generators. Each generator uses different functions to generate noise,
    /// but all of them have period 1 (so that the noise repeats after 1 unit along each axis).
    ///
    /// The most well known example of a coherent periodic noise function is the Perlin noise function, but modified
    /// to be periodic.
    /// </summary>
    public interface I2DCoherentPeriodicNoiseGenerator : I2DCoherentNoiseGenerator { }
}
