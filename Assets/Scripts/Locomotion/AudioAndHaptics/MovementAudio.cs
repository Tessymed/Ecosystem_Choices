using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Master audio director for locomotion and ambient ecosystem soundscapes.
    /// Features:
    /// 1. Dynamic continuous & discrete footstep audio with velocity/acceleration pitch scaling,
    ///    instant movement gating, and complete silence when stationary.
    /// 2. Multi-surface acoustic detection with silky-smooth equal-power crossfading between
    ///    terrain path footsteps and wooden bridge plank footsteps.
    /// 3. Immersive ambient soundscape with organic multi-frequency breathing waves (flows & wavelengths),
    ///    locomotion-reactive atmospheric swell, and subtle pitch drift.
    /// 4. 3D spatial canopy bird soundscape with calls emerging from tree branches surrounding the player in VR.
    /// 5. Automatic AudioListener safety verification so sound never fails to play.
    /// </summary>
    [AddComponentMenu("Locomotion/Movement Audio")]
    [RequireComponent(typeof(AudioSource))]
    public class MovementAudio : MonoBehaviour
    {
        public enum FootstepPlaybackMode
        {
            ContinuousTrack,
            DiscreteStepClips,
            AutoDetect
        }

        [Header("Terrain Path Footsteps")]
        [Tooltip("Continuous walking audio track on terrain / gravel (e.g. Footsteps_Gravel_Grass_Continuous).")]
        [SerializeField] private AudioClip continuousFootstepClip;

        [Tooltip("Array of discrete footstep clips (used when no continuous clip is provided or in discrete mode).")]
        [SerializeField] private AudioClip[] footstepClips;

        [Tooltip("Footstep playback mode.")]
        [SerializeField] private FootstepPlaybackMode playbackMode = FootstepPlaybackMode.AutoDetect;

        [Tooltip("Master volume multiplier for terrain footstep audio.")]
        [Range(0f, 1f)]
        [SerializeField] private float footstepVolume = 0.55f;

        [Tooltip("Minimum movement speed in m/s before footstep sound activates.")]
        [SerializeField] private float minMoveSpeed = 0.08f;

        [Tooltip("Reference walking speed in m/s for maximum pitch and volume scaling.")]
        [SerializeField] private float referenceWalkSpeed = 2.2f;

        [Tooltip("Dynamic pitch scaling range (X = slow creeping, Y = brisk walking/sprint).")]
        [SerializeField] private Vector2 pitchRange = new Vector2(0.88f, 1.22f);

        [Tooltip("Fade-in speed multiplier when movement begins.")]
        [SerializeField] private float footstepFadeInSpeed = 9.0f;

        [Tooltip("Fade-out speed multiplier to instant silence when movement stops.")]
        [SerializeField] private float footstepFadeOutSpeed = 7.5f;

        [Tooltip("Distance in meters between discrete one-shot footsteps (used when discrete mode is active).")]
        [SerializeField] private float stepDistance = 1.4f;

        [Header("Wooden Bridge Footsteps")]
        [Tooltip("Continuous walking audio track on wooden bridge (e.g. footsteps-on-wood-397989).")]
        [SerializeField] private AudioClip woodFootstepClip;

        [Tooltip("Array of discrete wood footstep clips (optional fallback for discrete mode).")]
        [SerializeField] private AudioClip[] woodFootstepClips;

        [Tooltip("Relative volume multiplier for wooden bridge footsteps.")]
        [Range(0f, 1f)]
        [SerializeField] private float woodFootstepVolume = 0.50f;

        [Tooltip("Speed of crossfading between terrain path footsteps and wooden bridge footsteps in units/sec (3.0 = ~0.33s smooth transition).")]
        [SerializeField] private float surfaceCrossfadeSpeed = 3.0f;

        [Header("Ecosystem Ambient Soundscape (Flows & Wavelengths)")]
        [Tooltip("AudioSource for ambient ecosystem background audio. Auto-created if null.")]
        [SerializeField] private AudioSource ambientAudioSource;

        [Tooltip("Ambient nature / atmosphere clip (e.g. Ambience_Nature_Serbia).")]
        [SerializeField] private AudioClip ambientSoundscape;

        [Tooltip("Target baseline volume for ambient background soundscape.")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientMaxVolume = 0.38f;

        [Tooltip("Enable dynamic atmospheric wave modulation (organic slow LFO mimicking passing breezes).")]
        [SerializeField] private bool enableOrganicAtmosphere = true;

        [Tooltip("Cycle duration in seconds for primary ambient flow wave (wavelength 1).")]
        [SerializeField] private float ambientWaveCycle1 = 17.5f;

        [Tooltip("Cycle duration in seconds for secondary ambient flow wave (wavelength 2).")]
        [SerializeField] private float ambientWaveCycle2 = 29.0f;

        [Tooltip("Depth of atmospheric volume wave ebb & flow (0 = static, 0.35 = rich breeze swells).")]
        [Range(0f, 0.4f)]
        [SerializeField] private float ambientWaveDepth = 0.18f;

        [Tooltip("Subtle atmospheric pitch drift across the breeze cycle (percentage, e.g. 0.015 = +/-1.5%).")]
        [Range(0f, 0.04f)]
        [SerializeField] private float ambientPitchDrift = 0.015f;

        [Tooltip("Increase ambient air presence slightly when moving through ecosystem.")]
        [SerializeField] private bool dynamicMovementAmbiance = true;

        [Tooltip("Movement ambiance volume boost multiplier.")]
        [Range(1.0f, 1.8f)]
        [SerializeField] private float movementAmbianceBoost = 1.25f;

        [Header("3D Spatial Canopy Bird Soundscape")]
        [Tooltip("Clip for morning forest bird calls / chirps.")]
        [SerializeField] private AudioClip canopyBirdClip;

        [Tooltip("Enable randomized 3D spatial bird calls originating from tree canopies around the player.")]
        [SerializeField] private bool enableCanopyBirds = true;

        [Tooltip("Volume for 3D canopy bird calls.")]
        [Range(0f, 1f)]
        [SerializeField] private float canopyBirdVolume = 0.42f;

        [Tooltip("Time interval range (seconds) between bird calls.")]
        [SerializeField] private Vector2 canopyCallInterval = new Vector2(8.0f, 20.0f);

        [Tooltip("Distance range (meters) in tree canopy around player to position bird sound.")]
        [SerializeField] private Vector2 canopyBirdDistance = new Vector2(9.0f, 22.0f);

        [Tooltip("Elevation range (meters above player) in tree branches for bird sound.")]
        [SerializeField] private Vector2 canopyBirdHeight = new Vector2(4.5f, 11.0f);

        [Header("References")]
        [SerializeField] private ContinuousLocomotion continuousLocomotion;
        [SerializeField] private GroundDetector groundDetector;

        [Header("Audio Listener & VR Setup")]
        [Tooltip("Ensure an AudioListener is active on the VR Camera / HMD.")]
        [SerializeField] private bool autoEnsureAudioListener = true;

        // Internal audio sources & state
        private AudioSource discreteFootstepSource;
        private AudioSource continuousFootstepSource;
        private AudioSource woodFootstepSource;
        private AudioSource canopyBirdSource;
        private CharacterController characterController;

        private Vector3 lastPosition;
        private float distanceTraveled;
        private float currentContinuousVol = 0f;
        private float currentSurfaceBlend = 0f; // 0.0 = 100% terrain path, 1.0 = 100% wooden bridge
        private float currentAmbientVolume;
        private float canopyBirdTimer;
        private float canopyPhraseStopTimer;
        private bool isCanopyPlaying;

        private static readonly Collider[] overlapBuffer = new Collider[8];

        public AudioClip AmbientSoundscape
        {
            get => ambientSoundscape;
            set
            {
                ambientSoundscape = value;
                if (ambientAudioSource != null && ambientSoundscape != null)
                {
                    ambientAudioSource.clip = ambientSoundscape;
                    if (!ambientAudioSource.isPlaying) ambientAudioSource.Play();
                }
            }
        }

        public AudioClip ContinuousFootstepClip
        {
            get => continuousFootstepClip;
            set
            {
                continuousFootstepClip = value;
                if (continuousFootstepSource != null && continuousFootstepClip != null)
                {
                    continuousFootstepSource.clip = continuousFootstepClip;
                }
            }
        }

        public AudioClip WoodFootstepClip
        {
            get => woodFootstepClip;
            set
            {
                woodFootstepClip = value;
                if (woodFootstepSource != null && woodFootstepClip != null)
                {
                    woodFootstepSource.clip = woodFootstepClip;
                }
            }
        }

        public AudioClip CanopyBirdClip
        {
            get => canopyBirdClip;
            set => canopyBirdClip = value;
        }

        public float CurrentSurfaceBlend => currentSurfaceBlend;
        public bool IsOnWoodBridge => currentSurfaceBlend > 0.5f;

        private void Awake()
        {
            discreteFootstepSource = GetComponent<AudioSource>();
            if (continuousLocomotion == null) continuousLocomotion = GetComponentInParent<ContinuousLocomotion>();
            if (groundDetector == null) groundDetector = GetComponentInParent<GroundDetector>();
            if (characterController == null) characterController = GetComponentInParent<CharacterController>();

            lastPosition = transform.position;

            if (autoEnsureAudioListener)
            {
                EnsureAudioListener();
            }

            InitializeContinuousFootstepAudio();
            InitializeWoodFootstepAudio();
            InitializeAmbientAudio();
            InitializeCanopyBirdAudio();
        }

        private void EnsureAudioListener()
        {
            AudioListener existing = FindAnyObjectByType<AudioListener>();
            if (existing == null)
            {
                Camera cam = GetComponentInChildren<Camera>();
                if (cam == null) cam = Camera.main;

                if (cam != null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                    Debug.Log($"[MovementAudio] Automatically added AudioListener to {cam.gameObject.name} for VR immersion.");
                }
                else
                {
                    gameObject.AddComponent<AudioListener>();
                    Debug.Log("[MovementAudio] Added fallback AudioListener to locomotion rig.");
                }
            }
        }

        private void InitializeContinuousFootstepAudio()
        {
            if (continuousFootstepSource == null)
            {
                GameObject footstepGO = new GameObject("ContinuousFootstepAudio");
                footstepGO.transform.SetParent(transform, false);
                continuousFootstepSource = footstepGO.AddComponent<AudioSource>();
                continuousFootstepSource.playOnAwake = false;
                continuousFootstepSource.loop = true;
                continuousFootstepSource.spatialBlend = 0.2f; // Subtle stereo width beneath body
                continuousFootstepSource.volume = 0f;
            }

            if (continuousFootstepClip != null)
            {
                continuousFootstepSource.clip = continuousFootstepClip;
            }
        }

        private void InitializeWoodFootstepAudio()
        {
            if (woodFootstepSource == null)
            {
                GameObject woodGO = new GameObject("WoodFootstepAudio");
                woodGO.transform.SetParent(transform, false);
                woodFootstepSource = woodGO.AddComponent<AudioSource>();
                woodFootstepSource.playOnAwake = false;
                woodFootstepSource.loop = true;
                woodFootstepSource.spatialBlend = 0.2f; // Subtle stereo width beneath body
                woodFootstepSource.volume = 0f;
            }

#if UNITY_EDITOR
            if (woodFootstepClip == null)
            {
                woodFootstepClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Locomotion/footsteps-on-wood-397989.mp3");
            }
#endif

            if (woodFootstepClip != null)
            {
                woodFootstepSource.clip = woodFootstepClip;
            }
        }

        private void InitializeAmbientAudio()
        {
            if (ambientAudioSource == null)
            {
                GameObject ambientGO = new GameObject("AmbientEcosystemAudio");
                ambientGO.transform.SetParent(transform, false);
                ambientAudioSource = ambientGO.AddComponent<AudioSource>();
                ambientAudioSource.playOnAwake = false;
                ambientAudioSource.loop = true;
                ambientAudioSource.spatialBlend = 0.0f; // 2D stereo bed for enveloping presence
            }

            if (ambientSoundscape != null)
            {
                ambientAudioSource.clip = ambientSoundscape;
                ambientAudioSource.volume = ambientMaxVolume * 0.5f;
                ambientAudioSource.Play();
            }
        }

        private void InitializeCanopyBirdAudio()
        {
            if (canopyBirdSource == null)
            {
                GameObject birdGO = new GameObject("CanopyBirdSpatialAudio");
                birdGO.transform.SetParent(transform, false);
                canopyBirdSource = birdGO.AddComponent<AudioSource>();
                canopyBirdSource.playOnAwake = false;
                canopyBirdSource.loop = false;
                canopyBirdSource.spatialBlend = 1.0f; // Full 3D spatial audio
                canopyBirdSource.rolloffMode = AudioRolloffMode.Logarithmic;
                canopyBirdSource.minDistance = 3.0f;
                canopyBirdSource.maxDistance = 50.0f;
                canopyBirdSource.dopplerLevel = 0.5f;
            }

            canopyBirdTimer = Random.Range(canopyCallInterval.x, canopyCallInterval.y);
        }

        private void Update()
        {
            Vector3 currentPos = transform.position;
            Vector3 horizontalDelta = currentPos - lastPosition;
            horizontalDelta.y = 0f;

            float distanceThisFrame = horizontalDelta.magnitude;
            float currentSpeed = Time.deltaTime > 0f ? (distanceThisFrame / Time.deltaTime) : 0f;
            lastPosition = currentPos;

            bool isGrounded = groundDetector != null ? groundDetector.IsGrounded : true;
            bool isMoving = isGrounded && (currentSpeed > minMoveSpeed);

            // 1. Process Multi-Surface Footstep Audio with Smooth Crossfading
            UpdateFootstepAudio(isMoving, currentSpeed, distanceThisFrame, isGrounded);

            // 2. Process Organic Ambient Flows & Wavelengths
            UpdateAmbientAudio(isMoving);

            // 3. Process 3D Spatial Canopy Birds
            UpdateCanopyBirdAudio();
        }

        private void UpdateFootstepAudio(bool isMoving, float currentSpeed, float distanceThisFrame, bool isGrounded)
        {
            bool useContinuous = false;
            if (playbackMode == FootstepPlaybackMode.ContinuousTrack) useContinuous = true;
            else if (playbackMode == FootstepPlaybackMode.DiscreteStepClips) useContinuous = false;
            else useContinuous = (continuousFootstepClip != null || woodFootstepClip != null);

            // 1. Detect if player is on wooden bridge surface
            bool onWoodBridge = DetectWoodBridgeSurface();
            float targetSurfaceBlend = onWoodBridge ? 1.0f : 0.0f;

            // 2. Smoothly crossfade surface blend factor
            currentSurfaceBlend = Mathf.MoveTowards(
                currentSurfaceBlend,
                targetSurfaceBlend,
                Time.deltaTime * surfaceCrossfadeSpeed
            );

            // 3. Equal-Power crossfade weights (guarantees constant acoustic power, no mid-transition volume dip)
            float blendRad = currentSurfaceBlend * (Mathf.PI * 0.5f);
            float terrainWeight = Mathf.Cos(blendRad);
            float woodWeight = Mathf.Sin(blendRad);

            if (useContinuous)
            {
                if (isMoving)
                {
                    // Start playback on sources if within audible crossfade window
                    if (continuousFootstepSource != null && continuousFootstepClip != null)
                    {
                        if (!continuousFootstepSource.isPlaying && terrainWeight > 0.001f)
                        {
                            continuousFootstepSource.Play();
                        }
                    }

                    if (woodFootstepSource != null && woodFootstepClip != null)
                    {
                        if (!woodFootstepSource.isPlaying && woodWeight > 0.001f)
                        {
                            woodFootstepSource.Play();
                        }
                    }

                    // Dynamic pitch & volume scaling based on velocity/acceleration
                    float speedFactor = Mathf.Clamp01((currentSpeed - minMoveSpeed) / Mathf.Max(0.1f, referenceWalkSpeed - minMoveSpeed));
                    float targetMovementVol = Mathf.Lerp(0.6f, 1.0f, speedFactor);
                    float targetPitch = Mathf.Lerp(pitchRange.x, pitchRange.y, speedFactor);

                    currentContinuousVol = Mathf.MoveTowards(currentContinuousVol, targetMovementVol, Time.deltaTime * footstepFadeInSpeed);

                    if (continuousFootstepSource != null)
                    {
                        continuousFootstepSource.volume = currentContinuousVol * footstepVolume * terrainWeight;
                        continuousFootstepSource.pitch = Mathf.Lerp(continuousFootstepSource.pitch, targetPitch, Time.deltaTime * 6.0f);
                    }

                    if (woodFootstepSource != null)
                    {
                        woodFootstepSource.volume = currentContinuousVol * woodFootstepVolume * woodWeight;
                        woodFootstepSource.pitch = Mathf.Lerp(woodFootstepSource.pitch, targetPitch, Time.deltaTime * 6.0f);
                    }
                }
                else
                {
                    // Instant smooth fade-out to complete silence when stopped
                    currentContinuousVol = Mathf.MoveTowards(currentContinuousVol, 0f, Time.deltaTime * footstepFadeOutSpeed);

                    if (continuousFootstepSource != null)
                    {
                        continuousFootstepSource.volume = currentContinuousVol * footstepVolume * terrainWeight;
                        if (currentContinuousVol <= 0.001f && continuousFootstepSource.isPlaying)
                        {
                            continuousFootstepSource.Pause();
                        }
                    }

                    if (woodFootstepSource != null)
                    {
                        woodFootstepSource.volume = currentContinuousVol * woodFootstepVolume * woodWeight;
                        if (currentContinuousVol <= 0.001f && woodFootstepSource.isPlaying)
                        {
                            woodFootstepSource.Pause();
                        }
                    }
                }
            }
            else
            {
                // Discrete step fallback (per step distance)
                if (isGrounded && isMoving && distanceThisFrame > 0.001f)
                {
                    distanceTraveled += distanceThisFrame;
                    if (distanceTraveled >= stepDistance)
                    {
                        PlayDiscreteFootstepSound(terrainWeight, woodWeight);
                        distanceTraveled = 0f;
                    }
                }
                else if (!isMoving)
                {
                    distanceTraveled = 0f;
                }
            }
        }

        public bool DetectWoodBridgeSurface()
        {
            // A. Direct Raycast / Spherecast hit from GroundDetector beneath feet
            if (groundDetector != null && groundDetector.IsGrounded)
            {
                RaycastHit hit = groundDetector.SurfaceHit;
                if (hit.collider != null && IsBridgeCollider(hit.collider))
                {
                    return true;
                }
            }

            // B. Spatial bounds query against all active WoodenBridgeSurfaces in the scene
            Vector3 feetPos = transform.position;
            if (EcosystemChoices.Environment.WoodenBridgeSurface.IsPointOnAnyBridge(feetPos))
            {
                return true;
            }

            // C. Localized feet overlap sphere check
            int count = Physics.OverlapSphereNonAlloc(feetPos + Vector3.up * 0.25f, 0.45f, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = overlapBuffer[i];
                if (c == null) continue;
                if (characterController != null && c == characterController) continue;

                if (IsBridgeCollider(c))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBridgeCollider(Collider col)
        {
            if (col == null) return false;
            if (col.GetComponentInParent<EcosystemChoices.Environment.WoodenBridgeSurface>() != null) return true;

            string colName = col.gameObject.name;
            if (colName.IndexOf("bridge", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (colName.IndexOf("wood", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

            Transform root = col.transform.root;
            if (root != null && root.name.IndexOf("bridge", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        private void UpdateAmbientAudio(bool isMoving)
        {
            if (ambientAudioSource == null || !ambientAudioSource.isPlaying) return;

            float time = Time.time;
            float organicWave = 0f;
            float pitchOffset = 0f;

            if (enableOrganicAtmosphere)
            {
                // Dual sinusoidal LFO mimicking organic wind swells and pressure ebbs
                float wave1 = Mathf.Sin(time * (Mathf.PI * 2f / Mathf.Max(1f, ambientWaveCycle1)));
                float wave2 = Mathf.Cos(time * (Mathf.PI * 2f / Mathf.Max(1f, ambientWaveCycle2)));

                organicWave = (wave1 * 0.65f + wave2 * 0.35f) * ambientWaveDepth;
                pitchOffset = wave1 * ambientPitchDrift;
            }

            // Movement reaction: subtle swell in ambient volume when advancing
            float movementBoost = (isMoving && dynamicMovementAmbiance) ? movementAmbianceBoost : 1.0f;
            float targetVol = ambientMaxVolume * movementBoost * (1.0f + organicWave);

            currentAmbientVolume = Mathf.Lerp(currentAmbientVolume, targetVol, Time.deltaTime * 2.0f);
            ambientAudioSource.volume = currentAmbientVolume;
            ambientAudioSource.pitch = 1.0f + pitchOffset;
        }

        private void UpdateCanopyBirdAudio()
        {
            if (!enableCanopyBirds || canopyBirdClip == null || canopyBirdSource == null) return;

            // Handle active phrase playback stopping
            if (isCanopyPlaying)
            {
                canopyPhraseStopTimer -= Time.deltaTime;
                if (canopyPhraseStopTimer <= 0.5f)
                {
                    canopyBirdSource.volume = Mathf.MoveTowards(canopyBirdSource.volume, 0f, Time.deltaTime * 2f);
                }
                if (canopyPhraseStopTimer <= 0f)
                {
                    canopyBirdSource.Stop();
                    isCanopyPlaying = false;
                }
                return;
            }

            // Countdown to next canopy call event
            canopyBirdTimer -= Time.deltaTime;
            if (canopyBirdTimer <= 0f)
            {
                TriggerCanopyBirdCall();
                canopyBirdTimer = Random.Range(canopyCallInterval.x, canopyCallInterval.y);
            }
        }

        private void TriggerCanopyBirdCall()
        {
            if (canopyBirdClip == null || canopyBirdSource == null) return;

            // Pick random 3D position in tree canopies surrounding player
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(canopyBirdDistance.x, canopyBirdDistance.y);
            float height = Random.Range(canopyBirdHeight.x, canopyBirdHeight.y);

            Vector3 canopyPos = transform.position + new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);
            canopyBirdSource.transform.position = canopyPos;

            // Pick randomized starting section in bird track
            canopyBirdSource.clip = canopyBirdClip;
            float maxStart = Mathf.Max(0f, canopyBirdClip.length - 5.0f);
            canopyBirdSource.time = Random.Range(0f, maxStart);
            canopyBirdSource.pitch = Random.Range(0.94f, 1.06f);
            canopyBirdSource.volume = canopyBirdVolume;
            canopyBirdSource.Play();

            isCanopyPlaying = true;
            canopyPhraseStopTimer = Random.Range(2.8f, 5.2f);
        }

        private void PlayDiscreteFootstepSound(float terrainWeight = 1.0f, float woodWeight = 0.0f)
        {
            if (discreteFootstepSource == null) return;

            // Wood footstep
            if (woodWeight > 0.05f && woodFootstepClips != null && woodFootstepClips.Length > 0)
            {
                AudioClip woodClip = woodFootstepClips[Random.Range(0, woodFootstepClips.Length)];
                if (woodClip != null)
                {
                    discreteFootstepSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
                    discreteFootstepSource.PlayOneShot(woodClip, woodFootstepVolume * woodWeight);
                }
            }

            // Terrain footstep
            if (terrainWeight > 0.05f && footstepClips != null && footstepClips.Length > 0)
            {
                AudioClip terrainClip = footstepClips[Random.Range(0, footstepClips.Length)];
                if (terrainClip != null)
                {
                    discreteFootstepSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
                    discreteFootstepSource.PlayOneShot(terrainClip, footstepVolume * terrainWeight);
                }
            }
        }
    }
}
