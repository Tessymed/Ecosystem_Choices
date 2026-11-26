using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    public enum PathConstraintType
    {
        PhysicsLayerMask,
        TerrainPaintedLayer,
        Both
    }

    /// <summary>
    /// Restricts player movement to designated path layers or painted terrain splatmap layers
    /// while allowing a configurable side margin (bleed distance). Works with ContinuousLocomotion and TeleportController.
    /// Uses high-performance cached terrain splatmaps for 0-GC allocation execution.
    /// </summary>
    [AddComponentMenu("Locomotion/Path Movement Constraint")]
    public class PathMovementConstraint : MonoBehaviour
    {
        [Header("Constraint Settings")]
        [Tooltip("Enable or disable path restriction.")]
        [SerializeField] private bool enableConstraint = true;

        [Tooltip("Method used to determine valid path areas: Physics Layer, Painted Terrain Layer, or Both.")]
        [SerializeField] private PathConstraintType constraintType = PathConstraintType.Both;

        [Header("Physics Layer Settings")]
        [Tooltip("Layer mask assigned to separate path colliders (do NOT set to the entire Terrain layer).")]
        [SerializeField] private LayerMask pathLayer = ~0;

        [Header("Terrain Painted Layer Settings")]
        [Tooltip("Target Terrain object. Auto-detects active terrain if unassigned.")]
        [SerializeField] private Terrain targetTerrain;

        [Tooltip("Index of the painted path layer on the TerrainData (0-indexed).")]
        [SerializeField] private int pathTerrainLayerIndex = 0;

        [Tooltip("Optional name of the painted terrain layer asset (e.g., 'Path', 'Dirt', 'Cobblestone'). Auto-finds index if specified.")]
        [SerializeField] private string pathTerrainLayerName = "";

        [Tooltip("Minimum painted splatmap weight (0.01 to 1.0) to consider terrain position on-path.")]
        [Range(0.01f, 1.0f)]
        [SerializeField] private float minPaintedWeight = 0.1f;

        [Header("Bleed & Search Settings")]
        [Tooltip("Maximum allowed distance (meters) the player can bleed/off-step to the side of the path.")]
        [SerializeField] private float allowedBleedDistance = 1.2f;

        [Tooltip("Search radius (meters) around the player to locate path colliders or painted path pixels when off-path.")]
        [SerializeField] private float pathSearchRadius = 8.0f;

        [Header("Ground Check Parameters")]
        [Tooltip("Height above player to begin terrain/path raycasting.")]
        [SerializeField] private float raycastStartHeight = 2.0f;

        [Tooltip("Total length of downward raycast.")]
        [SerializeField] private float raycastDistance = 10.0f;

        [Tooltip("Invert restriction logic (if checked, staying ON the path layer blocks movement, allowing movement off-path).")]
        [SerializeField] private bool invertRestriction = false;

        public bool EnableConstraint { get => enableConstraint; set => enableConstraint = value; }
        public PathConstraintType ConstraintType { get => constraintType; set => constraintType = value; }
        public LayerMask PathLayer { get => pathLayer; set => pathLayer = value; }
        public Terrain TargetTerrain { get => targetTerrain; set => targetTerrain = value; }
        public int PathTerrainLayerIndex { get => pathTerrainLayerIndex; set => pathTerrainLayerIndex = value; }
        public string PathTerrainLayerName { get => pathTerrainLayerName; set => pathTerrainLayerName = value; }
        public float AllowedBleedDistance { get => allowedBleedDistance; set => allowedBleedDistance = value; }
        public bool InvertRestriction { get => invertRestriction; set => invertRestriction = value; }

        private static readonly Collider[] colliderBuffer = new Collider[16];

        // Cached Terrain Alphamap Data for 0-GC high performance
        private float[,,] cachedAlphamaps;
        private int cachedWidth;
        private int cachedHeight;
        private int cachedLayers;
        private Vector3 cachedTerrainPos;
        private Vector3 cachedTerrainSize;
        private bool isTerrainCached;

        private void Awake()
        {
            InitializeTerrain();
        }

        private void Start()
        {
            InitializeTerrain();
            CacheTerrainAlphamaps();
        }

        /// <summary>
        /// Auto-detects active terrain and matches painted layer index by name if provided.
        /// </summary>
        public void InitializeTerrain()
        {
            if (targetTerrain == null)
            {
                targetTerrain = Terrain.activeTerrain;
            }

            if (targetTerrain != null && targetTerrain.terrainData != null)
            {
                TerrainLayer[] layers = targetTerrain.terrainData.terrainLayers;
                if (layers != null && layers.Length > 0)
                {
                    // 1. Match by exact name if provided
                    if (!string.IsNullOrEmpty(pathTerrainLayerName))
                    {
                        for (int i = 0; i < layers.Length; i++)
                        {
                            if (layers[i] != null && layers[i].name.Equals(pathTerrainLayerName, System.StringComparison.OrdinalIgnoreCase))
                            {
                                pathTerrainLayerIndex = i;
                                Debug.Log($"[PathMovementConstraint] Matched specified layer '{pathTerrainLayerName}' at index {i} on Terrain '{targetTerrain.name}'.");
                                return;
                            }
                        }
                    }

                    // 2. Auto-detect path layer if pathTerrainLayerIndex is 0 and multiple layers exist
                    if (pathTerrainLayerIndex == 0 && layers.Length > 1)
                    {
                        string[] pathKeywords = new string[] { "path", "dirt", "gravel", "trail", "walk", "road", "layer_02", "ground_02", "ground_2", "newlayer" };
                        for (int i = 1; i < layers.Length; i++)
                        {
                            if (layers[i] == null) continue;
                            string lName = layers[i].name.ToLower();
                            foreach (string kw in pathKeywords)
                            {
                                if (lName.Contains(kw))
                                {
                                    pathTerrainLayerIndex = i;
                                    pathTerrainLayerName = layers[i].name;
                                    Debug.Log($"[PathMovementConstraint] Auto-detected painted path layer '{layers[i].name}' at index {i} on Terrain '{targetTerrain.name}'.");
                                    return;
                                }
                            }
                        }

                        // Fallback: Use layer index 1 as path layer if layer 0 is base terrain
                        pathTerrainLayerIndex = 1;
                        if (layers.Length > 1 && layers[1] != null) pathTerrainLayerName = layers[1].name;
                        Debug.Log($"[PathMovementConstraint] Defaulted path layer to index 1 ('{(layers.Length > 1 && layers[1] != null ? layers[1].name : "Layer 1")}') on Terrain '{targetTerrain.name}'.");
                    }
                    else
                    {
                        string layerName = pathTerrainLayerIndex < layers.Length && layers[pathTerrainLayerIndex] != null ? layers[pathTerrainLayerIndex].name : $"Layer {pathTerrainLayerIndex}";
                        Debug.Log($"[PathMovementConstraint] Active path layer set to index {pathTerrainLayerIndex} ('{layerName}') on Terrain '{targetTerrain.name}'.");
                    }
                }
            }
        }

        private bool hasAttemptedCache = false;

        /// <summary>
        /// Pre-loads terrain splatmaps into a fast 3D array memory cache to avoid per-frame GC allocations.
        /// </summary>
        public void CacheTerrainAlphamaps()
        {
            hasAttemptedCache = true;
            try
            {
                if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
                if (targetTerrain == null || targetTerrain.terrainData == null)
                {
                    isTerrainCached = false;
                    return;
                }

                TerrainData tData = targetTerrain.terrainData;
                cachedWidth = tData.alphamapWidth;
                cachedHeight = tData.alphamapHeight;
                cachedLayers = tData.alphamapLayers;
                cachedTerrainPos = targetTerrain.transform.position;
                cachedTerrainSize = tData.size;

                if (cachedWidth > 0 && cachedHeight > 0 && cachedLayers > 0)
                {
                    cachedAlphamaps = tData.GetAlphamaps(0, 0, cachedWidth, cachedHeight);
                    isTerrainCached = cachedAlphamaps != null;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PathMovementConstraint] Failed to cache terrain alphamaps: {ex.Message}");
                isTerrainCached = false;
            }
        }

        /// <summary>
        /// Evaluates whether a target position lies on the path (physics or painted terrain layer) or within allowed bleed distance.
        /// </summary>
        public bool IsPositionValid(Vector3 position, out Vector3 nearestPathPoint)
        {
            nearestPathPoint = position;

            if (!enableConstraint) return true;

            // 1. Direct check at target position
            if (CheckDirectPathAt(position, out Vector3 directHitPoint))
            {
                nearestPathPoint = directHitPoint;
                return true;
            }

            // 2. Proximity search around position within search radius
            if (SearchNearestPathPoint(position, out Vector3 closestPoint, out float distance))
            {
                nearestPathPoint = closestPoint;
                return distance <= allowedBleedDistance;
            }

            return false;
        }

        private bool CheckDirectPathAt(Vector3 position, out Vector3 pathPoint)
        {
            pathPoint = position;

            // A. Check Physics Layer Mask (excluding whole Terrain colliders)
            if (constraintType == PathConstraintType.PhysicsLayerMask || constraintType == PathConstraintType.Both)
            {
                Vector3 rayStart = position + Vector3.up * raycastStartHeight;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDistance, pathLayer, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && hit.collider.GetComponent<Terrain>() == null)
                    {
                        pathPoint = hit.point;
                        return true;
                    }
                }
            }

            // B. Check Terrain Painted Layer (splatmaps)
            if (constraintType == PathConstraintType.TerrainPaintedLayer || constraintType == PathConstraintType.Both)
            {
                if (IsPaintedPathAt(position, out Vector3 terrainSurfacePoint))
                {
                    if (targetTerrain != null)
                    {
                        terrainSurfacePoint.y = targetTerrain.SampleHeight(position) + cachedTerrainPos.y;
                    }
                    pathPoint = terrainSurfacePoint;
                    return true;
                }
            }

            // C. Check Wooden Bridge Surfaces (bridges are designated paths across waterways)
            if (EcosystemChoices.Environment.WoodenBridgeSurface.IsPointOnAnyBridge(position, out Vector3 bridgePoint))
            {
                pathPoint = bridgePoint;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Fast zero-allocation lookup of terrain alphamap weight at worldPos for pathTerrainLayerIndex.
        /// </summary>
        public bool IsPaintedPathAt(Vector3 worldPos, out Vector3 terrainSurfacePoint)
        {
            terrainSurfacePoint = worldPos;

            if (!hasAttemptedCache)
            {
                CacheTerrainAlphamaps();
            }

            if (!isTerrainCached || cachedAlphamaps == null) return false;

            Vector3 localPos = worldPos - cachedTerrainPos;

            float normX = localPos.x / cachedTerrainSize.x;
            float normZ = localPos.z / cachedTerrainSize.z;

            if (normX < 0f || normX > 1f || normZ < 0f || normZ > 1f) return false;

            int mapX = (int)(normX * (cachedWidth - 1));
            int mapZ = (int)(normZ * (cachedHeight - 1));

            if (mapX < 0 || mapX >= cachedWidth || mapZ < 0 || mapZ >= cachedHeight) return false;
            if (pathTerrainLayerIndex < 0 || pathTerrainLayerIndex >= cachedLayers) return false;

            // GetAlphamaps array indexing: [z, x, layer]
            float weight = cachedAlphamaps[mapZ, mapX, pathTerrainLayerIndex];

            bool isOnPath = weight >= minPaintedWeight;
            return invertRestriction ? !isOnPath : isOnPath;
        }

        private bool SearchNearestPathPoint(Vector3 position, out Vector3 closestPoint, out float minDistance)
        {
            closestPoint = position;
            minDistance = float.MaxValue;
            bool foundAny = false;

            // A. Check Physics Path Colliders via OverlapSphere (excluding whole Terrain colliders)
            if (constraintType == PathConstraintType.PhysicsLayerMask || constraintType == PathConstraintType.Both)
            {
                int count = Physics.OverlapSphereNonAlloc(position, pathSearchRadius, colliderBuffer, pathLayer, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider col = colliderBuffer[i];
                    if (col == null || col.GetComponent<Terrain>() != null) continue;

                    Vector3 closest = col.ClosestPoint(position);
                    Vector3 diff = position - closest;
                    diff.y = 0f;

                    float dist = diff.magnitude;
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestPoint = closest;
                        foundAny = true;
                    }
                }
            }

            // Check Wooden Bridge Surfaces
            var activeBridges = EcosystemChoices.Environment.WoodenBridgeSurface.ActiveBridges;
            for (int i = 0; i < activeBridges.Count; i++)
            {
                var bridge = activeBridges[i];
                if (bridge != null && bridge.ContainsPoint(position))
                {
                    closestPoint = position;
                    minDistance = 0f;
                    return true;
                }
            }

            // B. Check Painted Terrain Layer using fast cached sampling
            if (constraintType == PathConstraintType.TerrainPaintedLayer || constraintType == PathConstraintType.Both)
            {
                if (isTerrainCached && cachedAlphamaps != null)
                {
                    int ringCount = 2;
                    int samplesPerRing = 6;
                    float maxRadius = Mathf.Min(pathSearchRadius, allowedBleedDistance * 2.0f);

                    for (int r = 1; r <= ringCount; r++)
                    {
                        float radius = (maxRadius / ringCount) * r;
                        for (int s = 0; s < samplesPerRing; s++)
                        {
                            float angle = (s / (float)samplesPerRing) * Mathf.PI * 2f;
                            Vector3 sampleOffset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                            Vector3 samplePos = position + sampleOffset;

                            if (IsPaintedPathAt(samplePos, out _))
                            {
                                Vector3 diff = sampleOffset;
                                float dist = diff.magnitude;
                                if (dist < minDistance)
                                {
                                    minDistance = dist;
                                    closestPoint = samplePos;
                                    foundAny = true;
                                }
                            }
                        }
                        if (foundAny && minDistance <= allowedBleedDistance) break;
                    }
                }
            }

            if (foundAny && targetTerrain != null)
            {
                closestPoint.y = targetTerrain.SampleHeight(closestPoint) + cachedTerrainPos.y;
            }

            return foundAny;
        }

        /// <summary>
        /// Smoothly constrains a proposed move position so it stays within the allowed path + bleed margin.
        /// Uses smooth boundary sliding rather than abrupt snapping to eliminate walking jitter.
        /// </summary>
        public Vector3 ConstrainPosition(Vector3 currentPosition, Vector3 targetPosition)
        {
            if (!enableConstraint) return targetPosition;

            if (IsPositionValid(targetPosition, out Vector3 nearestPathPoint))
            {
                return targetPosition;
            }

            // Target position exceeds allowed bleed distance.
            // Smoothly slide along boundary line rather than abruptly snapping to discrete sample points!
            Vector3 proposedDelta = targetPosition - currentPosition;
            Vector3 toPath = nearestPathPoint - currentPosition;
            toPath.y = 0f;

            if (toPath.sqrMagnitude > 0.0001f)
            {
                Vector3 pathNormal = toPath.normalized; // Inward direction toward valid path
                float dot = Vector3.Dot(proposedDelta, pathNormal);

                // If steering back toward the path, allow full movement immediately
                if (dot > 0f)
                {
                    return targetPosition;
                }

                // If steering outward away from path, strip outward velocity so player smoothly slides along path edge
                Vector3 outwardComponent = Vector3.Project(proposedDelta, -pathNormal);
                if (Vector3.Dot(outwardComponent, -pathNormal) > 0f)
                {
                    proposedDelta -= outwardComponent;
                }

                Vector3 slideTarget = currentPosition + proposedDelta;
                slideTarget.y = targetPosition.y;
                return slideTarget;
            }

            return currentPosition;
        }

        private void OnDrawGizmosSelected()
        {
            if (!enableConstraint) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, allowedBleedDistance);

            if (IsPositionValid(transform.position, out Vector3 pathPoint))
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(pathPoint, 0.2f);
                Gizmos.DrawLine(transform.position, pathPoint);
            }
            else
            {
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position, 0.2f);
            }
        }
    }
}

