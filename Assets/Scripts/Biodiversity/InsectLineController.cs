using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Controls the movement, orientation, physics collision avoidance, ground snapping,
    /// and natural concealment/burrowing lifecycle of insects and multi-legged crawlers (e.g. millipedes, ants).
    /// Uses macro terrain contour sampling (spine length and surface normals) to hug terrain slopes,
    /// ridges, and dips seamlessly with legs snapped flush to the ground and zero jitter.
    /// Steers around obstacles (trees, rocks, stumps, shrubs) using whisker collision avoidance,
    /// and smoothly burrows into foliage/earth instead of abruptly vanishing.
    /// </summary>
    [AddComponentMenu("Biodiversity/Insect Line Controller")]
    public class InsectLineController : MonoBehaviour
    {
        public enum ControllerState
        {
            Idle,
            Crawling,
            Burrowing,
            Despawned
        }

        [Header("Movement Settings")]
        [Tooltip("Crawling speed in meters per second (default: 0.14 m/s for natural calm millipede crawl).")]
        [SerializeField] private float crawlSpeed = 0.14f;

        [Tooltip("Multiplier for global crawling speed scaling.")]
        [Range(0.05f, 2.5f)]
        [SerializeField] private float speedMultiplier = 1.0f;

        [Tooltip("Sync animator playback speed proportional to crawlSpeed so leg ripple gait matches ground speed.")]
        [SerializeField] private bool syncAnimationSpeed = true;

        [Tooltip("Base speed reference at which animator plays at 1.0x (m/s).")]
        [SerializeField] private float referenceAnimationSpeed = 0.15f;

        [Tooltip("Layer mask for terrain/ground alignment.")]
        [SerializeField] private LayerMask groundLayer = ~0;

        [Tooltip("Height offset above ground surface in world meters (e.g. 0.001 = 1mm). Set very low so feet touch the ground directly.")]
        [SerializeField] private float groundOffset = 0.001f;

        [Tooltip("Automatically detect lowest mesh point and lift pivot so feet touch the ground surface.")]
        [SerializeField] private bool autoCalculateGroundOffset = true;

        [Tooltip("Half-length of insect body along travel direction (world meters). Auto-calculated from mesh bounds.")]
        [SerializeField] private float bodyHalfLength = 0.15f;

        [Tooltip("Turn steering rate in degrees per second.")]
        [SerializeField] private float turnSpeed = 6.0f;

        [Tooltip("Smoothing speed for terrain contour pitch/roll alignment.")]
        [SerializeField] private float contourAlignmentSpeed = 9.0f;

        [Tooltip("Vertical position damping rate to eliminate terrain micro-step jitter.")]
        [SerializeField] private float heightSmoothRate = 12.0f;

        [Header("Obstacle Avoidance & Physics Collision")]
        [Tooltip("Layer mask for obstacle models (trees, rocks, stumps, shrubs).")]
        [SerializeField] private LayerMask obstacleLayer = ~0;

        [Tooltip("Distance ahead to look for obstacles and steer away.")]
        [SerializeField] private float obstacleDetectionDistance = 1.3f;

        [Tooltip("Radius of the collision sphere check around insect body.")]
        [SerializeField] private float obstacleRadius = 0.04f;

        [Tooltip("Steering strength when turning away from obstacles.")]
        [SerializeField] private float avoidanceStrength = 3.5f;

        [Header("Concealment & Burrowing")]
        [Tooltip("Distance from destination (inside bush/undergrowth) to begin burrowing into the earth.")]
        [SerializeField] private float burrowDistanceThreshold = 0.7f;

        [Tooltip("Duration in seconds of the smooth burrowing down / shrink sequence.")]
        [SerializeField] private float burrowDuration = 1.6f;

        [Tooltip("Downward pitch angle in degrees as insect digs into the soil/leaves.")]
        [SerializeField] private float burrowPitchAngle = 18.0f;

        [Tooltip("Vertical distance below ground the insect sinks during burrowing.")]
        [SerializeField] private float burrowSinkDepth = 0.15f;

        [Tooltip("Maximum duration (seconds) before safety burrow/despawn.")]
        [SerializeField] private float maxLifetimeSeconds = 60.0f;

        [Header("Animation & Model Settings")]
        [Tooltip("Optional child GameObject for the visual insect model (if nested inside a container object).")]
        [SerializeField] private GameObject antModelSubtree;

        [Tooltip("Rotation offset (Euler degrees) to correct insect FBX mesh orientation (Pitch X, Yaw Y, Roll Z) so belly is flat on ground.")]
        [SerializeField] private Vector3 modelRotationOffset = Vector3.zero;

        [SerializeField] private Animator animator;
        [SerializeField] private string crawlAnimationBool = "IsCrawling";

        [Tooltip("Only attach procedural ant/beetle wobble if no Animator Controller exists.")]
        [SerializeField] private bool useProceduralFallback = false;

        [Header("Audio Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip crawlingSound;
        [Range(0f, 1f)]
        [SerializeField] private float audioVolume = 0.4f;

        // Runtime state
        private Vector3 targetEndPoint;
        private ControllerState currentState = ControllerState.Idle;
        private float currentLifetime;
        private float calculatedPivotElevation = 0f;
        private Vector3 currentVelocityDirection = Vector3.forward;
        private Vector3 initialLocalScale = Vector3.one;
        private float burrowTimer = 0f;
        private Terrain activeTerrain;
        private TerrainCollider activeTerrainCollider;
        private Vector3 currentSmoothedNormal = Vector3.up;

        public Vector3 ModelRotationOffset { get => modelRotationOffset; set => modelRotationOffset = value; }
        public float GroundOffset { get => groundOffset; set => groundOffset = value; }
        public float BodyHalfLength { get => bodyHalfLength; set => bodyHalfLength = value; }
        public ControllerState State => currentState;
        public GameObject ModelSubtree { get => antModelSubtree; set => antModelSubtree = value; }

        public float CrawlSpeed
        {
            get => crawlSpeed;
            set
            {
                crawlSpeed = value;
                UpdateAnimatorSpeed();
            }
        }

        public float SpeedMultiplier
        {
            get => speedMultiplier;
            set
            {
                speedMultiplier = value;
                UpdateAnimatorSpeed();
            }
        }

        public float EffectiveSpeed => crawlSpeed * speedMultiplier;

        public void UpdateAnimatorSpeed()
        {
            if (syncAnimationSpeed && animator != null)
            {
                animator.speed = Mathf.Clamp(EffectiveSpeed / referenceAnimationSpeed, 0.2f, 2.5f);
            }
        }

        private void Awake()
        {
            initialLocalScale = transform.localScale;

            // Cache active terrain to ensure clean ground snapping without hitting branches/rocks
            activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                activeTerrainCollider = activeTerrain.GetComponent<TerrainCollider>();
            }

            if (antModelSubtree != null) antModelSubtree.SetActive(true);
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();

            // Apply rotation offset FIRST before calculating ground bounds
            ApplyModelRotationOffset();
            CalculateGroundClearance();

            // Only attach ProceduralInsectAnimator if explicitly enabled and no animator controller exists
            if (useProceduralFallback && (animator == null || animator.runtimeAnimatorController == null) && GetComponent<ProceduralInsectAnimator>() == null)
            {
                gameObject.AddComponent<ProceduralInsectAnimator>();
            }
        }

        public void ApplyModelRotationOffset()
        {
            if (antModelSubtree != null)
            {
                antModelSubtree.transform.localRotation = Quaternion.Euler(modelRotationOffset);
            }
        }

        /// <summary>
        /// Accurately computes the world-space vertical depth of the creature's feet below its root pivot
        /// as well as the body footprint length across all child meshes and renderers.
        /// </summary>
        public void CalculateGroundClearance()
        {
            if (!autoCalculateGroundOffset)
            {
                calculatedPivotElevation = groundOffset;
                return;
            }

            // Save current pose
            Vector3 prevPos = transform.position;
            Quaternion prevRot = transform.rotation;

            // Temporarily set transform to origin with identity rotation to measure world bounds at scale
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            // Ensure visual subtree orientation offset is applied
            ApplyModelRotationOffset();

            Bounds totalBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool foundBounds = false;

            // 1. Transform MeshFilter bounding boxes to world space
            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
            foreach (var mf in meshFilters)
            {
                if (mf == null || mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                Vector3 c = b.center;
                Vector3 e = b.extents;

                Vector3[] corners = new Vector3[8]
                {
                    mf.transform.TransformPoint(new Vector3(c.x - e.x, c.y - e.y, c.z - e.z)),
                    mf.transform.TransformPoint(new Vector3(c.x - e.x, c.y - e.y, c.z + e.z)),
                    mf.transform.TransformPoint(new Vector3(c.x - e.x, c.y + e.y, c.z - e.z)),
                    mf.transform.TransformPoint(new Vector3(c.x - e.x, c.y + e.y, c.z + e.z)),
                    mf.transform.TransformPoint(new Vector3(c.x + e.x, c.y - e.y, c.z - e.z)),
                    mf.transform.TransformPoint(new Vector3(c.x + e.x, c.y - e.y, c.z + e.z)),
                    mf.transform.TransformPoint(new Vector3(c.x + e.x, c.y + e.y, c.z - e.z)),
                    mf.transform.TransformPoint(new Vector3(c.x + e.x, c.y + e.y, c.z + e.z)),
                };

                foreach (var pt in corners)
                {
                    if (!foundBounds)
                    {
                        totalBounds = new Bounds(pt, Vector3.zero);
                        foundBounds = true;
                    }
                    else
                    {
                        totalBounds.Encapsulate(pt);
                    }
                }
            }

            // 2. Also incorporate all Renderers (MeshRenderer, SkinnedMeshRenderer)
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                if (rend == null || !rend.enabled) continue;
                if (!foundBounds)
                {
                    totalBounds = rend.bounds;
                    foundBounds = true;
                }
                else
                {
                    totalBounds.Encapsulate(rend.bounds);
                }
            }

            // Restore original pose
            transform.position = prevPos;
            transform.rotation = prevRot;

            if (foundBounds)
            {
                // In world space, feet depth below root pivot (pivot is at Y=0 during measurement)
                float feetDepthWorld = Mathf.Max(0f, -totalBounds.min.y);
                calculatedPivotElevation = feetDepthWorld + groundOffset;

                // Auto-detect body half-length (along forward Z)
                if (totalBounds.size.z > 0.02f)
                {
                    bodyHalfLength = Mathf.Clamp(totalBounds.size.z * 0.42f, 0.04f, 1.2f);
                }
            }
            else
            {
                calculatedPivotElevation = groundOffset;
            }
        }

        /// <summary>
        /// Samples terrain or ground surface at a specific world coordinate.
        /// </summary>
        public Vector3 SampleGround(Vector3 worldPos, out Vector3 surfaceNormal)
        {
            surfaceNormal = Vector3.up;

            if (activeTerrain != null && activeTerrain.terrainData != null)
            {
                float terrainY = activeTerrain.SampleHeight(worldPos) + activeTerrain.transform.position.y;
                Vector3 tSize = activeTerrain.terrainData.size;
                Vector3 tPos = activeTerrain.transform.position;

                float normX = Mathf.Clamp01((worldPos.x - tPos.x) / tSize.x);
                float normZ = Mathf.Clamp01((worldPos.z - tPos.z) / tSize.z);
                surfaceNormal = activeTerrain.terrainData.GetInterpolatedNormal(normX, normZ);

                // Check if there is an elevated path collider or bridge slightly above the terrain
                Vector3 rayStart = new Vector3(worldPos.x, terrainY + 0.5f, worldPos.z);
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 0.7f, groundLayer, QueryTriggerInteraction.Ignore))
                {
                    if (activeTerrainCollider == null || hit.collider != activeTerrainCollider)
                    {
                        surfaceNormal = hit.normal;
                        return hit.point;
                    }
                }

                return new Vector3(worldPos.x, terrainY, worldPos.z);
            }

            // Fallback: downward raycast hitting groundLayer
            Vector3 fallbackRayStart = worldPos + Vector3.up * 5.0f;
            if (Physics.Raycast(fallbackRayStart, Vector3.down, out RaycastHit fallbackHit, 15.0f, groundLayer, QueryTriggerInteraction.Ignore))
            {
                surfaceNormal = fallbackHit.normal;
                return fallbackHit.point;
            }

            return worldPos;
        }

        /// <summary>
        /// Computes terrain contouring across the body spine while filtering out high-frequency micro-jitter.
        /// Uses length-based spine alignment for natural pitch and smooth interpolated normals for roll.
        /// </summary>
        public Vector3 SnapAndContourToTerrain(Vector3 worldPos, Vector3 forwardHeading, out Vector3 blendedNormal, out Quaternion contourRotation)
        {
            Vector3 fwdH = forwardHeading;
            fwdH.y = 0f;
            if (fwdH.sqrMagnitude < 0.0001f) fwdH = Vector3.forward;
            else fwdH.Normalize();

            // 1. Sample 3 points along the longitudinal spine (Front, Center, Rear)
            Vector3 centerGround = SampleGround(worldPos, out Vector3 centerNormal);
            Vector3 frontGround = SampleGround(worldPos + fwdH * bodyHalfLength, out Vector3 frontNormal);
            Vector3 rearGround = SampleGround(worldPos - fwdH * bodyHalfLength, out Vector3 rearNormal);

            // 2. Compute smooth spine vector along body length (macro slope pitch)
            Vector3 spineDir = frontGround - rearGround;
            if (spineDir.sqrMagnitude < 0.0001f) spineDir = fwdH;
            else spineDir.Normalize();

            // 3. Blend terrain surface normals across the body span (smooth, filtered surface normal)
            Vector3 avgNormal = (centerNormal * 2.0f + frontNormal + rearNormal).normalized;
            if (avgNormal.y < 0.25f) avgNormal = Vector3.up;

            // 4. Formulate orthogonal slope axes:
            // Right is perpendicular to spine and surface normal:
            Vector3 slopeRight = Vector3.Cross(avgNormal, spineDir).normalized;
            if (slopeRight.sqrMagnitude < 0.001f) slopeRight = Vector3.Cross(Vector3.up, fwdH).normalized;

            // True surface normal perpendicular to spine and lateral slope:
            blendedNormal = Vector3.Cross(spineDir, slopeRight).normalized;
            if (blendedNormal.y < 0.3f) blendedNormal = Vector3.up;

            // 5. Smooth ground contact elevation (weighted spline average eliminating vertical pops)
            float midSpineY = (frontGround.y + rearGround.y) * 0.5f;
            float contactGroundY = midSpineY * 0.55f + centerGround.y * 0.45f;
            Vector3 contactPoint = new Vector3(worldPos.x, contactGroundY, worldPos.z);

            // 6. Snap pivot along blended normal
            float elevation = calculatedPivotElevation > 0f ? calculatedPivotElevation : groundOffset;
            Vector3 snappedPosition = contactPoint + blendedNormal * elevation;

            // 7. Compute contour rotation
            Vector3 slopeForward = Vector3.ProjectOnPlane(spineDir, blendedNormal).normalized;
            if (slopeForward.sqrMagnitude < 0.001f) slopeForward = fwdH;

            contourRotation = Quaternion.LookRotation(slopeForward, blendedNormal);

            return snappedPosition;
        }

        /// <summary>
        /// Snaps a horizontal position cleanly down to the terrain/ground surface.
        /// </summary>
        public Vector3 SnapToTerrain(Vector3 worldPos, out Vector3 surfaceNormal)
        {
            Vector3 groundPoint = SampleGround(worldPos, out surfaceNormal);
            float elevation = calculatedPivotElevation > 0f ? calculatedPivotElevation : groundOffset;
            return groundPoint + surfaceNormal * elevation;
        }

        /// <summary>
        /// Initializes the crossing path from startPoint to endPoint across terrain.
        /// </summary>
        public void InitializeCrossing(Vector3 startPoint, Vector3 endPoint, float speedOverride = -1f)
        {
            if (speedOverride > 0f) crawlSpeed = speedOverride;
            UpdateAnimatorSpeed();

            Vector3 moveDirection = endPoint - startPoint;
            moveDirection.y = 0f;
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                currentVelocityDirection = moveDirection.normalized;
            }
            else
            {
                currentVelocityDirection = transform.forward;
                currentVelocityDirection.y = 0f;
                if (currentVelocityDirection.sqrMagnitude < 0.001f) currentVelocityDirection = Vector3.forward;
            }

            Vector3 snappedStart = SnapAndContourToTerrain(startPoint, currentVelocityDirection, out Vector3 startNormal, out Quaternion startRotation);
            transform.position = snappedStart;
            currentSmoothedNormal = startNormal;
            targetEndPoint = SnapToTerrain(endPoint, out _);

            // Apply instant contour rotation
            ApplyOrientation(startRotation, true);

            currentLifetime = 0f;
            burrowTimer = 0f;
            currentState = ControllerState.Crawling;

            // Safe animator parameter trigger
            if (animator != null)
            {
                animator.enabled = true;
                UpdateAnimatorSpeed();
                if (!string.IsNullOrEmpty(crawlAnimationBool))
                {
                    SafeSetAnimBool(animator, crawlAnimationBool, true);
                }
            }

            // Spatial 3D Audio setup
            if (audioSource != null)
            {
                if (crawlingSound != null) audioSource.clip = crawlingSound;
                audioSource.spatialBlend = 1.0f;
                audioSource.volume = audioVolume;
                audioSource.loop = true;
                if (audioSource.clip != null && !audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }
        }

        private void Update()
        {
            if (currentState == ControllerState.Idle || currentState == ControllerState.Despawned)
                return;

            if (currentState == ControllerState.Burrowing)
            {
                UpdateBurrowing();
                return;
            }

            currentLifetime += Time.deltaTime;

            Vector3 currentPos = transform.position;
            Vector3 toTarget = targetEndPoint - currentPos;
            toTarget.y = 0f;
            float distanceToTarget = toTarget.magnitude;

            // Check if we've arrived at destination cover or reached safety timeout
            if (distanceToTarget <= burrowDistanceThreshold || currentLifetime >= maxLifetimeSeconds)
            {
                StartBurrowing();
                return;
            }

            // 1. Calculate desired travel direction
            Vector3 desiredDirection = toTarget.normalized;

            // 2. Compute obstacle avoidance steering force
            Vector3 avoidanceForce = ComputeObstacleAvoidance(currentPos, currentVelocityDirection);

            // Blend target heading with avoidance heading
            Vector3 blendedDirection = (desiredDirection + avoidanceForce * avoidanceStrength).normalized;
            currentVelocityDirection = Vector3.Slerp(currentVelocityDirection, blendedDirection, Time.deltaTime * turnSpeed);
            currentVelocityDirection.y = 0f;
            if (currentVelocityDirection.sqrMagnitude < 0.001f) currentVelocityDirection = desiredDirection;
            currentVelocityDirection.Normalize();

            // 3. Check for hard collision ahead and slide along obstacle surface if blocked
            float stepDistance = EffectiveSpeed * Time.deltaTime;
            Vector3 moveStep = currentVelocityDirection * stepDistance;

            if (DetectHardObstacle(currentPos, currentVelocityDirection, stepDistance * 1.5f, out Vector3 obstacleNormal))
            {
                // Slide along obstacle tangent
                Vector3 tangent = Vector3.ProjectOnPlane(currentVelocityDirection, obstacleNormal).normalized;
                if (tangent.sqrMagnitude > 0.01f)
                {
                    currentVelocityDirection = tangent;
                    moveStep = currentVelocityDirection * (stepDistance * 0.7f);
                }
                else
                {
                    // Blocked completely: turn 90 degrees away from obstacle
                    Vector3 turnAway = Vector3.Cross(Vector3.up, obstacleNormal).normalized;
                    currentVelocityDirection = turnAway;
                    moveStep = currentVelocityDirection * (stepDistance * 0.4f);
                }
            }

            // 4. Advance position and snap cleanly to terrain surface with smooth damping
            Vector3 nextPosHorizontal = currentPos + moveStep;
            Vector3 nextPosGrounded = SnapAndContourToTerrain(nextPosHorizontal, currentVelocityDirection, out Vector3 surfaceNormal, out Quaternion contourRotation);

            // Damped vertical following to eliminate high-frequency micro-step chatter
            float smoothedY = Mathf.Lerp(currentPos.y, nextPosGrounded.y, Time.deltaTime * heightSmoothRate);
            transform.position = new Vector3(nextPosGrounded.x, smoothedY, nextPosGrounded.z);

            // 5. Orient body smoothly to follow terrain contour pitch, roll, and steering heading
            ApplyOrientation(contourRotation, false);
        }

        /// <summary>
        /// Casts feeler rays (center, left whisker, right whisker) to steer smoothly away from trees, rocks, stumps, shrubs.
        /// </summary>
        private Vector3 ComputeObstacleAvoidance(Vector3 origin, Vector3 forward)
        {
            Vector3 avoidance = Vector3.zero;
            Vector3 rayStart = origin + Vector3.up * 0.05f;

            // 3 Whiskers: Center, Left (32 deg), Right (32 deg)
            Quaternion leftRot = Quaternion.Euler(0f, -32f, 0f);
            Quaternion rightRot = Quaternion.Euler(0f, 32f, 0f);

            Vector3 centerDir = forward;
            Vector3 leftDir = leftRot * forward;
            Vector3 rightDir = rightRot * forward;

            bool centerHit = CheckFeelerRay(rayStart, centerDir, obstacleDetectionDistance, out RaycastHit cHit);
            bool leftHit = CheckFeelerRay(rayStart, leftDir, obstacleDetectionDistance * 0.85f, out RaycastHit lHit);
            bool rightHit = CheckFeelerRay(rayStart, rightDir, obstacleDetectionDistance * 0.85f, out RaycastHit rHit);

            if (centerHit)
            {
                // If left is more open than right, steer left; else steer right
                float leftDist = leftHit ? lHit.distance : obstacleDetectionDistance;
                float rightDist = rightHit ? rHit.distance : obstacleDetectionDistance;

                Vector3 steerSide = (rightDist >= leftDist) ? Vector3.Cross(Vector3.up, cHit.normal).normalized : -Vector3.Cross(Vector3.up, cHit.normal).normalized;
                float weight = 1.0f - Mathf.Clamp01(cHit.distance / obstacleDetectionDistance);
                avoidance += (steerSide * 1.5f + cHit.normal).normalized * weight;
            }
            else
            {
                if (leftHit)
                {
                    float weight = 1.0f - Mathf.Clamp01(lHit.distance / (obstacleDetectionDistance * 0.85f));
                    avoidance += Vector3.Cross(Vector3.up, lHit.normal).normalized * weight;
                }
                if (rightHit)
                {
                    float weight = 1.0f - Mathf.Clamp01(rHit.distance / (obstacleDetectionDistance * 0.85f));
                    avoidance += -Vector3.Cross(Vector3.up, rHit.normal).normalized * weight;
                }
            }

            avoidance.y = 0f;
            return avoidance;
        }

        private bool CheckFeelerRay(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            if (Physics.SphereCast(origin, obstacleRadius * 0.5f, direction, out hit, distance, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                // Ignore terrain itself as an obstacle
                if (activeTerrainCollider != null && hit.collider == activeTerrainCollider)
                {
                    return false;
                }
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        private bool DetectHardObstacle(Vector3 origin, Vector3 direction, float distance, out Vector3 obstacleNormal)
        {
            obstacleNormal = Vector3.zero;
            Vector3 rayStart = origin + Vector3.up * 0.05f;

            if (Physics.SphereCast(rayStart, obstacleRadius, direction, out RaycastHit hit, distance, obstacleLayer, QueryTriggerInteraction.Ignore))
            {
                if (activeTerrainCollider != null && hit.collider == activeTerrainCollider)
                {
                    return false;
                }
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                {
                    return false;
                }

                obstacleNormal = hit.normal;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Applies target contour rotation to root transform and ensures model rotation offset is maintained.
        /// </summary>
        private void ApplyOrientation(Quaternion targetRotation, bool instant)
        {
            if (antModelSubtree != null)
            {
                // Subtree container handles model rotation offset cleanly
                if (instant)
                {
                    transform.rotation = targetRotation;
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * contourAlignmentSpeed);
                }
                antModelSubtree.transform.localRotation = Quaternion.Euler(modelRotationOffset);
            }
            else
            {
                // Rotate root incorporating model offset
                Quaternion targetWithOffset = targetRotation * Quaternion.Euler(modelRotationOffset);
                if (instant)
                {
                    transform.rotation = targetWithOffset;
                }
                else
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetWithOffset, Time.deltaTime * contourAlignmentSpeed);
                }
            }
        }

        private void StartBurrowing()
        {
            currentState = ControllerState.Burrowing;
            burrowTimer = 0f;
        }

        /// <summary>
        /// Smoothly conceals insect by tilting head down into soil/leaves, sinking down, and fading/shrinking.
        /// </summary>
        private void UpdateBurrowing()
        {
            burrowTimer += Time.deltaTime;
            float t = Mathf.Clamp01(burrowTimer / burrowDuration);

            // 1. Continue crawling forward slowly while digging
            float slowCrawlStep = (crawlSpeed * 0.35f) * Time.deltaTime;
            Vector3 forwardStep = currentVelocityDirection * slowCrawlStep;

            // 2. Sink down into the earth below surface while contouring to slope
            float currentSink = Mathf.Lerp(0f, burrowSinkDepth, t);
            Vector3 groundedPos = SnapAndContourToTerrain(transform.position + forwardStep, currentVelocityDirection, out Vector3 surfaceNormal, out Quaternion contourRotation);
            transform.position = groundedPos - surfaceNormal * currentSink;

            // 3. Pitch head downwards into the soil
            Quaternion pitchDown = Quaternion.AngleAxis(burrowPitchAngle * t, transform.right);
            ApplyOrientation(pitchDown * contourRotation, false);

            // 4. Smoothly shrink scale to zero as it burrows underneath
            float scaleFactor = Mathf.Lerp(1.0f, 0.0f, t * t);
            transform.localScale = initialLocalScale * scaleFactor;

            // 5. Fade audio smoothly
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.volume = Mathf.Lerp(audioVolume, 0f, t);
            }

            if (t >= 1.0f)
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
            currentState = ControllerState.Despawned;

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            // Feeler whiskers visualization
            Gizmos.color = Color.green;
            Vector3 pos = transform.position + Vector3.up * 0.05f;
            Vector3 forward = transform.forward;
            Gizmos.DrawRay(pos, forward * obstacleDetectionDistance);
            Gizmos.DrawRay(pos, Quaternion.Euler(0, -32, 0) * forward * (obstacleDetectionDistance * 0.85f));
            Gizmos.DrawRay(pos, Quaternion.Euler(0, 32, 0) * forward * (obstacleDetectionDistance * 0.85f));
            Gizmos.DrawWireSphere(pos + forward * obstacleDetectionDistance, obstacleRadius);

            // Body contour points visualization
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position + forward * bodyHalfLength, 0.015f);
            Gizmos.DrawSphere(transform.position - forward * bodyHalfLength, 0.015f);
        }
    }
}
