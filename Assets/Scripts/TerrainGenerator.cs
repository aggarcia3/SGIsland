using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

/// <summary>
/// Generates a realistic-looking procedural terrain on the fly via coherent noise.
/// Currently, the coherent noise used by this class is a variant of OpenSimplex,
/// whose reference implementation is at <see href="https://github.com/KdotJPG/OpenSimplex2"/>,
/// which is used to make pink (fractal) noise.
/// </summary>
[RequireComponent(typeof(Terrain))]
public class TerrainGenerator : MonoBehaviour
{
    private const short NumberOfSimplexPermutationsMask = 2047;

    private static readonly SimplexLatticePoint[] simplexNoiseLUT;
    private static readonly SimplexPointGradients[] simplexGradients;

    private long? _terrainSeed;
    [SerializeField]
    private float _maximumTerrainAmplitude;
    [SerializeField]
    private uint _terrainNoiseOctaves;
    [SerializeField]
    private float _terrainNoiseFrequency;
    [SerializeField]
    private float _terrainNoisePersistence;
    [SerializeField]
    private float _terrainNoiseLacunarity;
    [SerializeField]
    private Vector2Int _terrainLayerTextureSize;

    private short[] simplexPermutations;
    private SimplexPointGradients[] simplexPermutatedGradients;

    private bool areSimplexSeedPermutationsUpToDate = false;

    /// <summary>
    /// The seed used to generate the terrain. Same seeds produce the same terrains.
    /// More precisely, this seed value affects the OpenSimplex noise permutations that are used
    /// to generate a terrain heightmap.
    /// </summary>
    /// <exception cref="InvalidOperationException">If trying to get the seed before it is set for the first time.</exception>
    public long TerrainSeed
    {
        get => _terrainSeed.Value;
        set
        {
            if (!_terrainSeed.HasValue || _terrainSeed != value)
            {
                areSimplexSeedPermutationsUpToDate = false;
                _terrainSeed = value;
            }
        }
    }

    /// <summary>
    /// The maximum height of the terrain, relative to the maximum height of the terrain mesh in world units.
    /// This changes the amplitude of the added values of several octaves of OpenSimplex noise.
    /// </summary>
    public float MaximumTerrainAmplitude
    {
        get => _maximumTerrainAmplitude;
        set
        {
            if (_maximumTerrainAmplitude != value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentException("The maximum height is NaN or infinite");

                _maximumTerrainAmplitude = value;
            }
        }
    }

    /// <summary>
    /// The number of coherent noise octaves that will be used to generate the terrain.
    /// Higher values create a more interesting and detailed terrain, that will be defined by
    /// a wider range of noise frequencies, but take longer to compute.
    /// </summary>
    public uint TerrainNoiseOctaves
    {
        get => _terrainNoiseOctaves;
        set
        {
            if (_terrainNoiseOctaves != value)
            {
                _terrainNoiseOctaves = value;
            }
        }
    }

    /// <summary>
    /// The base frequency of the coherent noise octaves that will be used to generate the terrain.
    /// Higher values mean that the noise will vary more quickly, while lower values smooth out transitions.
    /// </summary>
    public float TerrainNoiseFrequency
    {
        get => _terrainNoiseFrequency;
        set
        {
            if (_terrainNoiseFrequency != value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentException("The maximum height is NaN or infinite");

                _terrainNoiseFrequency = value;
            }
        }
    }

    /// <summary>
    /// Determines the contribution to the final height of the octave frequencies that will be used to generate the terrain.
    /// Higher values mean the second and successive octaves will have more effect in the final terrain, following a
    /// geometric progression.
    /// </summary>
    public float TerrainNoisePersistence
    {
        get => _terrainNoisePersistence;
        set
        {
            if (_terrainNoisePersistence != value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentException("The maximum height is NaN or infinite");

                _terrainNoisePersistence = value;
            }
        }
    }

