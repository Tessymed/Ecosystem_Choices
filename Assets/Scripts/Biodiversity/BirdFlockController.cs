using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Controls a single bird flock instance in flight.
    /// Works with FBX bird models attached to a controller root object.
    /// Handles flight translation, arc elevation, facing rotation, animation playback, and auto-despawn.
    /// </summary>
    [AddComponentMenu("Biodiversity/Bird Flock Controller")]
    public class BirdFlockController : MonoBehaviour
    {
        [Header("Flight Parameters")]
        [Tooltip("Flying speed of the flock in meters per second.")]
        [SerializeField] private float flightSpeed = 12.0f;

        [Tooltip("Curve / upward ascension factor during flight.")]
        [SerializeField] private float verticalArcFactor = 0.5f;

        [Tooltip("Maximum flight duration before automatic cleanup.")]
        [SerializeField] private float maxLifetimeSeconds = 25.0f;

        [Tooltip("Distance from spawn point before auto-despawn.")]
        [SerializeField] private float maxFlightDistance = 300.0f;

        [Header("Animation & Model Settings")]
        [Tooltip("Optional child GameObject for the bird flock model (if combined with other models in a single FBX).")]
        [SerializeField] private GameObject birdModelSubtree;

        [Tooltip("Rotation offset (Euler degrees) to correct FBX mesh orientation (e.g. Roll 180 or Roll 90 if imported sideways).")]
        [SerializeField] private Vector3 modelRotationOffset = new Vector3(0f, 0f, 180f);

        [Tooltip("Optional animator component (can be on empty controller root or child model).")]
        [SerializeField] private Animator animator;

        [Tooltip("Animation clip or trigger name for flying state.")]
        [SerializeField] private string flyAnimationTrigger = "Fly";

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip flightSound;

        private Vector3 flightDirection;
        private Vector3 startPosition;
        private float currentLifetime;
        private bool isFlying;

        public Vector3 ModelRotationOffset { get => modelRotationOffset; set => modelRotationOffset = value; }
        public AudioClip FlightSound { get => flightSound; set => flightSound = value; }

        private void Awake()
        {
            if (birdModelSubtree == null && transform.childCount > 0)
            {
                birdModelSubtree = transform.GetChild(0).gameObject;
            }
            if (birdModelSubtree != null) birdModelSubtree.SetActive(true);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

            ApplyModelRotationOffset();
        }

        /// <summary>
        /// Applies the model rotation offset to the child subtree or root.
        /// </summary>
        public void ApplyModelRotationOffset()
        {
            if (birdModelSubtree != null)
            {
                birdModelSubtree.transform.localRotation = Quaternion.Euler(modelRotationOffset);
            }
        }

        /// <summary>
        /// Initializes and launches the bird flock flight path.
        /// </summary>
        public void InitializeFlight(Vector3 direction, float speedOverride = -1f, AudioClip soundOverride = null)
        {
            flightDirection = direction.normalized;
            if (speedOverride > 0f) flightSpeed = speedOverride;
            if (soundOverride != null) flightSound = soundOverride;

            startPosition = transform.position;
            currentLifetime = 0f;
            isFlying = true;

            // Orient model to face flight direction with model rotation offset
            if (flightDirection != Vector3.zero)
            {
                Quaternion baseRotation = Quaternion.LookRotation(flightDirection, Vector3.up);
                if (birdModelSubtree != null)
                {
                    transform.rotation = baseRotation;
                    birdModelSubtree.transform.localRotation = Quaternion.Euler(modelRotationOffset);
                }
                else
                {
                    transform.rotation = baseRotation * Quaternion.Euler(modelRotationOffset);
                }
            }

            // Trigger animation if animator is present and parameter exists
            if (animator != null && !string.IsNullOrEmpty(flyAnimationTrigger))
            {
                SafeSetAnimBool(animator, flyAnimationTrigger, true);
            }

            // Play spatial 3D audio if audioSource and sound are provided
            if (audioSource != null && flightSound != null)
            {
                audioSource.clip = flightSound;
                audioSource.spatialBlend = 1.0f; // Full 3D spatial sound
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        private void Update()
        {
            if (!isFlying) return;

            currentLifetime += Time.deltaTime;

            // Calculate flight step with subtle vertical curve
            float timeRatio = currentLifetime / maxLifetimeSeconds;
            Vector3 verticalCurve = Vector3.up * Mathf.Sin(timeRatio * Mathf.PI) * verticalArcFactor;

            Vector3 moveStep = (flightDirection + verticalCurve).normalized * (flightSpeed * Time.deltaTime);
            transform.position += moveStep;

            // Update rotation towards travel vector while keeping wings balanced
            if (moveStep != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveStep, Vector3.up);
                if (birdModelSubtree != null)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5.0f);
                    birdModelSubtree.transform.localRotation = Quaternion.Euler(modelRotationOffset);
                }
                else
                {
                    Quaternion targetWithOffset = targetRot * Quaternion.Euler(modelRotationOffset);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetWithOffset, Time.deltaTime * 5.0f);
                }
            }

            // Cleanup when lifetime or distance limit is reached
            float traveledDistance = Vector3.Distance(startPosition, transform.position);
            if (currentLifetime >= maxLifetimeSeconds || traveledDistance >= maxFlightDistance)
            {
                Despawn();
            }
        }

        private void SafeSetAnimBool(Animator anim, string paramName, bool value)
        {
            if (anim == null || string.IsNullOrEmpty(paramName)) return;
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.name.Equals(paramName, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (param.type == AnimatorControllerParameterType.Bool)
                    {
                        anim.SetBool(param.name, value);
                    }
                    else if (param.type == AnimatorControllerParameterType.Trigger && value)
                    {
                        anim.SetTrigger(param.name);
                    }
                    break;
                }
            }
        }

        private void Despawn()
        {
            isFlying = false;

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            Destroy(gameObject);
        }
    }
}

