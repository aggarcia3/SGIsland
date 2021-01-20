using SGIsland.Util;
using System;
using UnityEngine;

namespace SGIsland.Player
{
    /// <summary>
    /// Implements human player movement mechanics.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class HumanPlayerMovement : MonoBehaviour
    {
        [SerializeField]
        private Camera playerCamera;
        [SerializeField]
        private Collider worldBounds;

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
        private float runSpeed = 9.5f; // m/s
        [SerializeField]
        [Range(0, 10)]
        private float airborneMovementSpeedMultiplier = 2.0f;
        [SerializeField]
        [Range(0, 1)]
        private float swimmingMovementSpeedMultiplier = 0.33f;
        [SerializeField]
        [Range(0, 1)]
        private float waterHeightReductionCoefficient = 0.11f;
        [SerializeField]
        private AudioClip leftFootstepAudioClip;
        [SerializeField]
        private AudioClip rightFootstepAudioClip;
        [SerializeField]
        private AudioClip swimmingStrokeAudioClip;
        [SerializeField]
        [Range(0, 1)]
        private float maximumMovementSoundVolume = 1;
        [SerializeField]
        [Range(0.5f, 1.5f)]
        private float footstepStrideLength = 0.95f;
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
        [SerializeField]
        [Range(0, 1)]
        private float minimumWindIntensity = 0.02f;
        [SerializeField]
        private AudioClip howlingWindAudioClip;

        /// <summary>
        /// Sets whether the player is a male or female human being.
        /// </summary>
        public bool IsFemale
        {
            get => isFemale;
            set
            {
                baseVocalizationPitch = value ? 1.2f : 0.95f;
                isFemale = value;
            }
        }

        private CharacterController characterController;
        private AudioSource[] footstepAudioSources;
        private AudioSource swimmingStrokeAudioSource;
        private AudioSource exhaustedBreathingAudio;
        private AudioSource howlingWindAudio;
        private float baseVocalizationPitch;
        private float originalCharacterControllerHeight;

        private float currentGravitySpeed = 0;
        private Vector3 characterVelocity = Vector3.zero;
        private float eyeBobbingPhase = 0;
        private float previousRunIntensity = 0;
        private float timeRunning = 0;
        private float footstepStrideDistance = 0;
        private int previousFootstepIndex = 0;
        private float inWaterIntensity = 0;
        private float previousInWaterIntensity = 0;

        private void Start()
        {
            characterController = GetComponent<CharacterController>();

            IsFemale = isFemale;

            // Store the original height of the character controller
            originalCharacterControllerHeight = characterController.height;

            // Create the footstep sound sources
            footstepAudioSources = new AudioSource[2];

            footstepAudioSources[0] = gameObject.AddComponent<AudioSource>();
            footstepAudioSources[0].clip = leftFootstepAudioClip;

            footstepAudioSources[1] = gameObject.AddComponent<AudioSource>();
            footstepAudioSources[1].clip = rightFootstepAudioClip;

            foreach (var audioSource in footstepAudioSources)
            {
                audioSource.loop = false;
                audioSource.bypassEffects = true;
                audioSource.bypassListenerEffects = true;
                audioSource.bypassReverbZones = true;
                audioSource.dopplerLevel = 0;
                audioSource.spatialBlend = 1;
                audioSource.minDistance = 3;
                audioSource.maxDistance = 25;
                audioSource.playOnAwake = false;
                audioSource.rolloffMode = AudioRolloffMode.Linear;
            }

            // Create swimming stroke sound source
            swimmingStrokeAudioSource = gameObject.AddComponent<AudioSource>();
            swimmingStrokeAudioSource.clip = swimmingStrokeAudioClip;
            swimmingStrokeAudioSource.loop = true;
            swimmingStrokeAudioSource.bypassEffects = true;
            swimmingStrokeAudioSource.bypassListenerEffects = true;
            swimmingStrokeAudioSource.bypassReverbZones = true;
            swimmingStrokeAudioSource.dopplerLevel = 0;
            swimmingStrokeAudioSource.spatialBlend = 1;
            swimmingStrokeAudioSource.minDistance = 6;
            swimmingStrokeAudioSource.maxDistance = 25;
            swimmingStrokeAudioSource.playOnAwake = false;
            swimmingStrokeAudioSource.rolloffMode = AudioRolloffMode.Linear;

            // Create the exhausted breathing sound source component to use when running
            exhaustedBreathingAudio = gameObject.AddComponent<AudioSource>();
            exhaustedBreathingAudio.clip = exhaustedBreathingAudioClip;
            exhaustedBreathingAudio.loop = true;
            exhaustedBreathingAudio.bypassEffects = true;
            exhaustedBreathingAudio.bypassListenerEffects = true;
            exhaustedBreathingAudio.bypassReverbZones = true;
            exhaustedBreathingAudio.dopplerLevel = 0;
            exhaustedBreathingAudio.volume = 0;
            exhaustedBreathingAudio.mute = true;
            exhaustedBreathingAudio.pitch = baseVocalizationPitch;
            exhaustedBreathingAudio.minDistance = 3;
            exhaustedBreathingAudio.maxDistance = 10;
            exhaustedBreathingAudio.rolloffMode = AudioRolloffMode.Linear;
            exhaustedBreathingAudio.playOnAwake = false;
            exhaustedBreathingAudio.Play();

            // Create the howling wind sound source
            howlingWindAudio = gameObject.AddComponent<AudioSource>();
            howlingWindAudio.clip = howlingWindAudioClip;
            howlingWindAudio.loop = true;
            howlingWindAudio.bypassEffects = true;
            howlingWindAudio.bypassListenerEffects = true;
            howlingWindAudio.bypassReverbZones = true;
            howlingWindAudio.dopplerLevel = 0;
            howlingWindAudio.volume = 1;
            howlingWindAudio.minDistance = 0;
            howlingWindAudio.maxDistance = 1;
            howlingWindAudio.spatialBlend = 1;
            howlingWindAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            howlingWindAudio.playOnAwake = false;
            howlingWindAudio.Play();
        }

        private void OnValidate()
        {
            if (gravityTerminalSpeed > 0)
                throw new ArgumentException("The gravity terminal speed must be negative (downwards)");

            // Apply any side effects of changing sex
            IsFemale = isFemale;

            if (!playerCamera || !worldBounds)
                throw new ArgumentException("One or both of the player camera or world bounds objects were null");
        }

        private void Update()
        {
            // Calculate the fall speed
            float fallSpeed;
            if (!characterController.isGrounded)
            {
                float currentFallSpeed = characterController.velocity.y;
                currentGravitySpeed += currentFallSpeed < gravityTerminalSpeed ? 0 : Physics.gravity.y * Time.deltaTime;
                fallSpeed = currentGravitySpeed;
            }
            else
            {
                // The previous movement has just touched the ground
                fallSpeed = 0;
                currentGravitySpeed = 0; // Reset to zero so next fall starts at 0
            }

            // Get movement, grounded and run intensities.
            // Grounded intensity is maximum when at least we reached a fraction of the terminal velocity
            float rightMovementIntensity = Input.GetAxis("Movement X");
            float forwardMovementIntensity = Input.GetAxis("Movement Y");
            float groundedIntensity = Mathf.LerpUnclamped(
                Mathf.Max(1 - fallSpeed / (gravityTerminalSpeed * 0.75f), 0),
                0, inWaterIntensity
            );
            float runIntensity;
            if (forwardMovementIntensity > 0 && previousInWaterIntensity <= inWaterIntensity)
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
            previousInWaterIntensity = inWaterIntensity;

            // Compute player look direction vector according to horizontal and vertical look input
            Vector3 lookDirection = transform.forward;
            Vector3 upContribution = transform.up * 0.01f * Input.GetAxis("Look Y") / Time.deltaTime;
            lookDirection += upContribution;
            lookDirection += transform.right * 0.01f * Input.GetAxis("Look X") / Time.deltaTime;

            // Do not allow extreme up or down angles (they look bad)
            float angleWithUp = Vector3.Angle(Vector3.up, lookDirection);
            if (angleWithUp < 15 || angleWithUp > 165)
                lookDirection -= upContribution;

            transform.localRotation = Quaternion.LookRotation(lookDirection);

            // Now get the movement speed. Take into account whether we are airborne or in water
            float movementSpeed = Mathf.LerpUnclamped(
                walkSpeed, Mathf.LerpUnclamped(runSpeed, runSpeed * swimmingMovementSpeedMultiplier, inWaterIntensity),
                runIntensity
            );
            movementSpeed += fallSpeed / gravityTerminalSpeed * airborneMovementSpeedMultiplier * movementSpeed;

            // Move the character along the forward and right directions according to the input and where it is facing.
            // These direction vectors of the transform matrix already take into account rotation.
            // In order to ignore whether we are looking up or down, the forward direction is projected to the XZ plane by
            // setting its Y coordinate to 0. Then it is normalized, as this operation may change the length
            Vector3 projectedForward = transform.forward;
            projectedForward[1] = 0;
            projectedForward.Normalize();

            characterVelocity =
                projectedForward * forwardMovementIntensity * movementSpeed * Time.deltaTime +
                transform.right * rightMovementIntensity * movementSpeed * Time.deltaTime;
            characterVelocity[1] = fallSpeed * Time.deltaTime;

            // Do the actual movement
            Vector3 previousCharacterPosition = transform.position;
            characterController.Move(characterVelocity);

            // Undo movement if the player is now outside of the world bounds
            if (!worldBounds.bounds.Contains(transform.position))
                transform.position = previousCharacterPosition;

            // If the movement was constrained by an obstacle at a side,
            // consider no movement intensity from now on
            if ((characterController.collisionFlags & CollisionFlags.Sides) != 0)
            {
                rightMovementIntensity = 0;
                forwardMovementIntensity = 0;
                runIntensity = 0;
            }

            // Compute movement intensity as the maximum absolute intensity of both movement directions
            float movementIntensity = Mathf.Max(FastMathf.Abs(forwardMovementIntensity), FastMathf.Abs(rightMovementIntensity));

            // Update exhausted running audio pitch and volume
            timeRunning = Mathf.Max(timeRunning - (1 - movementIntensity * walkRestCoefficient) * Time.deltaTime, 0);
            timeRunning = Mathf.Min(timeRunning + runIntensity * groundedIntensity * Time.deltaTime, timeUntilMaximumExhaustion);
            exhaustedBreathingAudio.volume = timeRunning / timeUntilMaximumExhaustion * maximumExhaustedBreathingAudioVolume;
            if (exhaustedBreathingAudio.volume > 0)
            {
                exhaustedBreathingAudio.pitch =
                    baseVocalizationPitch +
                    0.05f * exhaustedBreathingAudio.volume / maximumExhaustedBreathingAudioVolume;

                exhaustedBreathingAudio.mute = false;
            }
            else
            {
                exhaustedBreathingAudio.mute = true;
            }

            // Update total walk stride distance and the footstep sound index, playing it if necessary.
            // Higher run intensities make the stride distance increase slower, simulating a longer stride
            footstepStrideDistance += movementSpeed * groundedIntensity * movementIntensity * Time.deltaTime * ((1 - runIntensity) * 0.7f + 0.3f);

            // Check for walk stride cycle end. In that case, reset the distance and index
            int footstepIndex = FastMathf.FloorToInt(footstepStrideDistance / footstepStrideLength);
            if (footstepIndex >= footstepAudioSources.Length)
            {
                footstepStrideDistance = 0;
                footstepIndex = 0;
            }

            // The player has just used their other feet to complete a step. Play the appropriate sound
            float halfMovementSoundVolume = maximumMovementSoundVolume * 0.5f;
            if (footstepIndex != previousFootstepIndex)
            {
                footstepAudioSources[footstepIndex].volume = halfMovementSoundVolume + halfMovementSoundVolume * runIntensity;
                footstepAudioSources[footstepIndex].Play();
            }

            previousFootstepIndex = footstepIndex;

            // Play swimming stroke sound effect with the appropriate volume if swimming
            float previousSwimmingStrokeSoundVolume = swimmingStrokeAudioSource.volume;
            float swimmingStrokeSoundVolume = inWaterIntensity * (halfMovementSoundVolume * movementIntensity + halfMovementSoundVolume * runIntensity);

            swimmingStrokeAudioSource.volume = swimmingStrokeSoundVolume;
            if (swimmingStrokeSoundVolume > 0 && previousSwimmingStrokeSoundVolume <= 0)
            {
                swimmingStrokeAudioSource.Play();
            }

            if (swimmingStrokeSoundVolume <= 0)
            {
                swimmingStrokeAudioSource.Stop();
            }

            // Update howling wind audio volume by changing the minimum distance.
            // Unity will take care of calculating the volume following a logarithmic smoothing function
            howlingWindAudio.minDistance = Mathf.LerpUnclamped(
                minimumWindIntensity + Mathf.Max(1 - groundedIntensity - 0.33f, 0) / 0.66f * (1 - minimumWindIntensity),
                0, inWaterIntensity
            );

            // Update the camera position and rotation.
            // Eye position
            Vector3 cameraPosition = transform.position + characterController.center;
            cameraPosition += transform.up * characterController.height / 2;

            // Take into account eye bobbing.
            // Grounded intensity is used to cancel the bobbing effect when falling
            float eyeBobbingIntensity = Mathf.Max(minimumEyeBobbingIntensity, runIntensity) * groundedIntensity;
            float horizontalEyeBobbingDisplacement = Mathf.Sin(eyeBobbingPhase - FastMathf.HALF_PI) * baseHorizontalEyeBobbingIntensity * eyeBobbingIntensity;
            float verticalEyeBobbingDisplacement = -Mathf.Sin(eyeBobbingPhase * 2 - FastMathf.HALF_PI) * baseVerticalEyeBobbingIntensity * eyeBobbingIntensity;
            cameraPosition += transform.up * verticalEyeBobbingDisplacement + transform.right * horizontalEyeBobbingDisplacement;
            // Update phase, and keep it in low values for maximum precision even for very long walks
            eyeBobbingPhase = (eyeBobbingPhase + Time.deltaTime * Mathf.PI * baseEyeBobbingSpeed * eyeBobbingIntensity) % FastMathf.PI_2;

            playerCamera.transform.position = cameraPosition;
            playerCamera.transform.localRotation = transform.localRotation;
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            bool inWater = hit.gameObject.layer == LayerMask.NameToLayer("Water");
            if (inWater)
            {
                // If water is shallow (less deep than half the player height), ignore
                inWater = !Physics.Linecast(hit.point, hit.point + Vector3.down * originalCharacterControllerHeight / 2, LayerMask.GetMask("Default"));
            }

            // Set the in water intensity attribute (it is convenient the most convenient form for most calculations)
            inWaterIntensity = inWater ? 1 : 0;

            // Update charater controller height to simulate switching to and from a swimming pose
            characterController.height = originalCharacterControllerHeight *
                Mathf.LerpUnclamped(1, waterHeightReductionCoefficient, inWaterIntensity);
        }
    }
}
