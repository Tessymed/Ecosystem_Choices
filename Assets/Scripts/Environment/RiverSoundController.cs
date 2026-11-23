using UnityEngine;

namespace EcosystemChoices.Environment
{
    /// <summary>
    /// Highly optimized 3D spatialized audio controller for a flowing canal/river in VR.
    /// Dynamically projects the 3D audio emitter to the closest point on the river segment(s)
    /// to the player's HMD. This provides realistic spatialization anywhere along the riverbank
    /// while using only a single AudioSource voice (optimal for Meta Quest / VR performance).
    /// </summary>
    [AddComponentMenu("Environment/River Sound Controller")]
    public class RiverSoundController : MonoBehaviour
    {
        [Header("Audio Clip & Settings")]
        [Tooltip("The looping river flow sound clip.")]
        [SerializeField] private AudioClip riverAudioClip;

        [Range(0f, 1f)]
        [SerializeField] private float maxVolume = 0.85f;

        [Tooltip("Distance from river bank where sound is at maximum volume (meters).")]
        [SerializeField] private float minDistance = 3.0f;

        [Tooltip("Distance from river bank where sound fades to silent (meters).")]
        [SerializeField] private float maxDistance = 25.0f;

        [Tooltip("Stereo spread angle in degrees (0 = point source, 60 = wide flowing river).")]
        [Range(0f, 180f)]
        [SerializeField] private float audioSpread = 60.0f;

        [Header("River Path Definition")]
        [Tooltip("Start point of the canal ridge line (in world coordinates).")]
        [SerializeField] private Vector3 riverStartPoint = new Vector3(0f, 2.5f, 51.1f);

        [Tooltip("End point of the canal ridge line (in world coordinates).")]
        [SerializeField] private Vector3 riverEndPoint = new Vector3(100f, 2.5f, 51.1f);

        [Tooltip("Optional intermediate waypoints for winding/curved canals.")]
        [SerializeField] private Vector3[] pathWaypoints;

        [Header("Performance & Tracking")]
        [Tooltip("How smoothly the sound emitter tracks along the river line to follow the player.")]
        [SerializeField] private float trackingSmoothSpeed = 10.0f;

        [Tooltip("Distance check interval to cull audio calculations when player is very far away.")]
        [SerializeField] private float cullingDistance = 35.0f;

        private AudioSource riverAudioSource;
        private Transform playerTransform;
        private Transform emitterTransform;
        private bool isAudioActive = true;

        public AudioClip RiverClip { get => riverAudioClip; set => riverAudioClip = value; }
        public Vector3 RiverStart { get => riverStartPoint; set => riverStartPoint = value; }
        public Vector3 RiverEnd { get => riverEndPoint; set => riverEndPoint = value; }

        private void Awake()
        {
            SetupAudioEmitter();
        }

        private void Start()
        {
            LocatePlayer();
            EnsureAudioPlaying();
        }

        private void SetupAudioEmitter()
        {
            // Create a dedicated child transform for the moving 3D sound origin
            GameObject emitterObj = new GameObject("River_3D_AudioEmitter");
            emitterObj.transform.SetParent(transform);
            emitterTransform = emitterObj.transform;
            emitterTransform.position = (riverStartPoint + riverEndPoint) * 0.5f;

            riverAudioSource = emitterObj.AddComponent<AudioSource>();
            riverAudioSource.clip = riverAudioClip;
            riverAudioSource.loop = true;
            riverAudioSource.playOnAwake = false;

            // Full 3D spatialization for VR
            riverAudioSource.spatialBlend = 1.0f;
            riverAudioSource.spatialize = true;
            riverAudioSource.spatializePostEffects = false;
            riverAudioSource.spread = audioSpread;

            // Logarithmic/Smooth custom rolloff from bank (minDistance) to forest (maxDistance)
            riverAudioSource.rolloffMode = AudioRolloffMode.Custom;
            AnimationCurve customCurve = new AnimationCurve();
            customCurve.AddKey(0f, 1f);
            customCurve.AddKey(minDistance / maxDistance, 1f);
            customCurve.AddKey(Mathf.Lerp(minDistance / maxDistance, 1f, 0.35f), 0.45f);
            customCurve.AddKey(1f, 0f);
            riverAudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customCurve);

