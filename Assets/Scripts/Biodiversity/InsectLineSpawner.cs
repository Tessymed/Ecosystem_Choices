using System.Collections.Generic;
using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Spawns insect (e.g. ant, millipede) lines moving across the path at controlled intervals
    /// as participant advances through the terrain.
    /// Strictly limits concurrent insect crawlers to preserve VR frame rates and prevent physics/audio overhead.
    /// Selects realistic cover points (bushes, shrubs, foliage) for start and end positions
    /// so insects emerge from vegetation and disappear naturally into bushes.
    /// </summary>
    [AddComponentMenu("Biodiversity/Insect Line Spawner")]
    public class InsectLineSpawner : MonoBehaviour
    {
        [Header("Prefab & References")]
        [Tooltip("Ant or insect prefab(s) configured with InsectLineController.")]
        [SerializeField] private GameObject[] insectPrefabs;

        [Tooltip("Transform of participant / HMD camera. Auto-detects main camera if null.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Layer mask assigned to the path colliders / terrain path layer.")]
        [SerializeField] private LayerMask pathLayer = ~0;

        [Tooltip("Layer mask for foliage/shrubs/trees used as cover.")]
        [SerializeField] private LayerMask coverLayer = ~0;

        [Header("Concurrency & Throttling")]
        [Tooltip("Maximum concurrent active insect crawlers in the scene to preserve VR performance.")]
        [SerializeField] private int maxConcurrentInsects = 2;

        [Header("Spawn Timing & Distance Triggers")]
        [Tooltip("Enable timed random spawning.")]
        [SerializeField] private bool useTimerTrigger = true;

        [Tooltip("Minimum time (seconds) between ant/millipede crossings.")]
        [SerializeField] private float minSpawnInterval = 25.0f;

        [Tooltip("Maximum time (seconds) between ant/millipede crossings.")]
        [SerializeField] private float maxSpawnInterval = 60.0f;

        [Tooltip("Enable travel distance trigger.")]
        [SerializeField] private bool useDistanceTrigger = true;

        [Tooltip("Meters participant must travel to trigger an insect line crossing.")]
        [SerializeField] private float spawnDistanceInterval = 30.0f;

        [Header("Path Crossing Parameters")]
        [Tooltip("Distance ahead of participant to spawn insect crossing.")]
        [SerializeField] private float spawnAheadDistance = 9.0f;

        [Tooltip("Width of path crossing corridor (meters) to extend into off-trail vegetation.")]
        [SerializeField] private float pathCrossingWidth = 14.0f;

        [Tooltip("Crawling speed range (min, max m/s).")]
        [SerializeField] private Vector2 crawlSpeedRange = new Vector2(0.4f, 0.7f);

        [Tooltip("Search radius (meters) around trail edges to snap start/end points into nearby bushes/shrubs.")]
        [SerializeField] private float bushSearchRadius = 4.0f;

        private float nextSpawnTime;
        private Vector3 lastPlayerPosition;
        private float accumulatedDistance;
        private readonly List<InsectLineController> activeInsects = new List<InsectLineController>();

        private void Awake()
        {
            FindPlayerTransform();
        }

        private void Start()
        {
            ScheduleNextTimer();
            if (playerTransform != null)
            {
                lastPlayerPosition = playerTransform.position;
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
                GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) playerTransform = centerEye.transform;
                else
                {
                    GameObject player = GameObject.FindWithTag("Player");
                    if (player != null) playerTransform = player.transform;
                }
            }

            if (playerTransform != null)
            {
                lastPlayerPosition = playerTransform.position;
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                FindPlayerTransform();
                if (playerTransform == null) return;
            }

            if (insectPrefabs == null || insectPrefabs.Length == 0) return;

            // 1. Clean up despawned or destroyed insects
            for (int i = activeInsects.Count - 1; i >= 0; i--)
            {
                if (activeInsects[i] == null || activeInsects[i].State == InsectLineController.ControllerState.Despawned)
                {
                    activeInsects.RemoveAt(i);
                }
            }

            // 2. Track walking distance with protection against VR teleport and tracking jumps
            if (useDistanceTrigger)
            {
                float distMoved = Vector3.Distance(playerTransform.position, lastPlayerPosition);
                lastPlayerPosition = playerTransform.position;

                // Only accumulate continuous movement; ignore teleportation or camera jumps (> 4m)
                if (distMoved > 0.05f && distMoved < 4.0f)
                {
                    accumulatedDistance += distMoved;
                    if (accumulatedDistance >= spawnDistanceInterval)
                    {
                        accumulatedDistance = 0f;
                        if (activeInsects.Count < maxConcurrentInsects)
                        {
                            TriggerInsectCrossing();
                        }
                    }
                }
            }

            // 3. Check timer trigger
            if (useTimerTrigger && Time.time >= nextSpawnTime)
            {
                ScheduleNextTimer();
                if (activeInsects.Count < maxConcurrentInsects)
                {
                    TriggerInsectCrossing();
                }
            }
        }

        private void ScheduleNextTimer()
        {
            nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        /// <summary>
        /// Triggers an ant/millipede crossing event across the path ahead of the participant,
        /// originating from edge vegetation and vanishing into a bush or foliage.
        /// </summary>
        public void TriggerInsectCrossing()
        {
            if (insectPrefabs == null || insectPrefabs.Length == 0) return;
            if (activeInsects.Count >= maxConcurrentInsects) return;

            GameObject prefab = insectPrefabs[Random.Range(0, insectPrefabs.Length)];
            if (prefab == null) return;

            // 1. Determine path center location ahead of player
            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0f;
            playerForward.Normalize();

            if (playerForward == Vector3.zero) playerForward = Vector3.forward;

            Vector3 targetPathCenter = playerTransform.position + playerForward * spawnAheadDistance;

            // Raycast down to find exact path ground surface
            if (Physics.Raycast(targetPathCenter + Vector3.up * 8.0f, Vector3.down, out RaycastHit hit, 20.0f, pathLayer, QueryTriggerInteraction.Ignore))
            {
                targetPathCenter = hit.point;
            }

            // 2. Calculate cross-path perpendicular direction
            Vector3 crossPathDir = Vector3.Cross(Vector3.up, playerForward).normalized;

            // Randomly pick left-to-right or right-to-left
            bool leftToRight = Random.value > 0.5f;
            Vector3 startDir = leftToRight ? -crossPathDir : crossPathDir;
            Vector3 endDir = leftToRight ? crossPathDir : -crossPathDir;

            float halfSpan = pathCrossingWidth * 0.5f;
            Vector3 rawStartPos = targetPathCenter + startDir * halfSpan;
            Vector3 rawEndPos = targetPathCenter + endDir * halfSpan;

            // 3. Find natural bush / shrub / foliage cover near start and end
            Vector3 startPos = FindCoverPosition(rawStartPos, bushSearchRadius, startDir);
            Vector3 endPos = FindCoverPosition(rawEndPos, bushSearchRadius, endDir);

            // 4. Instantiate and initialize controller
            GameObject insectInstance = Instantiate(prefab, startPos, Quaternion.identity);
            InsectLineController controller = insectInstance.GetComponent<InsectLineController>();
            if (controller == null)
            {
                controller = insectInstance.AddComponent<InsectLineController>();
            }

            float speed = Random.Range(crawlSpeedRange.x, crawlSpeedRange.y);
            controller.InitializeCrossing(startPos, endPos, speed);

            activeInsects.Add(controller);
        }

        /// <summary>
        /// Searches for a nearby bush, shrub, or vegetation model to anchor natural entry/exit points.
        /// </summary>
        private Vector3 FindCoverPosition(Vector3 searchCenter, float searchRadius, Vector3 fallbackOutwardDir)
        {
            Collider[] hits = Physics.OverlapSphere(searchCenter, searchRadius, coverLayer, QueryTriggerInteraction.Collide);

            Transform bestCover = null;
            float bestScore = -1f;

            foreach (var col in hits)
            {
                if (col == null || col is TerrainCollider) continue;

                // Match against typical vegetation keywords
                string name = col.gameObject.name.ToLower();
                if (name.Contains("shrub") || name.Contains("bush") || name.Contains("plant") || name.Contains("tree") || name.Contains("foliage") || name.Contains("fern"))
                {
                    float dist = Vector3.Distance(searchCenter, col.bounds.center);
                    float score = searchRadius - dist;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCover = col.transform;
                    }
                }
            }

            if (bestCover != null)
            {
                Vector3 coverPoint = bestCover.position;
                if (Physics.Raycast(coverPoint + Vector3.up * 4f, Vector3.down, out RaycastHit groundHit, 10f, pathLayer, QueryTriggerInteraction.Ignore))
                {
                    return groundHit.point;
                }
                return coverPoint;
            }

            // Fallback: ground point along fallback outward vector
            Vector3 fallback = searchCenter + fallbackOutwardDir * 1.5f;
            if (Physics.Raycast(fallback + Vector3.up * 8f, Vector3.down, out RaycastHit fHit, 20f, pathLayer, QueryTriggerInteraction.Ignore))
            {
                return fHit.point;
            }

            return fallback;
        }
    }
}
