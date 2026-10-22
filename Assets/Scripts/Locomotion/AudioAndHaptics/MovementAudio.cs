using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Provides immersive movement audio including speed-scaled footstep sounds, spatial pitch variation,
    /// and ambient ecosystem soundscapes (rustling leaves, nature ambiance, wind breeze) as the user explores.
    /// </summary>
    [AddComponentMenu("Locomotion/Movement Audio")]
    [RequireComponent(typeof(AudioSource))]
    public class MovementAudio : MonoBehaviour
    {
        [Header("Footstep Audio Settings")]
        [Tooltip("Distance in meters between consecutive footstep sounds.")]
        [SerializeField] private float stepDistance = 1.4f;

        [Tooltip("Array of footstep audio clips (e.g. grass, dirt, gravel footsteps).")]
        [SerializeField] private AudioClip[] footstepClips;

        [Tooltip("Volume multiplier for footsteps.")]
        [Range(0f, 1f)]
        [SerializeField] private float footstepVolume = 0.5f;

        [Tooltip("Randomize pitch slightly for organic footsteps (min, max).")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);

        [Header("Ecosystem Ambient Soundscape")]
        [Tooltip("Optional AudioSource for ambient ecosystem background audio (nature, wind, rustling leaves).")]
        [SerializeField] private AudioSource ambientAudioSource;

        [Tooltip("Ambient nature / rustling soundscape clip.")]
        [SerializeField] private AudioClip ambientSoundscape;

        [Tooltip("Target volume for ambient background soundscape.")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientMaxVolume = 0.4f;

        [Tooltip("Increase ambient audio slightly when moving through ecosystem.")]
        [SerializeField] private bool dynamicMovementAmbiance = true;

        [Header("References")]
        [SerializeField] private ContinuousLocomotion continuousLocomotion;
        [SerializeField] private GroundDetector groundDetector;

        private AudioSource footstepAudioSource;
        private Vector3 lastPosition;
        private float distanceTraveled;
        private float currentAmbientVolume;

        private void Awake()
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (continuousLocomotion == null) continuousLocomotion = GetComponentInParent<ContinuousLocomotion>();
            if (groundDetector == null) groundDetector = GetComponentInParent<GroundDetector>();

            lastPosition = transform.position;

            InitializeAmbientAudio();
        }

        private void InitializeAmbientAudio()
        {
            if (ambientAudioSource == null && ambientSoundscape != null)
            {
                GameObject ambientGO = new GameObject("AmbientEcosystemAudio");
                ambientGO.transform.SetParent(transform, false);
                ambientAudioSource = ambientGO.AddComponent<AudioSource>();
            }

            if (ambientAudioSource != null && ambientSoundscape != null)
            {
                ambientAudioSource.clip = ambientSoundscape;
                ambientAudioSource.loop = true;
                ambientAudioSource.spatialBlend = 0.3f; // Slight 3D depth, mostly stereo immersion
                ambientAudioSource.volume = dynamicMovementAmbiance ? 0.1f : ambientMaxVolume;
                ambientAudioSource.Play();
            }
        }

        private void Update()
        {
            Vector3 currentPos = transform.position;
            Vector3 horizontalDelta = currentPos - lastPosition;
            horizontalDelta.y = 0f;

            float distanceThisFrame = horizontalDelta.magnitude;
            lastPosition = currentPos;

            bool isGrounded = groundDetector != null ? groundDetector.IsGrounded : true;
            bool isMoving = continuousLocomotion != null ? continuousLocomotion.IsMoving : (distanceThisFrame > 0.001f);

            // 1. Process Footstep cadence based on distance moved
            if (isGrounded && distanceThisFrame > 0.001f)
            {
                distanceTraveled += distanceThisFrame;

                if (distanceTraveled >= stepDistance)
                {
                    PlayFootstepSound();
                    distanceTraveled = 0f;
                }
            }
            else if (!isGrounded)
            {
                distanceTraveled = 0f;
            }

            // 2. Process Dynamic Ambient Volume Scaling when moving through ecosystem
            if (dynamicMovementAmbiance && ambientAudioSource != null && ambientAudioSource.isPlaying)
            {
                float targetVol = isMoving ? ambientMaxVolume : ambientMaxVolume * 0.4f;
                currentAmbientVolume = Mathf.Lerp(ambientAudioSource.volume, targetVol, Time.deltaTime * 2.0f);
                ambientAudioSource.volume = currentAmbientVolume;
            }
        }

        private void PlayFootstepSound()
        {
            if (footstepClips == null || footstepClips.Length == 0 || footstepAudioSource == null) return;

            AudioClip clip = footstepClips[Random.Range(0, footstepClips.Length)];
            if (clip != null)
            {
                footstepAudioSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
                footstepAudioSource.PlayOneShot(clip, footstepVolume);
            }
        }
    }
}
