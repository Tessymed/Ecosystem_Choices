using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Provides lightweight controller haptic feedback for locomotion events (teleport confirmation, surface footsteps).
    /// </summary>
    [AddComponentMenu("Locomotion/Locomotion Haptics")]
    public class LocomotionHaptics : MonoBehaviour
    {
        [Header("Haptic Impulses")]
        [SerializeField] private float teleportFrequency = 0.8f;
        [SerializeField] private float teleportAmplitude = 0.5f;
        [SerializeField] private float teleportDuration = 0.15f;

        [SerializeField] private float footstepAmplitude = 0.1f;
        [SerializeField] private float footstepDuration = 0.05f;

        /// <summary>
        /// Triggers a short haptic pulse on specified controller.
        /// </summary>
        public void TriggerTeleportHaptic(bool isLeftHand = false)
        {
#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            OVRInput.Controller controller = isLeftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
            OVRInput.SetControllerVibration(teleportFrequency, teleportAmplitude, controller);
            Invoke(nameof(StopVibration), teleportDuration);
#endif
        }

        public void TriggerFootstepHaptic()
        {
#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            OVRInput.SetControllerVibration(0.2f, footstepAmplitude, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0.2f, footstepAmplitude, OVRInput.Controller.RTouch);
            Invoke(nameof(StopVibration), footstepDuration);
#endif
        }

        private void StopVibration()
        {
#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
#endif
        }
    }
}
