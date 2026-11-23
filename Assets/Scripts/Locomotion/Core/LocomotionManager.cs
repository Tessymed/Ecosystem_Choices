using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Lightweight coordinator script attached to the XR Player root.
    /// Connects and exposes references to all locomotion components.
    /// </summary>
    [AddComponentMenu("Locomotion/Locomotion Manager")]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(ContinuousLocomotion))]
    [RequireComponent(typeof(RotationController))]
    [RequireComponent(typeof(GroundDetector))]
    [RequireComponent(typeof(LocomotionStateMachine))]
    public class LocomotionManager : MonoBehaviour
    {
        public CharacterController CharacterController { get; private set; }
        public ContinuousLocomotion ContinuousLocomotion { get; private set; }
        public RotationController RotationController { get; private set; }
        public GroundDetector GroundDetector { get; private set; }
        public LocomotionStateMachine StateMachine { get; private set; }
        public PathMovementConstraint PathConstraint { get; private set; }

        public ComfortVignette Vignette { get; set; }
        public TeleportController TeleportController { get; set; }

        private void Awake()
        {
            CharacterController = GetComponent<CharacterController>();
            ContinuousLocomotion = GetComponent<ContinuousLocomotion>();
            RotationController = GetComponent<RotationController>();
            GroundDetector = GetComponent<GroundDetector>();
            StateMachine = GetComponent<LocomotionStateMachine>();
            PathConstraint = GetComponent<PathMovementConstraint>();

            if (GetComponent<LocomotionModifier>() == null)
            {
                gameObject.AddComponent<LocomotionModifier>();
            }

            Vignette = GetComponent<ComfortVignette>() ?? GetComponentInChildren<ComfortVignette>();
            TeleportController = GetComponent<TeleportController>() ?? GetComponentInChildren<TeleportController>();

            SynchronizeActiveTerrain();
        }

        private void Start()
        {
            InitializeXRTrackingAndSplashScreen();

            SynchronizeActiveTerrain();

            if (ContinuousLocomotion != null)
            {
                ContinuousLocomotion.SnapToGround();
            }
        }

        /// <summary>
        /// Ensures that the active Terrain's collider is fully synchronized with its visual TerrainData,
        /// clears restrictive layer masks, and ensures PathMovementConstraint references the active terrain.
        /// </summary>
        public void SynchronizeActiveTerrain()
        {
            Terrain activeTerrain = Terrain.activeTerrain;
            if (activeTerrain != null)
            {
                TerrainCollider terrainCollider = activeTerrain.GetComponent<TerrainCollider>();
                if (terrainCollider != null)
                {
                    if (terrainCollider.terrainData != activeTerrain.terrainData)
                    {
                        Debug.Log($"[LocomotionManager] Synchronized TerrainCollider terrainData to active Terrain '{activeTerrain.name}'.");
                        terrainCollider.terrainData = activeTerrain.terrainData;
                    }
                    if (terrainCollider.includeLayers != 0)
                    {
                        terrainCollider.includeLayers = 0;
                    }
                }

                if (PathConstraint != null)
                {
                    if (PathConstraint.TargetTerrain == null || PathConstraint.TargetTerrain != activeTerrain)
                    {
                        PathConstraint.TargetTerrain = activeTerrain;
                        PathConstraint.InitializeTerrain();
                        PathConstraint.CacheTerrainAlphamaps();
                    }
                }
            }

            SynchronizeBridgeSurfaces();
        }

        private void SynchronizeBridgeSurfaces()
        {
            var bridgeGO = GameObject.Find("PT_Wooden_Bridge_02");
            if (bridgeGO != null)
            {
                var surface = bridgeGO.GetComponent<EcosystemChoices.Environment.WoodenBridgeSurface>();
                if (surface == null)
                {
                    surface = bridgeGO.AddComponent<EcosystemChoices.Environment.WoodenBridgeSurface>();
                }
                surface.CacheAndInitialize();
            }
        }

        private void InitializeXRTrackingAndSplashScreen()
        {
            // 1. Set tracking origin mode to FloorLevel via UnityEngine.SubsystemManager
            var inputSubsystems = new System.Collections.Generic.List<UnityEngine.XR.XRInputSubsystem>();
            UnityEngine.SubsystemManager.GetSubsystems(inputSubsystems);
            foreach (var subsystem in inputSubsystems)
            {
                subsystem.TrySetTrackingOriginMode(UnityEngine.XR.TrackingOriginModeFlags.Floor);
            }

            // 2. Configure Meta Quest / Oculus floor tracking origin
#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            try
            {
                if (OVRManager.instance != null)
                {
                    OVRManager.instance.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LocomotionManager] OVR setup: {ex.Message}");
            }
#endif
        }
    }
}