            riverAudioSource.minDistance = minDistance;
            riverAudioSource.maxDistance = maxDistance;
            riverAudioSource.volume = maxVolume;
            riverAudioSource.dopplerLevel = 0f; // River doesn't move relative to earth
            riverAudioSource.bypassReverbZones = false;
        }

        private void LocatePlayer()
        {
            if (playerTransform == null)
            {
                // 1. Try finding CenterEyeAnchor (HMD)
                GameObject hmd = GameObject.Find("CenterEyeAnchor");
                if (hmd != null)
                {
                    playerTransform = hmd.transform;
                    return;
                }

                // 2. Try finding MainCamera
                Camera cam = Camera.main;
                if (cam != null)
                {
                    playerTransform = cam.transform;
                    return;
                }

                // 3. Fallback to Player tagged object
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                {
                    playerTransform = player.transform;
                }
            }
        }

        public void EnsureAudioPlaying()
        {
            if (riverAudioSource != null && riverAudioClip != null)
            {
                if (riverAudioSource.clip != riverAudioClip)
                {
                    riverAudioSource.clip = riverAudioClip;
                }

                if (!riverAudioSource.isPlaying)
                {
                    riverAudioSource.Play();
                }
            }
        }

        private void Update()
        {
            if (playerTransform == null)
            {
                LocatePlayer();
                if (playerTransform == null) return;
            }

            Vector3 playerPos = playerTransform.position;
            Vector3 nearestPoint = GetClosestPointOnRiver(playerPos);
            float distToRiver = Vector3.Distance(playerPos, nearestPoint);

            // Optimization: Cull update when far beyond audible range
            if (distToRiver > cullingDistance)
            {
                if (isAudioActive && riverAudioSource.isPlaying)
                {
                    riverAudioSource.Pause();
                    isAudioActive = false;
                }
                return;
            }
            else
            {
                if (!isAudioActive)
                {
                    riverAudioSource.UnPause();
                    if (!riverAudioSource.isPlaying) riverAudioSource.Play();
                    isAudioActive = true;
                }
            }

            // Smoothly slide the 3D audio source along the river directly facing the player
            if (emitterTransform != null)
            {
                emitterTransform.position = Vector3.Lerp(
                    emitterTransform.position,
                    nearestPoint,
                    Time.deltaTime * trackingSmoothSpeed
                );
            }
        }

        /// <summary>
        /// Finds the closest point along the river line/waypoints to the given position.
        /// </summary>
        public Vector3 GetClosestPointOnRiver(Vector3 position)
        {
            if (pathWaypoints == null || pathWaypoints.Length < 2)
            {
                return ClosestPointOnLineSegment(riverStartPoint, riverEndPoint, position);
            }

            Vector3 bestPoint = riverStartPoint;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < pathWaypoints.Length - 1; i++)
            {
                Vector3 pt = ClosestPointOnLineSegment(pathWaypoints[i], pathWaypoints[i + 1], position);
                float sqrDist = (position - pt).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    bestPoint = pt;
                }
            }

            return bestPoint;
        }

        private static Vector3 ClosestPointOnLineSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float sqrLen = ab.sqrMagnitude;
            if (sqrLen < 0.0001f) return a;

            float t = Vector3.Dot(point - a, ab) / sqrLen;
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.8f);
            if (pathWaypoints != null && pathWaypoints.Length > 1)
            {
                for (int i = 0; i < pathWaypoints.Length - 1; i++)
                {
                    Gizmos.DrawLine(pathWaypoints[i], pathWaypoints[i + 1]);
                    Gizmos.DrawWireSphere(pathWaypoints[i], 0.5f);
                }
                Gizmos.DrawWireSphere(pathWaypoints[pathWaypoints.Length - 1], 0.5f);
            }
            else
            {
                Gizmos.DrawLine(riverStartPoint, riverEndPoint);
                Gizmos.DrawWireSphere(riverStartPoint, 0.6f);
                Gizmos.DrawWireSphere(riverEndPoint, 0.6f);
            }

            if (emitterTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(emitterTransform.position, 0.8f);
            }
        }
    }
}