    /// <summary>
    /// Determines how much frequencies will increase from octave to octave when generating the terrain.
    /// Higher values mean that frequencies will increase more rapidly. This will increase the occurence
    /// of minor height details in the terrain.
    /// </summary>
    /// <see cref="TerrainNoiseFrequency"/>
    public float TerrainNoiseLacunarity
    {
        get => _terrainNoiseLacunarity;
        set
        {
            if (_terrainNoiseLacunarity != value)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                    throw new ArgumentException("The maximum height is NaN or infinite");

                _terrainNoiseLacunarity = value;
            }
        }
    }

    private void OnValidate()
    {
        if (_terrainLayerTextureSize.x < 16 || _terrainLayerTextureSize.x > 4096 || _terrainLayerTextureSize.y < 16 || _terrainLayerTextureSize.y > 4096)
            throw new ArgumentException("A coordinate of the terrain layer texture size is either too big or small");
    }

    static TerrainGenerator()
    {
        simplexGradients = new SimplexPointGradients[NumberOfSimplexPermutationsMask + 1];
        simplexNoiseLUT = new SimplexLatticePoint[8 * 4];

        // Initialize gradients
        SimplexPointGradients[] gradients = {
            new SimplexPointGradients{ Dx = 0.130526192220052f, Dy = 0.99144486137381f },
            new SimplexPointGradients{ Dx = 0.38268343236509f, Dy = 0.923879532511287f },
            new SimplexPointGradients{ Dx = 0.608761429008721f, Dy = 0.793353340291235f },
            new SimplexPointGradients{ Dx = 0.793353340291235f, Dy = 0.608761429008721f },
            new SimplexPointGradients{ Dx = 0.923879532511287f, Dy = 0.38268343236509f },
            new SimplexPointGradients{ Dx = 0.99144486137381f, Dy = 0.130526192220051f },
            new SimplexPointGradients{ Dx = 0.99144486137381f, Dy = -0.130526192220051f },
            new SimplexPointGradients{ Dx = 0.923879532511287f, Dy = -0.38268343236509f },
            new SimplexPointGradients{ Dx = 0.793353340291235f, Dy = -0.60876142900872f },
            new SimplexPointGradients{ Dx = 0.608761429008721f, Dy = -0.793353340291235f },
            new SimplexPointGradients{ Dx = 0.38268343236509f, Dy = -0.923879532511287f },
            new SimplexPointGradients{ Dx = 0.130526192220052f, Dy = -0.99144486137381f },
            new SimplexPointGradients{ Dx = -0.130526192220052f, Dy = -0.99144486137381f },
            new SimplexPointGradients{ Dx = -0.38268343236509f, Dy = -0.923879532511287f },
            new SimplexPointGradients{ Dx = -0.608761429008721f, Dy = -0.793353340291235f },
            new SimplexPointGradients{ Dx = -0.793353340291235f, Dy = -0.608761429008721f },
            new SimplexPointGradients{ Dx = -0.923879532511287f, Dy = -0.38268343236509f },
            new SimplexPointGradients{ Dx = -0.99144486137381f, Dy = -0.130526192220052f },
            new SimplexPointGradients{ Dx = -0.99144486137381f, Dy = 0.130526192220051f },
            new SimplexPointGradients{ Dx = -0.923879532511287f, Dy = 0.38268343236509f },
            new SimplexPointGradients{ Dx = -0.793353340291235f, Dy = 0.608761429008721f },
            new SimplexPointGradients{ Dx = -0.608761429008721f, Dy = 0.793353340291235f },
            new SimplexPointGradients{ Dx = -0.38268343236509f, Dy = 0.923879532511287f },
            new SimplexPointGradients{ Dx = -0.130526192220052f, Dy = 0.99144486137381f }
        };
        for (int i = 0; i < gradients.Length; ++i)
        {
            gradients[i].Dx /= 0.05481866495625118f;
            gradients[i].Dy /= 0.05481866495625118f;
        }
        for (int i = 0; i < simplexGradients.Length; i++)
        {
            simplexGradients[i] = gradients[i % gradients.Length];
        }

        // Initialize lookup table for lattice points
        for (int i = 0; i < 8; ++i)
        {
            int i1, j1, i2, j2;

            if ((i & 1) == 0)
            {
                if ((i & 2) == 0) { i1 = -1; j1 = 0; } else { i1 = 1; j1 = 0; }
                if ((i & 4) == 0) { i2 = 0; j2 = -1; } else { i2 = 0; j2 = 1; }
            }
            else
            {
                if ((i & 2) != 0) { i1 = 2; j1 = 1; } else { i1 = 0; j1 = 1; }
                if ((i & 4) != 0) { i2 = 1; j2 = 2; } else { i2 = 1; j2 = 0; }
            }

            simplexNoiseLUT[i * 4 + 0] = new SimplexLatticePoint(0, 0);
            simplexNoiseLUT[i * 4 + 1] = new SimplexLatticePoint(1, 1);
            simplexNoiseLUT[i * 4 + 2] = new SimplexLatticePoint(i1, j1);
            simplexNoiseLUT[i * 4 + 3] = new SimplexLatticePoint(i2, j2);
        }
    }

    /// <summary>
    /// Procedurally populates the sibling Terrain component with terrain features.
    /// This operation may take a noticeable amount of time. As such, it is recommended
    /// to execute it in a coroutine.
    /// </summary>
    public void GenerateTerrain()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        print("Generating terrain...");

        UnityEngine.Profiling.Profiler.BeginSample("Terrain Generation");

        Terrain terrain = gameObject.GetComponent<Terrain>();
        TerrainData terrainData = terrain.terrainData;

        int terrainLayerTextureSizeOffByOneX = _terrainLayerTextureSize.x - 1;
        int terrainLayerTextureSizeOffByOneY = _terrainLayerTextureSize.y - 1;
        float halfTerrainLayerTextureOffByOneWidth = (float)terrainLayerTextureSizeOffByOneX / 2;
        float halfTerrainLayerTextureOffByOneHeight = (float)terrainLayerTextureSizeOffByOneY / 2;

        // Compute the height points for the terrain heightmap
        int heightMapResolution = terrainData.heightmapResolution;
        float[,] terrainHeightPoints = new float[heightMapResolution, heightMapResolution];
        for (int x = 0; x < heightMapResolution; ++x)
        {
            for (int y = 0; y < heightMapResolution; ++y)
            {
                terrainHeightPoints[y, x] = OpenSimplex2DFractalNoise(
                    (float)x / heightMapResolution, (float)y / heightMapResolution,
                    _terrainNoiseFrequency, _maximumTerrainAmplitude, _terrainNoiseOctaves,
                    _terrainNoisePersistence, _terrainNoiseLacunarity
                );
            }
        }

        terrainData.SetHeights(0, 0, terrainHeightPoints);

        // Generate the sand texture
        Texture2D sandTexture = new Texture2D(_terrainLayerTextureSize.x, _terrainLayerTextureSize.y, TextureFormat.RGB24, true);
        Texture2D sandNormalMapTexture = new Texture2D(_terrainLayerTextureSize.x, _terrainLayerTextureSize.y, TextureFormat.RGB24, false);
        uint sandRipples = (26 + (uint)_terrainSeed % 21) & 0xFFFFFFFE; // Even numbers in [26, 46]
        for (int x = 0; x < _terrainLayerTextureSize.x; ++x)
        {
            for (int y = 0; y < _terrainLayerTextureSize.y; ++y)
            {
                // Normalize the X and Y texture coordinates to U and V so they are in [0, 1].
                // This is intentionally of by one because the pixel coordinates start at 0
                float u = (float)x / terrainLayerTextureSizeOffByOneX, v = (float)y / terrainLayerTextureSizeOffByOneY;
                // The characteristic (ideal) sand texture function is cos((sandRipples * (u + v)) * PI),
                // that has sandRipples "ups" and "downs" in a diagonal (cosine period is 2 * PI, its maximum
                // absolute value is at 0 = 2 * PI and PI, and its minimum value at PI / 2 and
                // (3 * PI) / 4. If we didn't multiply the sum by sandRipples we would just go over a single
                // period of the cosine function and get a single "up" in the bottom-left and
                // top-right corners). For the characteristic function result to be tileable, sandRipples
                // needs to be even. Then that is distorted (phase shifted) by using coherent noise,
                // making sure that we both pairs of edges of the texture have the same coherent noise
                // coordinates so that we distort by the same amount on both edges and the result is still
                // tileable
                float ut = fastAbs(u - 0.5f) / 0.5f, vt = fastAbs(v - 0.5f) / 0.5f;
                float distortion = OpenSimplex2DNoise(ut * 1.5f, vt * 1.5f);
                float rippleIntensity = 0.5f + 0.5f * Mathf.Cos((distortion + sandRipples * (u + v)) * Mathf.PI);
                sandTexture.SetPixel(x, y, new Color(
                    Mathf.LerpUnclamped(0.76f, 1, rippleIntensity),
                    Mathf.LerpUnclamped(0.7f, 0.94f, rippleIntensity),
                    Mathf.LerpUnclamped(0.5f, 0.79f, rippleIntensity)
                ));
                sandNormalMapTexture.SetPixel(x, y, new Color(
                    0.5f,
                    0.5f,
                    rippleIntensity * (4 * rippleIntensity - 4) + 1
                ));
            }
        }
        sandTexture.Apply();
        sandNormalMapTexture.Apply();

        // Generate the dirt texture
        Texture2D dirtTexture = new Texture2D(_terrainLayerTextureSize.x, _terrainLayerTextureSize.y, TextureFormat.RGB24, true);
        for (int x = 0; x < _terrainLayerTextureSize.x; ++x)
        {
            for (int y = 0; y < _terrainLayerTextureSize.y; ++y)
            {
                // Off by one so the edges match exactly
                float u = fastAbs(x - halfTerrainLayerTextureOffByOneWidth) / halfTerrainLayerTextureOffByOneWidth;
                float v = fastAbs(y - halfTerrainLayerTextureOffByOneHeight) / halfTerrainLayerTextureOffByOneHeight;
                float ut = OpenSimplex2DNoise(u * 512, v * 512);
                dirtTexture.SetPixel(x, y, new Color(
                    Mathf.LerpUnclamped(0.61f, 0.71f, ut),
                    Mathf.LerpUnclamped(0.47f, 0.57f, ut),
                    Mathf.LerpUnclamped(0.33f, 0.43f, ut)
                ));
            }
        }
        dirtTexture.Apply();

        // Generate the grass texture
        Texture2D grassTexture = new Texture2D(_terrainLayerTextureSize.x, _terrainLayerTextureSize.y, TextureFormat.RGB24, true);
        for (int x = 0; x < _terrainLayerTextureSize.x; ++x)
        {
            for (int y = 0; y < _terrainLayerTextureSize.y; ++y)
            {
                // Off by one so the edges match exactly
                float u = fastAbs(x - halfTerrainLayerTextureOffByOneWidth) / halfTerrainLayerTextureOffByOneWidth;
                float v = fastAbs(y - halfTerrainLayerTextureOffByOneHeight) / halfTerrainLayerTextureOffByOneHeight;
                float ut = OpenSimplex2DFractalNoise(u, v, 256, 1, 4, 0.5f, 1.5f);
                grassTexture.SetPixel(x, y, new Color(
                    // Lagrange polynomials for RGB component interpolation between three base grass colors
                    0.38f * ut * ut - 0.81f * ut + 0.59f,
                    0.16f * ut * ut - 0.58f * ut + 0.64f,
                    -0.14f * ut * ut - 0.17f * ut + 0.34f
                ));
            }
        }
        grassTexture.Apply();

        // Create the sand terrain layer
        TerrainLayer sandLayer = new TerrainLayer();
        sandLayer.metallic = 0.0f;
        sandLayer.diffuseTexture = sandTexture;
        sandLayer.normalMapTexture = sandNormalMapTexture;
        sandLayer.tileSize = new Vector2(2.0f, 2.0f);

        terrainData.terrainLayers = new TerrainLayer[] { sandLayer };

        // Make sure the basemap distance is appropriate for the maximum height of the terrain
        // (so if the player looks down when in the top of a mountain things look okay)
        terrain.basemapDistance = terrainData.heightmapScale.y / 2;

        UnityEngine.Profiling.Profiler.EndSample();

        stopwatch.Stop();
        print($"Terrain generated in {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Calculates fractal noise by adding together octaves of OpenSimplex 2D noise for the specified 2D coordinates.
    /// <param name="x">The X value of the coordinate whose noise value is to be calculated.</param>
    /// <param name="y">The Y value of the coordinate whose noise value is to be calculated.</param>
    /// <param name="initialOctaveFrequency">The frequency (coefficient) of the 2D coordinates for the first octave.</param>
    /// <param name="initialOctaveAmplitude">The amplitude of the first octave, that defines the rough shape of the noise.</param>
    /// <param name="octaves">The number of octaves that will be added together to generate noise. More octaves result in noise with finer details.</param>
    /// <param name="persistence">The persistence coefficient that will control the variation of amplitude for successive octaves. Higher values mean that secondary octaves contribute more to the final noise.</param>
    /// <param name="lacunarity">The persistence coefficient that will control the variation of frequency for successive octaves. Higher values mean that secondary octaves will increase details more sharply.</param>
    /// <returns>The computed fractal noise value. It is guaranteed to be in [0, initialOctaveAmplitude].</returns>
    /// <exception cref="InvalidOperationException">If there is no current seed value.</exception>
    private float OpenSimplex2DFractalNoise(float x, float y, float initialOctaveFrequency, float initialOctaveAmplitude, uint octaves, float persistence, float lacunarity)
    {
        // Generate fractal noise by adding octaves of OpenSimplex noise
        float totalAmplitude = 0;
        float totalMaximumAmplitude = 0;
        float octaveFrequency = initialOctaveFrequency;
        float octaveAmplitude = initialOctaveAmplitude;
        for (uint i = 0; i < octaves; ++i)
        {
            totalAmplitude += OpenSimplex2DNoise(x * octaveFrequency, y * octaveFrequency) * octaveAmplitude;

            totalMaximumAmplitude += octaveAmplitude;
            octaveAmplitude *= persistence;
            octaveFrequency *= lacunarity;
        }

        return totalAmplitude / totalMaximumAmplitude * initialOctaveAmplitude;
    }

    /// <summary>
    /// Calculates the OpenSimplex 2D noise value for the specified 2D coordinates.
    /// </summary>
    /// <param name="x">The X value of the coordinate whose noise value is to be calculated.</param>
    /// <param name="y">The Y value of the coordinate whose noise value is to be calculated.</param>
    /// <returns>The computed OpenSimplex 2D noise value.</returns>
    /// <exception cref="InvalidOperationException">If there is no current seed value.</exception>
    private float OpenSimplex2DNoise(float x, float y)
    {
        float value = 0;

        // Make sure the data dependent on the seed is okay
        UpdateOpenSimplex2DNoiseSeedPermutations();

        float s = 0.3660254180431365966796875f * (x + y);
        float xs = x + s, ys = y + s;
        int xsb = FastFloor(x + s), ysb = FastFloor(y + s);
        float xsi = xs - xsb, ysi = ys - ysb;

        // Index to point list
        int a = (int)(xsi + ysi);
        int index =
            (a << 2) |
            (int)(xsi - ysi / 2 + 1 - a / 2.0) << 3 |
            (int)(ysi - xsi / 2 + 1 - a / 2.0) << 4;

        float ssi = (xsi + ysi) * -0.211324870586395263671875f;
        float xi = xsi + ssi, yi = ysi + ssi;

        // Point contributions
        for (int i = 0; i < 4; ++i)
        {
            try
            {
                var c = simplexNoiseLUT[index + i];

                float dx = xi + c.gradient.Dx, dy = yi + c.gradient.Dy;
                float attn = 2.0f / 3.0f - dx * dx - dy * dy;

                int pxm = (xsb + c.Xsv) & NumberOfSimplexPermutationsMask;
                int pym = (ysb + c.Ysv) & NumberOfSimplexPermutationsMask;
                var grad = simplexPermutatedGradients[simplexPermutations[pxm] ^ pym];
                float extrapolation = grad.Dx * dx + grad.Dy * dy;

                attn *= attn;
                value += attn * attn * extrapolation;
            }
            catch (IndexOutOfRangeException)
            {
                // Assume that invalid indexes signal points with
                // negligible contributions. This happens if attn is < 0
                // and for very high coordinates
            }
        }

        return value;
    }

    /// <summary>
    /// Calculates the OpenSimplex noise permutation tables for the current seed value.
    /// If the permutation tables are already calculated, this method does nothing.
    /// </summary>
    /// <param name="seed">The seed value to use.</param>
    /// <exception cref="InvalidOperationException">If there is no current seed value.</exception>
    private void UpdateOpenSimplex2DNoiseSeedPermutations()
    {
        // Bail out if already up to date
        if (areSimplexSeedPermutationsUpToDate)
            return;

        if (!_terrainSeed.HasValue)
            throw new InvalidOperationException("No seed provided");

        simplexPermutations = new short[NumberOfSimplexPermutationsMask + 1];
        simplexPermutatedGradients = new SimplexPointGradients[NumberOfSimplexPermutationsMask + 1];
        short[] source = new short[NumberOfSimplexPermutationsMask + 1];

        for (short i = 0; i < source.Length; i++)
            source[i] = i;

        for (int i = NumberOfSimplexPermutationsMask; i >= 0; i--)
        {
            // Linear congruential PRNG
            _terrainSeed = _terrainSeed * 6364136223846793005L + 1442695040888963407L;

            int r = (int)((_terrainSeed + 31) % (i + 1));
            if (r < 0)
                r += (i + 1);

            simplexPermutations[i] = source[r];
            simplexPermutatedGradients[i] = simplexGradients[simplexPermutations[i]];
            source[r] = source[i];
        }

        areSimplexSeedPermutationsUpToDate = true;
    }

    /// <summary>
    /// Computes the floor function for a double value with better performance than using Mathf methods.
    /// </summary>
    /// <param name="x">The double value to compute its floor function result.</param>
    /// <returns>The result of the floor function for the double value
    /// (i.e. the next integer that is less or equal than the specified double value).</returns>
    private int FastFloor(double x)
    {
        // Shift negative values to positive so we get the rounding behavior
        // we want for negative values
        return (int)((uint)(x + 2147483648) - 2147483648);
    }

    private float fastAbs(float x)
    {
        // Discard sign bit of IEEE 754 representation
        return (float)BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(x) & 0x7FFFFFFFFFFFFFFF);
    }

    /// <summary>
    /// Represents a point in a OpenSimplex 2D noise lattice.
    /// </summary>
    private struct SimplexLatticePoint
    {
        public int Xsv { get; }
        public int Ysv { get; }
        public SimplexPointGradients gradient { get; }

        public SimplexLatticePoint(int xsv, int ysv)
        {
            float ssv = (xsv + ysv) * -0.211324870586395263671875f;
            Xsv = xsv; Ysv = ysv;
            gradient = new SimplexPointGradients
            {
                Dx = -xsv - ssv,
                Dy = -ysv - ssv
            };
        }
    }

    /// <summary>
    /// Contains a pair of numbers, each number being the gradient (i.e. slope) of a
    /// mathematical function in a 2D point.
    /// </summary>
    private struct SimplexPointGradients
    {
        public float Dx { get; set; }
        public float Dy { get; set; }
    }
}
