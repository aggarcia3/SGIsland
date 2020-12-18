using System;
using UnityEngine;

namespace SGIsland.Controllers.Scenes
{
    /// <summary>
    /// Manages, at a high-level, interactions of the player with the main menu scene.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private static readonly System.Random prng = new System.Random();

        private long islandSeed;

        private byte[] longBuffer = new byte[8];

        public MainMenuController()
        {
            // Initialize the seed to a random value
            islandSeed = GenerateRandomLong();
        }

        /// <summary>
        /// Listener method called when the seed input field is updated to a new value.
        /// </summary>
        /// <param name="seed">The seed that the player provided.</param>
        public void OnSeedInputFieldUpdate(string seed)
        {
            // Use seed value directly if is is a long integer. If it is not,
            // then use the hash code of non-empty strings. If the string is empty,
            // generate a new random seed
            if (!long.TryParse(seed, out islandSeed))
                islandSeed = seed.Length > 0 ? seed.GetHashCode() : GenerateRandomLong();

            Debug.Log($"Island seed: {islandSeed}");
        }

        /// <summary>
        /// Listener method called when the player clicks the generate island button.
        /// Eventually, this initiates a scene transition.
        /// </summary>
        public void OnGenerateIslandButtonClick()
        {
            GameObject.FindGameObjectWithTag("GameController")
                .GetComponent<GameController>().GenerateIsland(islandSeed);
        }

        private long GenerateRandomLong()
        {
            prng.NextBytes(longBuffer);
            return BitConverter.ToInt64(longBuffer, 0);
        }
    }
}
