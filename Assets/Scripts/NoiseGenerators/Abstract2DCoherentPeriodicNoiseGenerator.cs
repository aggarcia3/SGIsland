using UnityEngine;

namespace SGIsland.NoiseGenerators
{
    /// <summary>
    /// Partial implementation of <see cref="I2DCoherentPeriodicNoiseGenerator"/> that generates fractal noise
    /// by adding together octaves of a base noise function.
    /// </summary>
    public abstract class Abstract2DCoherentPeriodicNoiseGenerator : ScriptableObject, I2DCoherentPeriodicNoiseGenerator
    {
        ///<inheritdoc/>
        public float FractalNoise2D(long seed, float x, float y, float initialOctaveFrequency, float initialOctaveAmplitude, uint octaves, float persistence, float lacunarity)
        {
            // Generate fractal noise by adding octaves of the underlying noise primitive
            float totalAmplitude = 0;
            float totalMaximumAmplitude = 0;
            float octaveFrequency = initialOctaveFrequency;
            float octaveAmplitude = initialOctaveAmplitude;

            for (uint i = 0; i < octaves; ++i)
            {
                totalAmplitude += Noise2D(seed, x * octaveFrequency, y * octaveFrequency) * octaveAmplitude;

                totalMaximumAmplitude += octaveAmplitude;
                octaveAmplitude *= persistence;
                octaveFrequency *= lacunarity;
            }

            return totalAmplitude / totalMaximumAmplitude * initialOctaveAmplitude;
        }

        ///<inheritdoc/>
        public abstract float Noise2D(long seed, float x, float y);
    }
}
