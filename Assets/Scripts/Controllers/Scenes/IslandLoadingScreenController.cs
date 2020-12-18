using SGIsland.TerrainGeneration;
using System;
using UnityEngine;
using UnityEngine.UI;
using static SGIsland.TerrainGeneration.IslandTerrainGenerator;

namespace SGIsland.Controllers.Scenes
{
    public class IslandLoadingScreenController : MonoBehaviour
    {
        [SerializeField]
        private string islandTerrainTag = "IslandTerrain";
        [SerializeField]
        private Slider progressBar;
        [SerializeField]
        private Text generationOperationText;
        [SerializeField]
        [Min(1)]
        private uint targetFramesPerSecond = 10;
        /// <summary>
        /// The value used to smooth out frame delta time variations with a exponential moving average.
        /// Higher values make the moving average sharper, "forgetting" older values quicker, while
        /// lower values make it smoother, giving more weight to the past values.
        /// A value of 1 is equal to no smoothing at all, and results in higher FPS, because individual
        /// frame delta time fluctuations are taken into account more.
        /// </summary>
        [SerializeField]
        [Range(0, 1)]
        private float averageDeltaTimeSharpnessFactor = 0.33f;

        private float targetDeltaTime;
        private float? deltaTimeMovingAverage;

        private void OnValidate()
        {
            if (!progressBar || !generationOperationText)
                throw new ArgumentException("Both progress bar and generation operation text objects must be not null");

            targetDeltaTime = 1.0f / targetFramesPerSecond;
        }

        private void OnEnable()
        {
            var gameController = GameObject.FindGameObjectWithTag("GameController").GetComponent<GameController>();
            var terrainGenerator = GameObject.FindGameObjectWithTag(islandTerrainTag).GetComponent<IslandTerrainGenerator>();
            uint totalWorkUnits = terrainGenerator.GetTerrainGenerationTotalWorkUnits();
            uint workUnitsDone = 0;

            // Calculate the delta time according to the target FPS
            targetDeltaTime = 1.0f / targetFramesPerSecond;

            // Prepare the terrain generation operation and set the callback
            var terrainGenerationCoroutine = terrainGenerator.GenerateTerrain(gameController.CurrentSeed,
                delegate(TerrainGenerationOperation generationOperation)
                {
                    progressBar.value = (float)++workUnitsDone / totalWorkUnits;
                    generationOperationText.text = generationOperation.GetUserFriendlyName();

                    if (workUnitsDone >= totalWorkUnits)
                    {
                        // Reset the time scale used to control terrain generation work unit frequency,
                        // as the physics system will now use it
                        Time.timeScale = 1;

                        // Signal the end of the generation operation to the game controller
                        gameController.IslandGenerationEnd();
                    }
                }
            );

            // Now start the actual terrain generation
            StartCoroutine(terrainGenerationCoroutine);
        }

        private void Update()
        {
            float currentDeltaTime = Time.deltaTime;

            // Update the exponential moving average of frame delta times
            deltaTimeMovingAverage = deltaTimeMovingAverage.HasValue ?
                averageDeltaTimeSharpnessFactor * currentDeltaTime + (1 - averageDeltaTimeSharpnessFactor) * deltaTimeMovingAverage :
                currentDeltaTime;

            // Make the terrain generation faster or slower according to how big the target
            // delta time is relative to the moving average. We use the moving average to
            // smooth out delta time fluctuations
            Time.timeScale *= targetDeltaTime / deltaTimeMovingAverage.Value;
        }
    }
}
