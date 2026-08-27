using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Synchronizes footstep audio based on actual distance traveled rather than a fixed timer.
    /// Adjusts footstep cadence naturally as movement speed increases or decreases.
    /// </summary>
    [AddComponentMenu("Locomotion/Movement Audio")]
    [RequireComponent(typeof(AudioSource))]
    public class MovementAudio : MonoBehaviour
    {
        [Header("Footstep Audio Settings")]
        [Tooltip("Distance in meters between consecutive footstep sounds.")]
        [SerializeField] private float stepDistance = 1.4f;

        [Tooltip("Array of footstep audio clips.")]
        [SerializeField] private AudioClip[] defaultFootsteps;

        [Tooltip("Volume multiplier for footsteps.")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.5f;

        [Header("References")]
        [SerializeField] private ContinuousLocomotion continuousLocomotion;
        [SerializeField] private GroundDetector groundDetector;

        private AudioSource audioSource;
        private Vector3 lastPosition;
        private float distanceTraveled;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (continuousLocomotion == null) continuousLocomotion = GetComponentInParent<ContinuousLocomotion>();
            if (groundDetector == null) groundDetector = GetComponentInParent<GroundDetector>();

            lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 currentPos = transform.position;
            // Measure horizontal distance moved
            Vector3 horizontalDelta = currentPos - lastPosition;
            horizontalDelta.y = 0f;

            float distanceThisFrame = horizontalDelta.magnitude;
            lastPosition = currentPos;

            bool isGrounded = groundDetector != null ? groundDetector.IsGrounded : true;

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
        }

        private void PlayFootstepSound()
        {
            if (defaultFootsteps == null || defaultFootsteps.Length == 0 || audioSource == null) return;

            AudioClip clip = defaultFootsteps[Random.Range(0, defaultFootsteps.Length)];
            if (clip != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
