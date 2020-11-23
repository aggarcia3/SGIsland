using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Generates a realistic looking procedural island terrain on the fly via coherent noise.
/// </summary>
[RequireComponent(typeof(Terrain))]
[RequireComponent(typeof(I2DCoherentNoiseGenerator))]
public class IslandTerrainGenerator : MonoBehaviour
{
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
    private float _islandRadiusVariance;
    [SerializeField]
    private float _islandShorelineLength;
    [SerializeField]
    private float _minimumHeightAboveSea;
    [SerializeField]
    private Vector2Int _terrainLayerTextureSize;

    /// <summary>
    /// The maximum height of the terrain, relative to the maximum height of the terrain mesh in world units.
    /// This changes the amplitude of the added values of several octaves of OpenSimplex noise.
    /// </summary>
    public float MaximumTerrainAmplitude
    {
        get => _maximumTerrainAmplitude;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("The maximum height is NaN or infinite");

            _maximumTerrainAmplitude = value;
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
            _terrainNoiseOctaves = value;
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
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("The maximum height is NaN or infinite");

            _terrainNoiseFrequency = value;
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
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("The maximum height is NaN or infinite");

            _terrainNoisePersistence = value;
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
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentException("The maximum height is NaN or infinite");

            _terrainNoiseLacunarity = value;
        }
    }

    /// <summary>
    /// Determines the minimum island radius that will result after perturbating the perfect island circle,
    /// as a multiplier for the maximum island radius (determined by terrain size). Lower values make the radius
    /// vary more and result in smaller islands with more shoreline features.
    /// </summary>
    public float IslandRadiusVariance
    {
        get => _islandRadiusVariance;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                throw new ArgumentException("The radius variance is NaN, infinite or outside of [0, 1]");

            _islandRadiusVariance = value;
        }
    }

    /// <summary>
    /// Determines the length of the shoreline, as a multiplier for the difference between maximum and minimum island radius.
    /// A longer shoreline length will make the island terrain transition more smoothly between sea level and interior points,
    /// but will also "erode" the interior points height. Lower values preserve more the interior point length, but may generate
    /// more abrupt cliffs.
    /// </summary>
    public float IslandShorelineLength
    {
        get => _islandShorelineLength;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                throw new ArgumentException("The shoreline length is NaN, infinite or outside of [0, 1]");

            _islandShorelineLength = value;
        }
    }

    /// <summary>
    /// This value is used as a multiplier for the maximum terrain amplitude to compute the minimum height (terrain amplitude)
    /// at points that should be in land, above sea level.
    /// </summary>
    public float MinimumHeightAboveSea
    {
        get => _minimumHeightAboveSea;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                throw new ArgumentException("The minimum height above the sea is NaN, infinite or outside of [0, 1]");

            _minimumHeightAboveSea = value;
        }
    }

    private void OnValidate()
    {
        if (_terrainLayerTextureSize.x < 16 || _terrainLayerTextureSize.x > 4096 || _terrainLayerTextureSize.y < 16 || _terrainLayerTextureSize.y > 4096)
            throw new ArgumentException("A coordinate of the terrain layer texture size is either too big or small");
    }

    /// <summary>
    /// Procedurally populates the sibling Terrain component with terrain features.
    /// This operation may take a considerable amount of time, and as such it may
    /// be executed in a coroutine.
    /// </summary>
    /// <param name="seed">The seed that will be used to generate the terrain. Same seeds generate the same terrain, but not vice-versa.</param>
    /// <param name="workUnits">This parameter controls after how many units of work this method will yield back to its caller.
    /// Lower values make this method end faster, but also hang the main thread more.</param>
    public IEnumerator GenerateTerrain(long seed, uint workUnits)
    {
        uint workDone = 0;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        print("Generating terrain...");

        Terrain terrain = gameObject.GetComponent<Terrain>();
        I2DCoherentNoiseGenerator noiseGenerator = gameObject.GetComponent<I2DCoherentNoiseGenerator>();
        TerrainData terrainData = terrain.terrainData;

        int terrainLayerTextureSizeOffByOneX = _terrainLayerTextureSize.x - 1;
        int terrainLayerTextureSizeOffByOneY = _terrainLayerTextureSize.y - 1;
        float halfTerrainLayerTextureOffByOneWidth = (float)terrainLayerTextureSizeOffByOneX / 2;
        float halfTerrainLayerTextureOffByOneHeight = (float)terrainLayerTextureSizeOffByOneY / 2;

        // An ideal (zero noise) island has a perfect circle shape with a certain radius.
        // However, for a natural look, the minimum radius of an island can be incremented
        // in [0, islandRadiusDifference] via coherent noise for each point
        float maximumIslandRadius = Math.Min(terrainData.size.x, terrainData.size.z) / 2;
        float minimumIslandRadius = maximumIslandRadius * _islandRadiusVariance;
        float islandRadiusDifference = maximumIslandRadius - minimumIslandRadius;
        float shorelineLength = islandRadiusDifference * _islandShorelineLength;
        float minimumLandHeightAboveSea = _maximumTerrainAmplitude * _minimumHeightAboveSea;
        float seaHeight = minimumLandHeightAboveSea / 4 * terrainData.heightmapScale.y;
        // Squared versions of the above variables to avoid computing sqrt
        float squaredMinimumIslandRadius = minimumIslandRadius * minimumIslandRadius;
        float squaredIslandRadiusDifference = islandRadiusDifference * islandRadiusDifference;
        float squaredShorelineLength = shorelineLength * shorelineLength;

        // Make sure the position, rotation and scale are alright
        // (it is assumed later that the island center is at (0, 0, 0) to simplify computations)
        terrain.transform.position = new Vector3(-terrainData.size.x / 2, 0, -terrainData.size.z / 2);
        terrain.transform.rotation = Quaternion.identity;
        terrain.transform.localScale = Vector3.one;

        // Make sure the position and rotation of the water for the island is okay
        try
        {
            GameObject islandWater = GameObject.FindGameObjectWithTag("IslandWater");
            islandWater.transform.position = new Vector3(0, seaHeight, 0);
            islandWater.transform.rotation = Quaternion.identity;
        } catch (UnityException) { }

        // Make sure the basemap distance is appropriate for the maximum height of the terrain
        // (so if the player looks down when in the top of a mountain things look okay)
        terrain.basemapDistance = terrainData.heightmapScale.y / 2;

        // Compress the hole texture
        terrainData.enableHolesTextureCompression = true;

        // Compute the height points for the terrain heightmap
        int heightMapResolution = terrainData.heightmapResolution;
        float[,] terrainHeightPoints = new float[heightMapResolution, heightMapResolution];
        for (int x = 0; x < heightMapResolution; ++x)
        {
            float u = (float)x / heightMapResolution;

            for (int y = 0; y < heightMapResolution; ++y)
            {
                float v = (float)y / heightMapResolution;

                // Calculate the base terrain height
                float baseHeight = noiseGenerator.FractalNoise2D(
                    seed, u, v,
                    _terrainNoiseFrequency, _maximumTerrainAmplitude, _terrainNoiseOctaves,
                    _terrainNoisePersistence, _terrainNoiseLacunarity
                );

                // Calculate a [0, 1] noise value that indicates how much should the radius
                // of the perfect island circle be perturbed
                float islandRadiusPerturbation = noiseGenerator.FractalNoise2D(seed, u, v, 1, 1, 4, 0.5f, 4);

                // Compute the distance of the current world coordinates to the center of the island
                // and then use the perturbed circle radius to see how far away the point is from the sea
                float cathetusX = (u - 0.5f) * terrainData.size.x, cathetusY = (v - 0.5f) * terrainData.size.z;
                float squaredCenterDistance = cathetusX * cathetusX + cathetusY * cathetusY;
                float squaredPerturbedIslandRadius = squaredMinimumIslandRadius + islandRadiusPerturbation * squaredIslandRadiusDifference;
                // 0 at perturbed circle (shoreline limit), < 0 inside the island, > 0 at the sea
                float squaredShorelineDistance = squaredCenterDistance - squaredPerturbedIslandRadius;

                // Sigmoid function whose values are clamped to [0, 1], 0 meaning this point should be at sea level and 1 meaning
                // it is a point that is fully in land. This function has a relatively abrupt transition, but it looks okay:
                // hilly terrain stays hilly, and makes an interesting cliff, and it can be made much less abrupt by increasing
                // the shoreline length, so it is flexible
                float interiorHeightInfluence = Mathf.Clamp01(
                    (float)(Math.Tanh(-2 * (squaredShorelineDistance / squaredShorelineLength) - 1) / 1.523188312 + 0.5)
                );

                // Make sure that fully interior points are at minimumLandHeightAboveSea. Then allow up to
                // 1 - minimumLandHeightAboveSea more height
                terrainHeightPoints[x, y] =
                    interiorHeightInfluence * (minimumLandHeightAboveSea + (1 - minimumLandHeightAboveSea) * baseHeight);
            }

            if (++workDone == workUnits)
            {
                workDone = 0;
                yield return null;
            }
        }
        terrainData.SetHeights(0, 0, terrainHeightPoints);

        // Generate terrain holes, so we have no terrain if its height is very similar to sea level
        int holeResolution = terrainData.holesResolution;
        bool[,] terrainHoles = new bool[holeResolution, holeResolution];
        for (int x = 0; x < holeResolution; ++x)
        {
            float u = (float)x / holeResolution;

            for (int y = 0; y < holeResolution; ++y)
            {
                // It is surface if and only if it is above sea level
                terrainHoles[y, x] = terrainData.GetInterpolatedHeight(u, (float)y / holeResolution) > 0;
            }

            if (++workDone == workUnits)
            {
                workDone = 0;
                yield return null;
            }
        }
        terrainData.SetHoles(0, 0, terrainHoles);

        // Generate the sand texture
        Texture2D sandTexture = new Texture2D(_terrainLayerTextureSize.x, _terrainLayerTextureSize.y, TextureFormat.ARGB32, true);
        uint sandRipples = (26 + (uint)seed % 21) & 0xFFFFFFFE; // Even numbers in [26, 46]
        for (int x = 0; x < _terrainLayerTextureSize.x; ++x)
        {
            // Normalize the X and Y texture coordinates to U and V so they are in [0, 1].
            // This is intentionally of by one because the pixel coordinates start at 0
            float u = (float)x / terrainLayerTextureSizeOffByOneX;

            for (int y = 0; y < _terrainLayerTextureSize.y; ++y)
            {
                float v = (float)y / terrainLayerTextureSizeOffByOneY;

                // The characteristic (ideal) sand texture function is cos((sandRipples * (u + v)) * PI),
                // that has sandRipples "ups" and "downs" in a diagonal (cosine period is 2 * PI, its maximum
                // absolute value is at 0 = 2 * PI and PI, and its minimum value at PI / 2 and
                // (3 * PI) / 4. If we didn't multiply the sum by sandRipples we would just go over a single
                // period of the cosine function and get a single "up" in the bottom-left and
                // top-right corners). For the characteristic function result to be tileable, sandRipples
                // needs to be even. Then that is distorted (phase shifted) by using coherent noise,
                // making sure that we both pairs of edges of the texture have the same coherent noise
                // coordinates so that we distort by the same amount on each pair of edges and the result is still
                // tileable
                float ut = fastAbs(u - 0.5f) / 0.5f, vt = fastAbs(v - 0.5f) / 0.5f;
                float distortion = noiseGenerator.Noise2D(seed, ut * 1.5f, vt * 1.5f);
                float rippleIntensity = 0.5f + 0.5f * Mathf.Cos((distortion + sandRipples * (u + v)) * Mathf.PI);

                sandTexture.SetPixel(x, y, new Color(
                    Mathf.LerpUnclamped(0.76f, 1, rippleIntensity),
                    Mathf.LerpUnclamped(0.7f, 0.94f, rippleIntensity),
                    Mathf.LerpUnclamped(0.5f, 0.79f, rippleIntensity),
                    0 // No smoothness, so we don't get specular highlights
                ));
            }

            if (++workDone == workUnits)
            {
                workDone = 0;
                yield return null;
            }
        }
        sandTexture.Apply();

        // Generate the dirt texture
        Texture2D dirtTexture = new Texture2D(_terrainLayerTextureSize.x, _terrainLayerTextureSize.y, TextureFormat.ARGB32, true);
        for (int x = 0; x < _terrainLayerTextureSize.x; ++x)
        {
            // Off by one so the edges match exactly
            float u = fastAbs(x - halfTerrainLayerTextureOffByOneWidth) / halfTerrainLayerTextureOffByOneWidth;

            for (int y = 0; y < _terrainLayerTextureSize.y; ++y)
            {
                float v = fastAbs(y - halfTerrainLayerTextureOffByOneHeight) / halfTerrainLayerTextureOffByOneHeight;
                float ut = noiseGenerator.Noise2D(seed, u * 512, v * 512);

                dirtTexture.SetPixel(x, y, new Color(
                    Mathf.LerpUnclamped(0.61f, 0.71f, ut),
                    Mathf.LerpUnclamped(0.47f, 0.57f, ut),
                    Mathf.LerpUnclamped(0.33f, 0.43f, ut),
                    0 // No smoothness, so we don't get specular highlights
                ));
            }

            if (++workDone == workUnits)
            {
                workDone = 0;
                yield return null;
            }
        }
        dirtTexture.Apply();

        // Generate the grass texture
        Texture2D grassTexture = new Texture2D(_terrainLayerTextureSize.x, _terrainLayerTextureSize.y, TextureFormat.ARGB32, true);
        for (int x = 0; x < _terrainLayerTextureSize.x; ++x)
        {
            // Off by one so the edges match exactly
            float u = fastAbs(x - halfTerrainLayerTextureOffByOneWidth) / halfTerrainLayerTextureOffByOneWidth;

            for (int y = 0; y < _terrainLayerTextureSize.y; ++y)
            {
                float v = fastAbs(y - halfTerrainLayerTextureOffByOneHeight) / halfTerrainLayerTextureOffByOneHeight;
                float ut = noiseGenerator.FractalNoise2D(seed, u, v, 256, 1, 4, 0.5f, 1.5f);

                grassTexture.SetPixel(x, y, new Color(
                    // Lagrange polynomials for RGB component interpolation between three base grass colors
                    0.38f * ut * ut - 0.81f * ut + 0.59f,
                    0.16f * ut * ut - 0.58f * ut + 0.64f,
                    -0.14f * ut * ut - 0.17f * ut + 0.34f,
                    0 // No smoothness, so we don't get specular highlights
                ));
            }

            if (++workDone == workUnits)
            {
                workDone = 0;
                yield return null;
            }
        }
        grassTexture.Apply();

        // Create the sand terrain layer
        TerrainLayer sandLayer = new TerrainLayer();
        sandLayer.metallic = 0.0f;
        sandLayer.diffuseTexture = sandTexture;
        sandLayer.tileSize = new Vector2(2.0f, 2.0f);

        // Create the dirt terrain layer
        TerrainLayer dirtLayer = new TerrainLayer();
        dirtLayer.metallic = 0.0f;
        dirtLayer.diffuseTexture = dirtTexture;
        dirtLayer.tileSize = new Vector2(0.5f, 0.5f);

        // Create the grass terrain layer
        TerrainLayer grassLayer = new TerrainLayer();
        grassLayer.metallic = 0.0f;
        grassLayer.diffuseTexture = grassTexture;
        grassLayer.tileSize = new Vector2(0.5f, 0.5f);

        // Assign the layers to the terrain data
        terrainData.terrainLayers = new TerrainLayer[] { sandLayer, dirtLayer, grassLayer };

        // Now change terrain texture depending on the terrain characteristics
        float[,,] terrainLayerAlphaMaps = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];
        for (int x = 0; x < terrainData.alphamapWidth; ++x)
        {
            float u = (float)x / terrainData.alphamapWidth;

            for (int y = 0; y < terrainData.alphamapHeight; ++y)
            {
                float height = terrainData.GetInterpolatedHeight(u, (float)y / terrainData.alphamapHeight) / terrainData.heightmapScale.y;

                terrainLayerAlphaMaps[y, x, 0] = Mathf.Lerp(1, 0, height / minimumLandHeightAboveSea);
                terrainLayerAlphaMaps[y, x, 2] = Mathf.Lerp(0, 1, height / minimumLandHeightAboveSea);
                terrainLayerAlphaMaps[y, x, 1] = 0;
            }

            if (++workDone == workUnits)
            {
                workDone = 0;
                yield return null;
            }
        }
        terrainData.SetAlphamaps(0, 0, terrainLayerAlphaMaps);

        stopwatch.Stop();
        print($"Terrain generated in {stopwatch.ElapsedMilliseconds} ms");
    }

    /// <summary>
    /// Computes the absolute value of a floating point number, with a performance that higher than that of <see cref="Mathf.Abs(float)"/>.
    /// </summary>
    /// <param name="x">The floating point to get its absolute value.</param>
    /// <returns>The absolute value of <paramref name="x"/>.</returns>
    private float fastAbs(float x)
    {
        // Discard sign bit of IEEE 754 representation
        return (float)BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(x) & 0x7FFFFFFFFFFFFFFF);
    }
}
