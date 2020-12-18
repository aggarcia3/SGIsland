using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SGIsland.Controllers.Scenes
{
    /// <summary>
    /// Manages the high level game flow, at the request of more concrete scene controller scripts,
    /// so this controller contains inter-scene game state and the scene transition logic. Due to
    /// how the scenes are loaded, a <see cref="GameObject"/> with a "GameController" tag that
    /// contains an instance of this class is always available.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        [SerializeField]
        private string mainMenuSceneName = "MainMenu";
        [SerializeField]
        private string islandLoadingScreenSceneName = "IslandLoadingScreen";
        [SerializeField]
        private string islandSceneName = "Island";

        /// <summary>
        /// The current seed of the island, either to be generated or already
        /// generated. This is not initialized to a proper value until the
        /// loading screen is activated.
        /// </summary>
        public long CurrentSeed { get; private set; }

        private void Awake()
        {
            // This is the first script code ever executed in the game.
            // Perform an initial transition to the main menu, after setting some
            // properties

            // Set target framerate to the current refresh rate, as there is no
            // point in higher framerates for UI. This may save power
            Application.targetFrameRate = Screen.currentResolution.refreshRate;

            // Make sure the cursor is visible
            Cursor.visible = true;

            // Never destroy this game object
            DontDestroyOnLoad(gameObject);

            // Now load the main menu scene additively
            LoadScene(mainMenuSceneName);
        }

        /// <summary>
        /// Executes the high-level operations needed to start generating a new island.
        /// </summary>
        /// <param name="seed">The seed to use to generate the island.</param>
        public void GenerateIsland(long seed)
        {
            // Hide cursor and lock it to the center of the game window
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // Store the current seed
            CurrentSeed = seed;

            // Now load the loading screen, which will take care of the rest
            // via its scene controller. Cleanup the then unused assets after
            // the loading screen is done loading
            LoadScene(islandLoadingScreenSceneName, LoadSceneMode.Single, delegate
            {
                Debug.Log("Loading screen shown. Cleaning up unused assets...");

                Resources.UnloadUnusedAssets().completed += delegate
                {
                    Debug.Log("Loading island scene...");

                    LoadScene(islandSceneName, LoadSceneMode.Additive, delegate
                    {
                        Debug.Log("Island loaded. Activating loading screen game objects");

                        // Now both the island and loading screen are done loading.
                        // Signal the loading screen that it may proceed via game object activation
                        ActivateRootGameObjectsInScene(islandLoadingScreenSceneName);
                    });
                };
            });
        }

        /// <summary>
        /// Executes the high-level operations needed to show the just generated island to the player,
        /// and starting gameplay.
        /// </summary>
        public void IslandGenerationEnd()
        {
            Scene loadingScreenScene = SceneManager.GetSceneByName(islandLoadingScreenSceneName);
            Scene islandScene = SceneManager.GetSceneByName(islandSceneName);

            // Check that the island is actually being loaded
            if (loadingScreenScene.IsValid() && islandScene.IsValid())
            {
                Debug.Log("Generated island, activating island scene game objects");

                // Now activate the island game objects, so the gameplay starts
                ActivateRootGameObjectsInScene(islandScene);

                // Unload the loading screen and free memory of unused assets
                var unloadOperation = SceneManager.UnloadSceneAsync(islandLoadingScreenSceneName);
                unloadOperation.completed += delegate
                {
                    Resources.UnloadUnusedAssets().completed += delegate
                    {
                        // Reset target framerate to Unity's default, which is the most performance
                        // that makes sense
                        Application.targetFrameRate = -1;

                        Debug.Log("Unused assets cleanup complete");
                    };
                };
            }
            else
            {
                Debug.LogWarning("IslandGenerationEnd called while the island loading screen scene and/or island scene were not loaded");
            }
        }

        /// <summary>
        /// Facade around Unity <see cref="SceneManager"/> for loading scenes with less boilerplate code.
        /// The new scene will be the active scene, where game objects will be created by default.
        /// </summary>
        /// <param name="sceneName">The name of the scene to load.</param>
        /// <param name="successfulLoadAction">An action that will be executed when the scene loads successfully.</param>
        private void LoadScene(string sceneName, LoadSceneMode loadMode = LoadSceneMode.Single, Action<AsyncOperation> successfulLoadAction = null)
        {
            // Load the scene
            SceneManager.LoadSceneAsync(sceneName, loadMode).completed += delegate(AsyncOperation asyncOperation)
            {
                Scene targetScene = SceneManager.GetSceneByName(sceneName);

                if (!targetScene.IsValid())
                    throw new ArgumentException($"The scene of path {sceneName} could not be loaded");

                // Make sure the new scene is active, in case it was loaded additively
                SceneManager.SetActiveScene(targetScene);

                // Execute the successful load action
                successfulLoadAction?.Invoke(asyncOperation);
            };
        }

        /// <summary>
        /// Activates all the root game objects in a scene. This will also activate their descendants
        /// if they are locally activated.
        /// </summary>
        /// <param name="scene">The scene in which the root game objects should be activated.</param>
        private void ActivateRootGameObjectsInScene(Scene scene)
        {
            foreach (GameObject sceneGameObject in scene.GetRootGameObjects())
            {
                sceneGameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Activates all the root game objects in a scene. This will also activate their descendants
        /// if they are locally activated.
        /// </summary>
        /// <param name="sceneName">The name of the scene in which the root game objects should be activated.</param>
        private void ActivateRootGameObjectsInScene(string sceneName)
        {
            ActivateRootGameObjectsInScene(SceneManager.GetSceneByName(sceneName));
        }
    }
}
