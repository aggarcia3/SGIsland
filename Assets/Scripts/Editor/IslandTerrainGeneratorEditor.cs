using UnityEditor;
using UnityEngine;

namespace SGIsland.TerrainGeneration
{
    /// <summary>
    /// Populates the inspector GUI for the <see cref="IslandTerrainGenerator"/> script component.
    /// </summary>
    [CustomEditor(typeof(IslandTerrainGenerator))]
    public class IslandTerrainGeneratorEditor : Editor
    {
        private SerializedProperty islandWaterProperty, islandBoundsProperty, playerProperty, starsProperty, mainNoiseGeneratorProperty,
            secondaryNoiseGeneratorProperty,  maximumTerrainAmplitudeProperty, terrainNoiseOctavesProperty, terrainNoiseFrequencyProperty,
            terrainNoisePersistenceProperty, terrainNoiseLacunarityProperty, islandRadiusVarianceProperty, islandShorelineLengthProperty,
            minimumHeightAboveSeaProperty, terrainLayerTextureSizeProperty;

        private long terrainSeed;

        private void OnEnable()
        {
            islandWaterProperty = serializedObject.FindProperty("islandWater");
            islandBoundsProperty = serializedObject.FindProperty("islandBounds");
            playerProperty = serializedObject.FindProperty("player");
            starsProperty = serializedObject.FindProperty("stars");
            mainNoiseGeneratorProperty = serializedObject.FindProperty("_mainNoiseGenerator");
            secondaryNoiseGeneratorProperty = serializedObject.FindProperty("_secondaryNoiseGenerator");
            maximumTerrainAmplitudeProperty = serializedObject.FindProperty("_maximumTerrainAmplitude");
            terrainNoiseOctavesProperty = serializedObject.FindProperty("_terrainNoiseOctaves");
            terrainNoiseFrequencyProperty = serializedObject.FindProperty("_terrainNoiseFrequency");
            terrainNoisePersistenceProperty = serializedObject.FindProperty("_terrainNoisePersistence");
            terrainNoiseLacunarityProperty = serializedObject.FindProperty("_terrainNoiseLacunarity");
            islandRadiusVarianceProperty = serializedObject.FindProperty("_islandRadiusVariance");
            islandShorelineLengthProperty = serializedObject.FindProperty("_islandShorelineLength");
            minimumHeightAboveSeaProperty = serializedObject.FindProperty("_minimumHeightAboveSea");
            terrainLayerTextureSizeProperty = serializedObject.FindProperty("_terrainLayerTextureSize");
        }

        public override void OnInspectorGUI()
        {
            IslandTerrainGenerator targetIslandTerrainGenerator = (IslandTerrainGenerator)serializedObject.targetObject;

            serializedObject.Update();

            EditorGUILayout.PropertyField(islandWaterProperty, new GUIContent("Water"));
            EditorGUILayout.PropertyField(islandBoundsProperty, new GUIContent("Island Bounds"));
            EditorGUILayout.PropertyField(playerProperty, new GUIContent("Player"));
            EditorGUILayout.PropertyField(starsProperty, new GUIContent("Stars"));
            terrainSeed = EditorGUILayout.LongField("Terrain Seed", terrainSeed);
            EditorGUILayout.PropertyField(mainNoiseGeneratorProperty, new GUIContent("Main Noise Generator"));
            EditorGUILayout.PropertyField(secondaryNoiseGeneratorProperty, new GUIContent("Secondary Noise Generator"));
            EditorGUILayout.Slider(maximumTerrainAmplitudeProperty, 0, 1, "Maximum Terrain Amplitude");
            EditorGUILayout.IntSlider(terrainNoiseOctavesProperty, 1, 32, "Terrain Noise Octaves");
            EditorGUILayout.Slider(terrainNoiseFrequencyProperty, 0.1f, 64, "Terrain Noise Frequency");
            EditorGUILayout.Slider(terrainNoisePersistenceProperty, 0, 1, "Terrain Noise Persistence");
            EditorGUILayout.Slider(terrainNoiseLacunarityProperty, 0, 8, "Terrain Noise Lacunarity");
            EditorGUILayout.Slider(islandRadiusVarianceProperty, 0.1f, 1, "Island Radius Variance");
            EditorGUILayout.Slider(islandShorelineLengthProperty, 0.1f, 1, "Island Shoreline Length");
            EditorGUILayout.Slider(minimumHeightAboveSeaProperty, 0.1f, 1, "Minimum Land Height Above Sea Level");
            EditorGUILayout.PropertyField(terrainLayerTextureSizeProperty, new GUIContent("Terrain Layer Textures Size"));

            serializedObject.ApplyModifiedProperties();

            // Generate the terrain if the user wants to
            if (GUILayout.Button("Generate Terrain"))
                targetIslandTerrainGenerator.GenerateTerrain(terrainSeed);
        }
    }
}
