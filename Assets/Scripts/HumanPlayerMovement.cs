using Scripts.Util;
using System;
using UnityEngine;

namespace Scripts.Player
{
    /// <summary>
    /// Implements human player movement mechanics.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HumanPlayerMovement : MonoBehaviour
    {
        [SerializeField]
        private float gravityAcceleration = -9.807f; // m/s^2
        [SerializeField]
        private float gravityTerminalSpeed = -21.4f; // m/s
        [SerializeField]
        private bool isFemale = false;
        [SerializeField]
        private float walkSpeed = 1.4f; // m/s
        [SerializeField]
        [Range(0.1f, 1)]
        private float timeToFullRunIntensity = 0.5f; // s
        [SerializeField]
        private float runSpeed = 9.72f; // m/s
        [SerializeField]
        [Range(0, 1)]
        private float baseHorizontalEyeBobbingIntensity = 0.5f;
        [SerializeField]
        [Range(0, 1)]
        private float baseVerticalEyeBobbingIntensity = 0.25f;
        [SerializeField]
        [Range(0, 2)]
        private float baseEyeBobbingSpeed = 2;
        [SerializeField]
        [Range(0, 1)]
        private float minimumEyeBobbingIntensity = 0.2f;
        [SerializeField]
        private AudioClip exhaustedBreathingAudioClip;
        [SerializeField]
        [Range(0, 1)]
        private float maximumExhaustedBreathingAudioVolume = 0.25f;
        [SerializeField]
        [Range(0, 60)]
        private float timeUntilMaximumExhaustion = 30; // s
        [SerializeField]
        [Range(0, 1)]
        private float walkRestCoefficient = 0.75f;

        /// <summary>
        /// Sets whether the player is a male or female human being.
        /// </summary>
        public bool IsFemale
        {
            get => isFemale;
            set
            {
                baseExhaustedBreathingAudioPitch = value ? 1.2f : 0.95f;
                isFemale = value;
            }
        }

        private CharacterController characterController;
        private AudioSource exhaustedBreathingAudio;
        private GameObject playerCamera = null;
        private Collider worldBoundsCollider = null;
        private float baseExhaustedBreathingAudioPitch;

        private float currentGravitySpeed = 0;
        private Vector3 characterVelocity = Vector3.zero;
        private float eyeBobbingPhase = 0;
        private float previousRunIntensity = 0;
        private float timeRunning = 0;

        private void Start()
        {
            characterController = gameObject.GetComponent<CharacterController>();

            worldBoundsCollider = GameObject.FindGameObjectWithTag("IslandBoundsCollider").GetComponent<Collider>();

            try
            {
                playerCamera = GameObject.FindGameObjectWithTag("MainCamera");
            }
            catch (UnityException) { }

            IsFemale = isFemale;

            // Create the exhausted breathing component to use when running
            exhaustedBreathingAudio = gameObject.AddComponent<AudioSource>();
            exhaustedBreathingAudio.clip = exhaustedBreathingAudioClip;
            exhaustedBreathingAudio.loop = true;
            exhaustedBreathingAudio.bypassEffects = true;
            exhaustedBreathingAudio.bypassListenerEffects = true;
            exhaustedBreathingAudio.bypassReverbZones = true;
            exhaustedBreathingAudio.dopplerLevel = 0;
            exhaustedBreathingAudio.volume = 0;
            exhaustedBreathingAudio.pitch = baseExhaustedBreathingAudioPitch;
            exhaustedBreathingAudio.minDistance = 0;
            exhaustedBreathingAudio.maxDistance = float.MaxValue;
            exhaustedBreathingAudio.rolloffMode = AudioRolloffMode.Linear;
            exhaustedBreathingAudio.Play();
        }

        private void OnValidate()
        {
            if (gravityTerminalSpeed > 0)
                throw new ArgumentException("The gravity terminal speed must be negative (downwards)");

            IsFemale = isFemale;
        }

        private void Update()
        {
            // Calculate the fall speed
            float fallSpeed;
            if (!characterController.isGrounded)
            {
                float currentFallSpeed = characterController.velocity.y;
                currentGravitySpeed += currentFallSpeed < gravityTerminalSpeed ? 0 : gravityAcceleration * Time.deltaTime;
                fallSpeed = currentGravitySpeed;
            }
            else
            {
                // The previous movement has just touched the ground
                fallSpeed = 0;
                currentGravitySpeed = 0; // Reset to zero so next fall starts at 0
            }

            // Get movement and run intensities
            float rightMovementIntensity = Input.GetAxis("Movement X");
            float forwardMovementIntensity = Input.GetAxis("Movement Y");
            float runIntensity;
            if (forwardMovementIntensity > 0)
            {
                float rawRunAxis = Input.GetAxisRaw("Run");
                float runIntensityStep = Time.deltaTime / timeToFullRunIntensity;
                runIntensity = Mathf.Clamp01(previousRunIntensity - runIntensityStep + rawRunAxis * 2 * runIntensityStep);
            }
            else
            {
                runIntensity = 0;
            }

            previousRunIntensity = runIntensity;

            // Now get the movement speed
            float movementSpeed = Mathf.LerpUnclamped(walkSpeed, runSpeed, runIntensity);

            // TODO: rotate player along X and reduce height to radius according to fall phase
            // TODO: height and speed reduction to radius for swimming

            // Rotate player in Y according to horizontal look input (yaw)
            gameObject.transform.Rotate(0, Input.GetAxis("Look X") / Time.deltaTime, 0);

            // Move the character along the forward and right directions according to the input and where it is facing.
            // These direction vectors of the transform matrix already take into account rotation
            characterVelocity =
                gameObject.transform.forward * forwardMovementIntensity * movementSpeed * Time.deltaTime +
                gameObject.transform.right * rightMovementIntensity * movementSpeed * Time.deltaTime;
            characterVelocity[1] = fallSpeed * Time.deltaTime;

            // Do the actual movement
            Vector3 previousCharacterPosition = gameObject.transform.position;
            characterController.Move(characterVelocity);

            // Undo movement if the player is now outside of the world bounds
            if (!worldBoundsCollider.bounds.Contains(gameObject.transform.position))
                gameObject.transform.position = previousCharacterPosition;

            // If the movement was constrained by an obstacle at a side,
            // consider no movement intensity from now on
            if ((characterController.collisionFlags & CollisionFlags.Sides) != 0)
            {
                rightMovementIntensity = 0;
                forwardMovementIntensity = 0;
                runIntensity = 0;
            }

            // Update exhausted running audio pitch and volume
            timeRunning = Math.Max(timeRunning - (1 - Math.Max(forwardMovementIntensity, rightMovementIntensity) * walkRestCoefficient) * Time.deltaTime, 0);
            timeRunning = Mathf.Min(timeRunning + runIntensity * Time.deltaTime, timeUntilMaximumExhaustion);
            exhaustedBreathingAudio.volume = timeRunning / timeUntilMaximumExhaustion * maximumExhaustedBreathingAudioVolume;
            exhaustedBreathingAudio.pitch = baseExhaustedBreathingAudioPitch + 0.05f * exhaustedBreathingAudio.volume / maximumExhaustedBreathingAudioVolume;

            // Update the camera position and rotation if applicable
            if (playerCamera)
            {
                // Eye position
                Vector3 cameraPosition = gameObject.transform.position + characterController.center;
                cameraPosition += gameObject.transform.up * characterController.height / 2;

                // Camera rotation in X (pitch)
                // TODO
                Quaternion cameraRotation = gameObject.transform.rotation;

                // Take into account eye bobbing
                // Grounded intensity is maximum when at least two frames of gravity acceleration were not applied.
                // This cancels the bobbing effect when falling
                float eyeBobbingGroundedIntensity = Mathf.Max(1 - fallSpeed / (2 * gravityAcceleration * Time.deltaTime / Time.fixedDeltaTime), 0);
                float eyeBobbingIntensity = Mathf.Max(minimumEyeBobbingIntensity, runIntensity) * eyeBobbingGroundedIntensity;
                float horizontalEyeBobbingDisplacement = Mathf.Sin(eyeBobbingPhase - FastMathf.HALF_PI) * baseHorizontalEyeBobbingIntensity * eyeBobbingIntensity;
                float verticalEyeBobbingDisplacement = -Mathf.Sin(eyeBobbingPhase * 2 - FastMathf.HALF_PI) * baseVerticalEyeBobbingIntensity * eyeBobbingIntensity;
                cameraPosition += gameObject.transform.up * verticalEyeBobbingDisplacement + gameObject.transform.right * horizontalEyeBobbingDisplacement;
                // Update phase, and keep it in low values for maximum precision even for very long walks
                eyeBobbingPhase = (eyeBobbingPhase + Time.deltaTime * Mathf.PI * baseEyeBobbingSpeed * eyeBobbingIntensity) % FastMathf.PI_2;

                playerCamera.transform.position = cameraPosition;
                playerCamera.transform.rotation = cameraRotation;
            }
        }
    }
}
