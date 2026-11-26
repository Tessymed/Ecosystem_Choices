using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Debug overlay & scene Gizmos renderer displaying real-time locomotion telemetry:
    /// speed, input vector, grounding status, slope angle, locomotion state, and direction vectors.
    /// </summary>
    [AddComponentMenu("Locomotion/Locomotion Debug Overlay")]
    public class LocomotionDebugOverlay : MonoBehaviour
    {
        [Header("Debug Display Settings")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private bool drawGizmos = true;

        [Header("References")]
        [SerializeField] private ContinuousLocomotion continuousLocomotion;
        [SerializeField] private GroundDetector groundDetector;
        [SerializeField] private LocomotionStateMachine stateMachine;
        [SerializeField] private MovementAudio movementAudio;
        [SerializeField] private Transform hmdTransform;

        private void Awake()
        {
            if (continuousLocomotion == null) continuousLocomotion = GetComponentInParent<ContinuousLocomotion>();
            if (groundDetector == null) groundDetector = GetComponentInParent<GroundDetector>();
            if (stateMachine == null) stateMachine = GetComponentInParent<LocomotionStateMachine>();
            if (movementAudio == null) movementAudio = GetComponentInParent<MovementAudio>();
        }

        private void OnGUI()
        {
#if UNITY_EDITOR
            if (UnityEngine.XR.XRSettings.isDeviceActive) return;
            if (!showOverlay) return;

            float speed = continuousLocomotion != null ? continuousLocomotion.CurrentSpeed : 0f;
            Vector2 input = continuousLocomotion != null ? continuousLocomotion.ProcessedInput : Vector2.zero;
            bool grounded = groundDetector != null ? groundDetector.IsGrounded : false;
            float slope = groundDetector != null ? groundDetector.SlopeAngle : 0f;
            string stateStr = stateMachine != null ? stateMachine.CurrentState.ToString() : "N/A";
            float modifier = continuousLocomotion != null ? continuousLocomotion.ExternalSpeedModifier : 1.0f;
            string surfaceStr = movementAudio != null ? (movementAudio.CurrentSurfaceBlend > 0.05f ? $"Bridge Wood ({(movementAudio.CurrentSurfaceBlend * 100f):F0}%)" : "Terrain Path") : "Terrain";

            GUI.Box(new Rect(10, 10, 240, 200), "LOCOMOTION DEBUG");

            GUILayout.BeginArea(new Rect(20, 35, 220, 170));
            GUILayout.Label($"Speed: {speed:F2} m/s");
            GUILayout.Label($"Input Magnitude: {input.magnitude:F2}");
            GUILayout.Label($"Grounded: {(grounded ? "YES" : "NO")}");
            GUILayout.Label($"Slope Angle: {slope:F1}°");
            GUILayout.Label($"State: {stateStr}");
            GUILayout.Label($"Surface: {surfaceStr}");
            GUILayout.Label($"POI Speed Scale: {modifier:F2}");
            GUILayout.EndArea();
#endif
        }

        private void OnDrawGizmos()
        {
            if (!drawGizmos) return;

            Vector3 pos = transform.position;

            // Draw HMD direction in cyan
            Transform hmd = hmdTransform != null ? hmdTransform : (Camera.main != null ? Camera.main.transform : transform);
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(pos + Vector3.up * 1.5f, hmd.forward * 1.0f);

            // Draw movement velocity vector in yellow
            if (continuousLocomotion != null && continuousLocomotion.CurrentSpeed > 0.05f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(pos + Vector3.up * 0.2f, continuousLocomotion.CurrentVelocity);
            }

            // Draw ground normal in green
            if (groundDetector != null && groundDetector.IsGrounded)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(groundDetector.SurfaceHit.point, groundDetector.GroundNormal * 0.8f);
            }
        }
    }
}
