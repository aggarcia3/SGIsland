using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    private SerializedProperty maximumTerrainAmplitudeProperty, terrainNoiseOctavesProperty, terrainNoiseFrequencyProperty,
        terrainNoisePersistenceProperty, terrainNoiseLacunarityProperty, terrainLayerTextureSizeProperty;

    private void OnEnable()
    {
        maximumTerrainAmplitudeProperty = serializedObject.FindProperty("_maximumTerrainAmplitude");
        terrainNoiseOctavesProperty = serializedObject.FindProperty("_terrainNoiseOctaves");
        terrainNoiseFrequencyProperty = serializedObject.FindProperty("_terrainNoiseFrequency");
        terrainNoisePersistenceProperty = serializedObject.FindProperty("_terrainNoisePersistence");
        terrainNoiseLacunarityProperty = serializedObject.FindProperty("_terrainNoiseLacunarity");
        terrainLayerTextureSizeProperty = serializedObject.FindProperty("_terrainLayerTextureSize");
    }

    public override void OnInspectorGUI()
    {
        TerrainGenerator targetTerrainGenerator = (TerrainGenerator)serializedObject.targetObject;
        long currentSeed = 0;
        try
        {
            currentSeed = targetTerrainGenerator.TerrainSeed;
        }
        catch (InvalidOperationException) { }

        serializedObject.Update();

        targetTerrainGenerator.TerrainSeed = EditorGUILayout.LongField("Terrain Seed", currentSeed);
        EditorGUILayout.Slider(maximumTerrainAmplitudeProperty, 0, 1, "Maximum Terrain Amplitude");
        EditorGUILayout.IntSlider(terrainNoiseOctavesProperty, 1, 32, "Terrain Noise Octaves");
        EditorGUILayout.Slider(terrainNoiseFrequencyProperty, 1, 64, "Terrain Noise Frequency");
        EditorGUILayout.Slider(terrainNoisePersistenceProperty, 0, 1, "Terrain Noise Persistence");
        EditorGUILayout.Slider(terrainNoiseLacunarityProperty, 0, 8, "Terrain Noise Lacunarity");
        EditorGUILayout.PropertyField(terrainLayerTextureSizeProperty, new GUIContent("Terrain Layer Textures Size"));

        // Regenerate the terrain right now
        if (serializedObject.hasModifiedProperties || targetTerrainGenerator.TerrainSeed != currentSeed)
        {
            serializedObject.ApplyModifiedProperties();
            targetTerrainGenerator.GenerateTerrain();
        }
    }
}
