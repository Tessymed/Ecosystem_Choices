using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Spawns butterflies in green flora areas of the terrain (away from the painted path)
    /// with randomized group sizes and wide scattering anchored to vegetation and shrubs.
    /// </summary>
    [AddComponentMenu("Biodiversity/Butterfly Spawner")]
    public class ButterflySpawner : MonoBehaviour
    {
        [Header("Prefab & References")]
        [Tooltip("Butterfly prefab(s) configured with ProceduralButterflyController.")]
        [SerializeField] private GameObject[] butterflyPrefabs;

        [Tooltip("Transform of participant / HMD camera. Auto-detects main camera if null.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Reference to PathMovementConstraint to ensure butterflies spawn on the green terrain, off-path.")]
        [SerializeField] private EcosystemChoices.Locomotion.PathMovementConstraint pathConstraint;

        [Header("Cluster Size & Numbers")]
        [Tooltip("Minimum butterflies per spawn event.")]
        [Range(1, 4)]
        [SerializeField] private int minClusterSize = 1;

        [Tooltip("Maximum butterflies per spawn event.")]
        [Range(1, 8)]
        [SerializeField] private int maxClusterSize = 5;

        [Header("Spawn Timing & Distances")]
        [Tooltip("Minimum time (seconds) between butterfly spawns.")]
        [SerializeField] private float minSpawnInterval = 10.0f;

        [Tooltip("Maximum time (seconds) between butterfly spawns.")]
        [SerializeField] private float maxSpawnInterval = 30.0f;

        [Tooltip("Distance ahead of player to spawn butterfly zone.")]
        [SerializeField] private float spawnDistanceAhead = 14.0f;

        [Tooltip("Side spread distance (meters) away from path on green terrain.")]
        [SerializeField] private float sideSpreadDistance = 16.0f;

        [Header("Vegetation Scattering")]
        [Tooltip("Scatter radius (meters) across the flora zone for wide natural distribution.")]
        [SerializeField] private float scatterRadius = 15.0f;

        [Tooltip("Search radius (meters) to detect nearby shrub, bush, or plant colliders.")]
        [SerializeField] private float plantSearchRadius = 7.0f;

        [Tooltip("Layer mask for shrubs, bushes, foliage, and vegetation models.")]
        [SerializeField] private LayerMask vegetationLayer = ~0;

        [Tooltip("Min and max flight height above terrain / bush canopy.")]
        [SerializeField] private Vector2 flightHeightRange = new Vector2(1.2f, 3.2f);

        private float nextSpawnTime;

        private void Awake()
        {
            FindPlayerTransform();

            if (pathConstraint == null)
            {
                pathConstraint = FindAnyObjectByType<EcosystemChoices.Locomotion.PathMovementConstraint>();
            }
        }

        private void FindPlayerTransform()
        {
            if (playerTransform != null) return;

            if (Camera.main != null)
            {
                playerTransform = Camera.main.transform;
            }
            else
            {
                var locomotion = FindAnyObjectByType<Locomotion.LocomotionManager>();
                if (locomotion != null)
                {
                    playerTransform = locomotion.transform;
                }
                else
                {
                    GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                    if (centerEye != null) playerTransform = centerEye.transform;
                    else
                    {
                        GameObject player = GameObject.FindWithTag("Player");
                        if (player != null) playerTransform = player.transform;
                    }
                }
            }
        }

        private void Start()
        {
            ScheduleNextTimer();
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                FindPlayerTransform();
                if (playerTransform == null) return;
            }

            if (butterflyPrefabs == null || butterflyPrefabs.Length == 0) return;

            if (Time.time >= nextSpawnTime)
            {
                TriggerButterflySpawn();
                ScheduleNextTimer();
            }
        }

        private void ScheduleNextTimer()
        {
            nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        /// <summary>
        /// Calculates a weighted cluster count favoring natural distribution (solo, pairs, small groups).
        /// </summary>
        private int CalculateClusterCount()
        {
            float roll = Random.value;
            if (roll < 0.40f) return minClusterSize; // 40% Solo
            if (roll < 0.75f) return Mathf.Clamp(2, minClusterSize, maxClusterSize); // 35% Pair
            return Random.Range(Mathf.Clamp(3, minClusterSize, maxClusterSize), maxClusterSize + 1); // 25% Group
        }

        /// <summary>
        /// Spawns a randomized number of butterflies scattered across nearby vegetation and green terrain.
        /// </summary>
        public void TriggerButterflySpawn()
        {
            if (butterflyPrefabs == null || butterflyPrefabs.Length == 0) return;

            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0f;
            playerForward.Normalize();
            if (playerForward == Vector3.zero) playerForward = Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, playerForward).normalized;

            // Pick left or right side of path (green flora area)
            float sideSign = Random.value > 0.5f ? 1.0f : -1.0f;
            float sideOffset = Random.Range(5.0f, sideSpreadDistance) * sideSign;

            Vector3 baseZonePoint = playerTransform.position + playerForward * spawnDistanceAhead + right * sideOffset;

            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                baseZonePoint.y = activeTerrain.SampleHeight(baseZonePoint) + activeTerrain.transform.position.y;
            }

            // Verify position is OFF-PATH on green terrain
            if (pathConstraint != null && pathConstraint.IsPositionValid(baseZonePoint, out _))
            {
                baseZonePoint += right * 6.0f * sideSign;
                if (activeTerrain != null)
                {
                    baseZonePoint.y = activeTerrain.SampleHeight(baseZonePoint) + activeTerrain.transform.position.y;
                }
            }

            int clusterCount = CalculateClusterCount();

            for (int i = 0; i < clusterCount; i++)
            {
                GameObject prefab = butterflyPrefabs[Random.Range(0, butterflyPrefabs.Length)];
                if (prefab == null) continue;

                // Scatter each butterfly across the flora area
                Vector2 scatterCircle = Random.insideUnitCircle * scatterRadius;
                Vector3 candidateFloraPoint = baseZonePoint + new Vector3(scatterCircle.x, 0f, scatterCircle.y);

                // Sample terrain height
                if (activeTerrain != null)
                {
                    candidateFloraPoint.y = activeTerrain.SampleHeight(candidateFloraPoint) + activeTerrain.transform.position.y;
                }

                // Push off-path if accidentally sampled on dirt trail
                if (pathConstraint != null && pathConstraint.IsPositionValid(candidateFloraPoint, out _))
                {
                    candidateFloraPoint += right * 5.0f * sideSign;
                    if (activeTerrain != null)
                    {
                        candidateFloraPoint.y = activeTerrain.SampleHeight(candidateFloraPoint) + activeTerrain.transform.position.y;
                    }
                }

                // Detect nearby shrub, bush, or plant model to anchor hover center over vegetation
                Vector3 hoverAnchor = FindVegetationHoverAnchor(candidateFloraPoint);

                // Spawn position slightly offset from hover anchor
                Vector3 spawnPos = hoverAnchor + Random.insideUnitSphere * 1.5f;

                GameObject butterflyInstance = Instantiate(prefab, spawnPos, Quaternion.identity);
                ProceduralButterflyController controller = butterflyInstance.GetComponent<ProceduralButterflyController>();
                if (controller == null)
                {
                    controller = butterflyInstance.AddComponent<ProceduralButterflyController>();
                }

                float speed = Random.Range(1.4f, 2.4f);
                float meander = Random.Range(5.0f, 9.0f);
                controller.InitializeFlight(spawnPos, hoverAnchor, speed, meander);
            }
        }

        /// <summary>
        /// Searches for a nearby bush, shrub, or tree collider to anchor the butterfly's hovering center over foliage.
        /// </summary>
        private Vector3 FindVegetationHoverAnchor(Vector3 candidatePoint)
        {
            Collider[] hits = Physics.OverlapSphere(candidatePoint, plantSearchRadius, vegetationLayer, QueryTriggerInteraction.Collide);
            Transform bestPlant = null;
            float bestDistance = float.MaxValue;

            foreach (var col in hits)
            {
                if (col == null || col is TerrainCollider) continue;
                string name = col.gameObject.name.ToLower();
                if (name.Contains("shrub") || name.Contains("bush") || name.Contains("tree") || name.Contains("plant") || name.Contains("fern") || name.Contains("flower"))
                {
                    float dist = Vector3.Distance(candidatePoint, col.bounds.center);
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestPlant = col.transform;
                    }
                }
            }

            float hoverElevation = Random.Range(flightHeightRange.x, flightHeightRange.y);

            if (bestPlant != null)
            {
                // Anchor above the plant canopy
                return new Vector3(bestPlant.position.x, bestPlant.position.y + hoverElevation, bestPlant.position.z);
            }

            return candidatePoint + Vector3.up * hoverElevation;
        }

        private void OnDrawGizmosSelected()
        {
            if (playerTransform == null) return;

            Gizmos.color = Color.magenta;
            Vector3 targetPoint = playerTransform.position + playerTransform.forward * spawnDistanceAhead;
            Gizmos.DrawWireSphere(targetPoint, scatterRadius);
        }
    }
}
