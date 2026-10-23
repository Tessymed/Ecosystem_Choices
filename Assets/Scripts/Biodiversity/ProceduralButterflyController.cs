using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Controls a single butterfly in flight with procedural multi-pair wing fluttering,
    /// organic wing flexing, dynamic plant-to-plant wandering, and natural horizontal flight orientation.
    /// Works with rigged 3D butterfly models (.blend / .fbx) with upper/lower wing bone pairs.
    /// </summary>
    [AddComponentMenu("Biodiversity/Procedural Butterfly Controller")]
    public class ProceduralButterflyController : MonoBehaviour
    {
        [Header("Wing Pairs (Upper & Lower)")]
        [Tooltip("Left upper (forewing) transform.")]
        [SerializeField] private Transform leftUpperWing;

        [Tooltip("Left lower (hindwing) transform.")]
        [SerializeField] private Transform leftLowerWing;

        [Tooltip("Right upper (forewing) transform.")]
        [SerializeField] private Transform rightUpperWing;

        [Tooltip("Right lower (hindwing) transform.")]
        [SerializeField] private Transform rightLowerWing;

        [Tooltip("Optional left wing tip bone for secondary aerodynamic flexing.")]
        [SerializeField] private Transform leftWingTip;

        [Tooltip("Optional right wing tip bone for secondary aerodynamic flexing.")]
        [SerializeField] private Transform rightWingTip;

        [Header("Wing Flutter Dynamics")]
        [Tooltip("Frequency of procedural wing flapping in Hz.")]
        [SerializeField] private float flapFrequency = 11.0f;

        [Tooltip("Maximum wing flap angle in degrees.")]
        [SerializeField] private float maxFlapAngle = 48.0f;

        [Tooltip("Phase lag between upper (forewing) and lower (hindwing) in radians.")]
        [SerializeField] private float lowerWingPhaseLag = 0.45f;

        [Tooltip("Local rotation axis around which wings flutter (default: Z axis / forward).")]
        [SerializeField] private Vector3 flapRotationAxis = Vector3.forward;

        [Header("Appearance & Color Variation")]
        [Tooltip("Apply subtle random color variations across spawned butterfly clusters.")]
        [SerializeField] private bool randomizeColor = true;

        [Tooltip("Palette of butterfly wing tints (Monarch Orange, Sunlit Amber, Golden Yellow, Emerald, Morpho).")]
        [SerializeField] private Color[] wingColorPalette = new Color[]
        {
            new Color(1.0f, 1.0f, 1.0f, 1.0f),         // Natural Monarch Orange/Gold (default texture)
            new Color(1.05f, 0.92f, 0.75f, 1.0f),      // Warm Sunlit Gold
            new Color(1.1f, 0.82f, 0.65f, 1.0f),       // Vibrant Amber
            new Color(0.95f, 0.95f, 1.05f, 1.0f),      // Pale Moonbeam
            new Color(1.0f, 0.85f, 0.95f, 1.0f)        // Rose Saffron
        };

        [Header("Flight Dynamics & Orientation")]
        [Tooltip("Flying/hovering speed in meters per second.")]
        [SerializeField] private float flightSpeed = 1.8f;

        [Tooltip("Erratic dancing turbulence scale.")]
        [SerializeField] private float turbulenceScale = 1.3f;

        [Tooltip("Euler offset applied to FBX model to ensure head points forward (-Z/tail points backward). Standard Blender FBX requires (-90, 0, 0).")]
        [SerializeField] private Vector3 modelRotationOffset = new Vector3(-90f, 0f, 0f);

        [Tooltip("Slight forward aerodynamic pitch tilt in flight (degrees).")]
        [SerializeField] private float flightPitchAngle = 6.0f;

        [Tooltip("Maximum flight lifetime before automatic despawn.")]
        [SerializeField] private float maxLifetimeSeconds = 35.0f;

        [Header("Flora Wandering (Plant to Plant)")]
        [Tooltip("Radius (meters) around home zone within which butterfly wanders between shrubs/flowers.")]
        [SerializeField] private float meanderRadius = 7.0f;

        [Tooltip("Time range (seconds) spent hovering around one plant before flitting to another nearby.")]
        [SerializeField] private Vector2 plantDwellTimeRange = new Vector2(4.0f, 9.0f);

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip flutterSound;

        private Transform modelSubtree;
        private Quaternion initialLeftUpperRot;
        private Quaternion initialLeftLowerRot;
        private Quaternion initialRightUpperRot;
        private Quaternion initialRightLowerRot;
        private Quaternion initialLeftTipRot;
        private Quaternion initialRightTipRot;

        private Vector3 startPosition;
        private Vector3 anchorZoneCenter;
        private Vector3 currentHoverWaypoint;
        private float wanderTimer;
        private float currentLifetime;
        private bool isFlying;
        private float perlinSeedX;
        private float perlinSeedZ;
        private Renderer wingRenderer;
        private MaterialPropertyBlock propBlock;

        public Vector3 ModelRotationOffset
        {
            get => modelRotationOffset;
            set
            {
                modelRotationOffset = value;
                ApplyModelOrientation();
            }
        }

        private void Awake()
        {
            modelSubtree = transform.childCount > 0 ? transform.GetChild(0) : transform;
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

            perlinSeedX = Random.Range(0f, 1000f);
            perlinSeedZ = Random.Range(0f, 1000f);

            AutoDetectWings();
            SetupColorVariation();
            ApplyModelOrientation();
        }

        private void ApplyModelOrientation()
        {
            if (modelSubtree != null && modelSubtree != transform)
            {
                modelSubtree.localRotation = Quaternion.Euler(modelRotationOffset);
            }
        }

        public void AutoDetectWings()
        {
            var children = GetComponentsInChildren<Transform>();
            foreach (var t in children)
            {
                if (t == transform || t == modelSubtree) continue;
                string nameLower = t.name.ToLower();

                // Skip controller and helper bones
                if (nameLower.Contains("controller") || nameLower.Contains("control")) continue;

                // Match specific upper/lower bones
                if (nameLower.Contains("wing_upper.l") || (nameLower.Contains("upper") && (nameLower.Contains(".l") || nameLower.Contains("_l"))))
                {
                    if (leftUpperWing == null) leftUpperWing = t;
                }
                else if (nameLower.Contains("wing_lower.l") || (nameLower.Contains("lower") && (nameLower.Contains(".l") || nameLower.Contains("_l"))))
                {
                    if (leftLowerWing == null) leftLowerWing = t;
                }
                else if (nameLower.Contains("wing_upper.r") || (nameLower.Contains("upper") && (nameLower.Contains(".r") || nameLower.Contains("_r"))))
                {
                    if (rightUpperWing == null) rightUpperWing = t;
                }
                else if (nameLower.Contains("wing_lower.r") || (nameLower.Contains("lower") && (nameLower.Contains(".r") || nameLower.Contains("_r"))))
                {
                    if (rightLowerWing == null) rightLowerWing = t;
                }
                else if (nameLower.Contains("wing_upper2.l"))
                {
                    if (leftWingTip == null) leftWingTip = t;
                }
                else if (nameLower.Contains("wing_upper2.r"))
                {
                    if (rightWingTip == null) rightWingTip = t;
                }
                else if (leftUpperWing == null && (nameLower.Contains("wing") || nameLower.Contains("flap")) && (nameLower.Contains("l") || nameLower.EndsWith("_l")))
                {
                    leftUpperWing = t;
                }
                else if (rightUpperWing == null && (nameLower.Contains("wing") || nameLower.Contains("flap")) && (nameLower.Contains("r") || nameLower.EndsWith("_r")))
                {
                    rightUpperWing = t;
                }
            }

            // Cache rest rotations
            if (leftUpperWing != null) initialLeftUpperRot = leftUpperWing.localRotation;
            if (leftLowerWing != null) initialLeftLowerRot = leftLowerWing.localRotation;
            if (rightUpperWing != null) initialRightUpperRot = rightUpperWing.localRotation;
            if (rightLowerWing != null) initialRightLowerRot = rightLowerWing.localRotation;
            if (leftWingTip != null) initialLeftTipRot = leftWingTip.localRotation;
            if (rightWingTip != null) initialRightTipRot = rightWingTip.localRotation;
        }

        private void SetupColorVariation()
        {
            if (!randomizeColor || wingColorPalette == null || wingColorPalette.Length == 0) return;

            // Locate wings mesh renderer
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.name.ToLower().Contains("wing"))
                {
                    wingRenderer = r;
                    break;
                }
            }

            if (wingRenderer != null)
            {
                if (propBlock == null) propBlock = new MaterialPropertyBlock();
                Color chosenColor = wingColorPalette[Random.Range(0, wingColorPalette.Length)];
                wingRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", chosenColor);
                wingRenderer.SetPropertyBlock(propBlock);
            }
        }

        /// <summary>
        /// Initializes butterfly flight centered around an initial vegetation hover zone.
        /// </summary>
        public void InitializeFlight(Vector3 spawnPos, Vector3 hoverCenter, float speedOverride = -1f, float meanderRadiusOverride = -1f)
        {
            transform.position = spawnPos;
            startPosition = spawnPos;
            anchorZoneCenter = hoverCenter;
            currentHoverWaypoint = hoverCenter;
            if (speedOverride > 0f) flightSpeed = speedOverride;
            if (meanderRadiusOverride > 0f) meanderRadius = meanderRadiusOverride;

            wanderTimer = Random.Range(plantDwellTimeRange.x, plantDwellTimeRange.y);
            currentLifetime = 0f;
            isFlying = true;

            ApplyModelOrientation();

            // Spatial 3D Audio setup
            if (audioSource != null && flutterSound != null)
            {
                audioSource.clip = flutterSound;
                audioSource.spatialBlend = 1.0f;
                audioSource.volume = 0.25f;
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

            // Periodic plant-to-plant wandering
            wanderTimer -= Time.deltaTime;
            if (wanderTimer <= 0f)
            {
                PickNewHoverWaypoint();
                wanderTimer = Random.Range(plantDwellTimeRange.x, plantDwellTimeRange.y);
            }

            // 1. Procedural Wing Fluttering
            AnimateWingFlutter();

            // 2. Natural Flight Trajectory & Attitude
            UpdateDancingTrajectory();
        }

        private void PickNewHoverWaypoint()
        {
            // Pick a randomized waypoint inside meander radius on vegetation/flora area
            Vector2 randomCircle = Random.insideUnitCircle * meanderRadius;
            Vector3 candidate = anchorZoneCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);

            // Sample ground height or nearby bush height
            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                float groundY = activeTerrain.SampleHeight(candidate) + activeTerrain.transform.position.y;
                float hoverHeight = Random.Range(1.2f, 3.2f);
                candidate.y = groundY + hoverHeight;
            }
            else
            {
                candidate.y = anchorZoneCenter.y + Random.Range(-0.5f, 1.5f);
            }

            currentHoverWaypoint = candidate;
        }

        private void AnimateWingFlutter()
        {
            float time = Time.time;

            // Gliding pauses: butterflies alternate between bursts of flutter and brief graceful glides
            float glideCycle = Mathf.Sin(time * 1.6f);
            bool isGliding = glideCycle > 0.82f;

            float phase = time * flapFrequency * Mathf.PI * 2f;
            float upperFlapAngle = isGliding ? 15.0f : Mathf.Sin(phase) * maxFlapAngle;
            float lowerFlapAngle = isGliding ? 12.0f : Mathf.Sin(phase - lowerWingPhaseLag) * (maxFlapAngle * 0.85f);
            float tipFlexAngle = isGliding ? 4.0f : Mathf.Sin(phase - 0.7f) * (maxFlapAngle * 0.25f);

            // Left Wing Pair
            if (leftUpperWing != null)
            {
                leftUpperWing.localRotation = initialLeftUpperRot * Quaternion.AngleAxis(upperFlapAngle, flapRotationAxis);
            }
            if (leftLowerWing != null)
            {
                leftLowerWing.localRotation = initialLeftLowerRot * Quaternion.AngleAxis(lowerFlapAngle, flapRotationAxis);
            }
            if (leftWingTip != null)
            {
                leftWingTip.localRotation = initialLeftTipRot * Quaternion.AngleAxis(tipFlexAngle, flapRotationAxis);
            }

            // Right Wing Pair (mirrored angle)
            if (rightUpperWing != null)
            {
                rightUpperWing.localRotation = initialRightUpperRot * Quaternion.AngleAxis(-upperFlapAngle, flapRotationAxis);
            }
            if (rightLowerWing != null)
            {
                rightLowerWing.localRotation = initialRightLowerRot * Quaternion.AngleAxis(-lowerFlapAngle, flapRotationAxis);
            }
            if (rightWingTip != null)
            {
                rightWingTip.localRotation = initialRightTipRot * Quaternion.AngleAxis(-tipFlexAngle, flapRotationAxis);
            }
        }

        private void UpdateDancingTrajectory()
        {
            float time = Time.time;

            // Perlin noise + Sine wave 3D turbulence offset for light, floating trajectory
            float noiseX = (Mathf.PerlinNoise(time * turbulenceScale + perlinSeedX, 0f) - 0.5f) * 3.5f;
            float noiseY = Mathf.Sin(time * 3.2f) * 0.6f + Mathf.Cos(time * 1.1f) * 0.3f;
            float noiseZ = (Mathf.PerlinNoise(0f, time * turbulenceScale + perlinSeedZ) - 0.5f) * 3.5f;

            Vector3 desiredTarget = currentHoverWaypoint + new Vector3(noiseX, noiseY, noiseZ);
            Vector3 moveVector = (desiredTarget - transform.position);

            if (moveVector != Vector3.zero)
            {
                Vector3 step = moveVector.normalized * (flightSpeed * Time.deltaTime);
                transform.position += step;

                // Flight heading: align +Z with step direction, +Y with sky
                Vector3 forwardHeading = step.normalized;
                if (forwardHeading.sqrMagnitude > 0.001f)
                {
                    Quaternion lookRot = Quaternion.LookRotation(forwardHeading, Vector3.up);
                    float bankRoll = -noiseX * 7.0f;

                    // Forward aerodynamic pitch and banking into turns
                    Quaternion flightAttitude = lookRot * Quaternion.Euler(flightPitchAngle, 0f, bankRoll);

                    // If model is at root, apply model rotation offset to flight attitude
                    if (modelSubtree == transform)
                    {
                        flightAttitude = flightAttitude * Quaternion.Euler(modelRotationOffset);
                    }

                    transform.rotation = Quaternion.Slerp(transform.rotation, flightAttitude, Time.deltaTime * 5.0f);
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
