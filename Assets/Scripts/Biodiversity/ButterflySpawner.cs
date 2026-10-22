using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Spawns butterflies in green flora areas of the terrain (away from the painted path)
    /// dancing around trees, flowers, and foliage as participant explores.
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

        [Header("Spawn Settings")]
        [Tooltip("Minimum time (seconds) between butterfly spawns.")]
        [SerializeField] private float minSpawnInterval = 12.0f;

        [Tooltip("Maximum time (seconds) between butterfly spawns.")]
        [SerializeField] private float maxSpawnInterval = 35.0f;

        [Tooltip("Distance ahead of player to spawn butterfly zone.")]
        [SerializeField] private float spawnDistanceAhead = 12.0f;

        [Tooltip("Side spread distance (meters) away from path on green terrain.")]
        [SerializeField] private float sideSpreadDistance = 15.0f;

        [Tooltip("Min and max height above ground level for flight.")]
        [SerializeField] private Vector2 flightHeightRange = new Vector2(1.0f, 3.5f);

        [Tooltip("Max butterflies per spawn cluster (1 = solo, 2-4 = group).")]
        [Range(1, 6)]
        [SerializeField] private int maxClusterSize = 3;

        private float nextSpawnTime;

        private void Awake()
        {
            if (playerTransform == null)
            {
                if (Camera.main != null) playerTransform = Camera.main.transform;
                else
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null) playerTransform = player.transform;
                }
            }

            if (pathConstraint == null)
            {
                pathConstraint = FindAnyObjectByType<EcosystemChoices.Locomotion.PathMovementConstraint>();
            }
        }

        private void Start()
        {
            ScheduleNextTimer();
        }

        private void Update()
        {
            if (playerTransform == null || butterflyPrefabs == null || butterflyPrefabs.Length == 0) return;

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
        /// Spawns a butterfly or butterfly cluster on the green side of the terrain.
        /// </summary>
        public void TriggerButterflySpawn()
        {
            if (butterflyPrefabs == null || butterflyPrefabs.Length == 0) return;

            GameObject prefab = butterflyPrefabs[Random.Range(0, butterflyPrefabs.Length)];
            if (prefab == null) return;

            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0f;
            playerForward.Normalize();
            if (playerForward == Vector3.zero) playerForward = Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, playerForward).normalized;

            // Pick left or right side of path (green flora area)
            float sideSign = Random.value > 0.5f ? 1.0f : -1.0f;
            float sideOffset = Random.Range(4.0f, sideSpreadDistance) * sideSign;

            Vector3 groundPoint = playerTransform.position + playerForward * spawnDistanceAhead + right * sideOffset;

            // Sample ground height on terrain
            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                groundPoint.y = activeTerrain.SampleHeight(groundPoint) + activeTerrain.transform.position.y;
            }

            // Verify position is OFF-PATH on green terrain if pathConstraint is available
            if (pathConstraint != null && pathConstraint.IsPositionValid(groundPoint, out _))
            {
                // Push further into green flora area
                groundPoint += right * 6.0f * sideSign;
                if (activeTerrain != null)
                {
                    groundPoint.y = activeTerrain.SampleHeight(groundPoint) + activeTerrain.transform.position.y;
                }
            }

            int clusterCount = Random.Range(1, maxClusterSize + 1);
            Vector3 hoverCenter = groundPoint + Vector3.up * Random.Range(flightHeightRange.x, flightHeightRange.y);

            for (int i = 0; i < clusterCount; i++)
            {
                Vector3 spawnPos = hoverCenter + Random.insideUnitSphere * 2.0f;
                GameObject butterflyInstance = Instantiate(prefab, spawnPos, Quaternion.identity);

                ProceduralButterflyController controller = butterflyInstance.GetComponent<ProceduralButterflyController>();
                if (controller == null)
                {
                    controller = butterflyInstance.AddComponent<ProceduralButterflyController>();
                }

                controller.InitializeFlight(spawnPos, hoverCenter);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (playerTransform == null) return;

            Gizmos.color = Color.magenta;
            Vector3 targetPoint = playerTransform.position + playerTransform.forward * spawnDistanceAhead;
            Gizmos.DrawWireSphere(targetPoint, sideSpreadDistance);
        }
    }
}
