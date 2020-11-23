/// <summary>
/// Interface for 2D coherent noise generators. Each generator uses different functions to generate noise.
///
/// The most well known example of a coherent noise function is the Perlin noise function.
/// </summary>
public interface I2DCoherentNoiseGenerator
{
    /// <summary>
    /// Calculates a 2D noise value for the specified 2D coordinates.
    /// </summary>
    /// <param name="seed">The seed that will be used to generate the noise.</param>
    /// <param name="x">The X value of the coordinate whose noise value is to be calculated.</param>
    /// <param name="y">The Y value of the coordinate whose noise value is to be calculated.</param>
    /// <returns>The computed 2D noise value.</returns>
    float Noise2D(long seed, float x, float y);

    /// <summary>
    /// Calculates fractal noise by adding together octaves of 2D noise for the specified 2D coordinates.
    /// <param name="seed">The seed that will be used to generate each octave of noise.</param>
    /// <param name="x">The X value of the coordinate whose noise value is to be calculated.</param>
    /// <param name="y">The Y value of the coordinate whose noise value is to be calculated.</param>
    /// <param name="initialOctaveFrequency">The frequency (coefficient) of the 2D coordinates for the first octave.</param>
    /// <param name="initialOctaveAmplitude">The amplitude of the first octave, that defines the rough shape of the noise.</param>
    /// <param name="octaves">The number of octaves that will be added together to generate noise. More octaves result in noise with finer details.</param>
    /// <param name="persistence">The persistence coefficient that will control the variation of amplitude for successive octaves. Higher values mean that secondary octaves contribute more to the final noise.</param>
    /// <param name="lacunarity">The persistence coefficient that will control the variation of frequency for successive octaves. Higher values mean that secondary octaves will increase details more sharply.</param>
    /// <returns>The computed fractal noise value. It is guaranteed to be in [0, initialOctaveAmplitude].</returns>
    float FractalNoise2D(long seed, float x, float y, float initialOctaveFrequency, float initialOctaveAmplitude, uint octaves, float persistence, float lacunarity);
}
