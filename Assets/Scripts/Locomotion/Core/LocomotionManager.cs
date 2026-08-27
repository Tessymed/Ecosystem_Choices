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

        public ComfortVignette Vignette { get; set; }
        public TeleportController TeleportController { get; set; }

        private void Awake()
        {
            CharacterController = GetComponent<CharacterController>();
            ContinuousLocomotion = GetComponent<ContinuousLocomotion>();
            RotationController = GetComponent<RotationController>();
            GroundDetector = GetComponent<GroundDetector>();
            StateMachine = GetComponent<LocomotionStateMachine>();

            Vignette = GetComponentInChildren<ComfortVignette>();
            TeleportController = GetComponentInChildren<TeleportController>();
        }

        private void Start()
        {
            InitializeXRTrackingAndSplashScreen();

            if (ContinuousLocomotion != null)
            {
                ContinuousLocomotion.SnapToGround();
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
                Debug.LogWarning($"[LocomotionManager] OVR splash dismissal: {ex.Message}");
            }
#endif
        }
    }
}
