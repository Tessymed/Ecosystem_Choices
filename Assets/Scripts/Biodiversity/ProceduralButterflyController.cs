using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Controls a single butterfly in flight with procedural wing fluttering,
    /// erratic dancing flight trajectories, and foliage/flower hovering behaviors.
    /// Works with rigged or static 3D butterfly models (.blend / .fbx).
    /// </summary>
    [AddComponentMenu("Biodiversity/Procedural Butterfly Controller")]
    public class ProceduralButterflyController : MonoBehaviour
    {
        [Header("Wing Flutter Parameters")]
        [Tooltip("Frequency of procedural wing flapping in Hz.")]
        [SerializeField] private float flapFrequency = 14.0f;

        [Tooltip("Maximum wing flap angle in degrees.")]
        [SerializeField] private float maxFlapAngle = 55.0f;

        [Tooltip("Left wing transform (auto-detected if unassigned).")]
        [SerializeField] private Transform leftWing;

        [Tooltip("Right wing transform (auto-detected if unassigned).")]
        [SerializeField] private Transform rightWing;

        [Header("Flight Dynamics")]
        [Tooltip("Flying/hovering speed in meters per second.")]
        [SerializeField] private float flightSpeed = 2.2f;

        [Tooltip("Erratic dancing turbulence scale.")]
        [SerializeField] private float turbulenceScale = 1.5f;

        [Tooltip("Rotation offset (Euler degrees) to correct butterfly FBX mesh orientation if needed.")]
        [SerializeField] private Vector3 modelRotationOffset = Vector3.zero;

        [Tooltip("Maximum flight lifetime before automatic despawn.")]
        [SerializeField] private float maxLifetimeSeconds = 35.0f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip flutterSound;

        private Transform modelSubtree;
        private Quaternion initialLeftWingRot;
        private Quaternion initialRightWingRot;
        private Vector3 startPosition;
        private Vector3 targetHoverPosition;
        private float currentLifetime;
        private bool isFlying;
        private float perlinSeedX;
        private float perlinSeedZ;

        public Vector3 ModelRotationOffset { get => modelRotationOffset; set => modelRotationOffset = value; }

        private void Awake()
        {
            modelSubtree = transform.childCount > 0 ? transform.GetChild(0) : transform;
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

            perlinSeedX = Random.Range(0f, 1000f);
            perlinSeedZ = Random.Range(0f, 1000f);

            AutoDetectWings();
        }

        public void AutoDetectWings()
        {
            if (leftWing == null || rightWing == null)
            {
                var children = GetComponentsInChildren<Transform>();
                foreach (var t in children)
                {
                    if (t == transform || t == modelSubtree) continue;
                    string nameLower = t.name.ToLower();
                    if (nameLower.Contains("wing") || nameLower.Contains("flap") || nameLower.Contains("flutter"))
                    {
                        if (nameLower.Contains("l") || nameLower.Contains("left") || nameLower.EndsWith("_l") || nameLower.EndsWith(".l"))
                        {
                            if (leftWing == null) leftWing = t;
                        }
                        else if (nameLower.Contains("r") || nameLower.Contains("right") || nameLower.EndsWith("_r") || nameLower.EndsWith(".r"))
                        {
                            if (rightWing == null) rightWing = t;
                        }
                    }
                }
            }

            if (leftWing != null) initialLeftWingRot = leftWing.localRotation;
            if (rightWing != null) initialRightWingRot = rightWing.localRotation;
        }

        /// <summary>
        /// Initializes butterfly flight centered around a target hover zone.
        /// </summary>
        public void InitializeFlight(Vector3 spawnPos, Vector3 hoverCenter, float speedOverride = -1f)
        {
            transform.position = spawnPos;
            startPosition = spawnPos;
            targetHoverPosition = hoverCenter;
            if (speedOverride > 0f) flightSpeed = speedOverride;

            currentLifetime = 0f;
            isFlying = true;

            // Apply model rotation offset
            if (modelSubtree != null)
            {
                modelSubtree.localRotation = Quaternion.Euler(modelRotationOffset);
            }

            // Spatial 3D Audio setup
            if (audioSource != null && flutterSound != null)
            {
                audioSource.clip = flutterSound;
                audioSource.spatialBlend = 1.0f;
                audioSource.volume = 0.3f;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        private void Update()
        {
            if (!isFlying) return;

            currentLifetime += Time.deltaTime;

            if (currentLifetime >= maxLifetimeSeconds)
            {
                Despawn();
                return;
            }

            // 1. Procedural Wing Fluttering
            AnimateWingFlutter();

            // 2. Erratic Dancing Trajectory
            UpdateDancingTrajectory();
        }

        private void AnimateWingFlutter()
        {
            // Add subtle gliding pauses
            float glideCycle = Mathf.Sin(Time.time * 1.5f);
            bool isGliding = glideCycle > 0.85f;

            float flapAngle = isGliding ? 10f : Mathf.Sin(Time.time * flapFrequency * Mathf.PI * 2f) * maxFlapAngle;

            if (leftWing != null)
            {
                leftWing.localRotation = initialLeftWingRot * Quaternion.Euler(0f, 0f, flapAngle);
            }

            if (rightWing != null)
            {
                rightWing.localRotation = initialRightWingRot * Quaternion.Euler(0f, 0f, -flapAngle);
            }
        }

        private void UpdateDancingTrajectory()
        {
            float time = Time.time;

            // Perlin noise + Sine wave 3D turbulence offset
            float noiseX = (Mathf.PerlinNoise(time * turbulenceScale + perlinSeedX, 0f) - 0.5f) * 4.0f;
            float noiseY = Mathf.Sin(time * 3.5f) * 0.8f + Mathf.Cos(time * 1.2f) * 0.4f;
            float noiseZ = (Mathf.PerlinNoise(0f, time * turbulenceScale + perlinSeedZ) - 0.5f) * 4.0f;

            Vector3 desiredTarget = targetHoverPosition + new Vector3(noiseX, noiseY, noiseZ);
            Vector3 moveVector = (desiredTarget - transform.position);

            if (moveVector != Vector3.zero)
            {
                Vector3 step = moveVector.normalized * (flightSpeed * Time.deltaTime);
                transform.position += step;

                // Smooth facing rotation with roll banking into turns
                Quaternion targetRot = Quaternion.LookRotation(step.normalized, Vector3.up);
                float bankRoll = -noiseX * 8.0f;
                Quaternion targetWithOffset = targetRot * Quaternion.Euler(0f, 0f, bankRoll) * Quaternion.Euler(modelRotationOffset);

                transform.rotation = Quaternion.Slerp(transform.rotation, targetWithOffset, Time.deltaTime * 6.0f);
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
