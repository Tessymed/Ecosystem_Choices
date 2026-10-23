using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Spawns bird flocks in the sky at random times and locations across the terrain
    /// based on participant movement and progress.
    /// </summary>
    [AddComponentMenu("Biodiversity/Bird Flock Spawner")]
    public class BirdFlockSpawner : MonoBehaviour
    {
        [Header("Prefab & References")]
        [Tooltip("Bird flock prefab(s) with empty controller object or FBX bird model.")]
        [SerializeField] private GameObject[] birdFlockPrefabs;

        [Tooltip("Transform of participant / HMD camera. Auto-detects main camera if null.")]
        [SerializeField] private Transform playerTransform;

        [Header("Spawn Timing & Distance Triggers")]
        [Tooltip("Enable timed random spawning.")]
        [SerializeField] private bool useTimerTrigger = true;

        [Tooltip("Minimum time (seconds) between bird spawns.")]
        [SerializeField] private float minSpawnInterval = 15.0f;

        [Tooltip("Maximum time (seconds) between bird spawns.")]
        [SerializeField] private float maxSpawnInterval = 45.0f;

        [Tooltip("Enable travel distance trigger.")]
        [SerializeField] private bool useDistanceTrigger = true;

        [Tooltip("Meters participant must travel to trigger a bird flyby.")]
        [SerializeField] private float spawnDistanceInterval = 35.0f;

        [Header("Sky Position & Flight Settings")]
        [Tooltip("Minimum height above ground level for bird flight.")]
        [SerializeField] private float minHeightOffset = 10.0f;

        [Tooltip("Maximum height above ground level for bird flight.")]
        [SerializeField] private float maxHeightOffset = 25.0f;

        [Tooltip("Distance ahead of player to spawn flight path.")]
        [SerializeField] private float spawnDistanceAhead = 30.0f;

        [Tooltip("Flight speed range (min, max).")]
        [SerializeField] private Vector2 speedRange = new Vector2(10.0f, 18.0f);

        [Header("Audio")]
        [Tooltip("Optional 3D audio clip played by the flock in flight (e.g. Birds_At_4am).")]
        [SerializeField] private AudioClip flockSound;

        private float nextSpawnTime;
        private Vector3 lastPlayerPosition;
        private float accumulatedDistance;

        public AudioClip FlockSound { get => flockSound; set => flockSound = value; }

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
        }

        private void Start()
        {
            ScheduleNextTimer();
            if (playerTransform != null)
            {
                lastPlayerPosition = playerTransform.position;
            }
        }

        private void Update()
        {
            if (playerTransform == null || birdFlockPrefabs == null || birdFlockPrefabs.Length == 0) return;

            // 1. Check timer trigger
            if (useTimerTrigger && Time.time >= nextSpawnTime)
            {
                TriggerBirdFlock();
                ScheduleNextTimer();
            }

            // 2. Check distance trigger
            if (useDistanceTrigger)
            {
                float distMoved = Vector3.Distance(playerTransform.position, lastPlayerPosition);
                accumulatedDistance += distMoved;
                lastPlayerPosition = playerTransform.position;

                if (accumulatedDistance >= spawnDistanceInterval)
                {
                    accumulatedDistance = 0f;
                    TriggerBirdFlock();
                }
            }
        }

        private void ScheduleNextTimer()
        {
            nextSpawnTime = Time.time + Random.Range(minSpawnInterval, maxSpawnInterval);
        }

        /// <summary>
        /// Manually or automatically spawns a bird flock flying across the sky.
        /// </summary>
        public void TriggerBirdFlock()
        {
            if (birdFlockPrefabs == null || birdFlockPrefabs.Length == 0) return;

            // Select random bird prefab
            GameObject prefab = birdFlockPrefabs[Random.Range(0, birdFlockPrefabs.Length)];
            if (prefab == null) return;

            // Compute spawn position in sky ahead of player
            Vector3 playerForward = playerTransform.forward;
            playerForward.y = 0f;
            playerForward.Normalize();

            if (playerForward == Vector3.zero) playerForward = Vector3.forward;

            // Random angle offset (-60 to +60 degrees)
            float randomAngle = Random.Range(-60.0f, 60.0f);
            Quaternion rotationOffset = Quaternion.Euler(0f, randomAngle, 0f);
            Vector3 spawnDir = rotationOffset * playerForward;

            float height = Random.Range(minHeightOffset, maxHeightOffset);
            Vector3 spawnPos = playerTransform.position + spawnDir * spawnDistanceAhead + Vector3.up * height;

            // Compute flight trajectory across sky (perpendicular or angled)
            float flightAngle = Random.Range(-45.0f, 45.0f);
            Vector3 flightDir = Quaternion.Euler(0f, flightAngle, 0f) * playerForward;

            GameObject birdInstance = Instantiate(prefab, spawnPos, Quaternion.LookRotation(flightDir));
            BirdFlockController controller = birdInstance.GetComponent<BirdFlockController>();
            if (controller == null)
            {
                controller = birdInstance.AddComponent<BirdFlockController>();
            }

            float speed = Random.Range(speedRange.x, speedRange.y);
            controller.InitializeFlight(flightDir, speed, flockSound);
        }

        private void OnDrawGizmosSelected()
        {
            if (playerTransform == null) return;

            Gizmos.color = Color.cyan;
            Vector3 targetSkyPoint = playerTransform.position + playerTransform.forward * spawnDistanceAhead + Vector3.up * ((minHeightOffset + maxHeightOffset) * 0.5f);
            Gizmos.DrawWireSphere(targetSkyPoint, 5.0f);
        }
    }
}
