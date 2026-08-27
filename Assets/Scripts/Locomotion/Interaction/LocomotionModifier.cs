using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Manages external speed modifiers applied to ContinuousLocomotion.
    /// Smoothly interpolates overall speed scale based on active environmental and POI modifiers.
    /// </summary>
    [AddComponentMenu("Locomotion/Locomotion Modifier")]
    [RequireComponent(typeof(ContinuousLocomotion))]
    public class LocomotionModifier : MonoBehaviour
    {
        [Tooltip("Smooth transition speed for modifier changes (0.5 - 1.5s total duration).")]
        [SerializeField] private float transitionSpeed = 2.0f;

        private ContinuousLocomotion continuousLocomotion;
        private float currentModifier = 1.0f;
        private float targetModifier = 1.0f;

        private void Awake()
        {
            continuousLocomotion = GetComponent<ContinuousLocomotion>();
        }

        private void Update()
        {
            if (Mathf.Abs(currentModifier - targetModifier) > 0.001f)
            {
                currentModifier = Mathf.MoveTowards(currentModifier, targetModifier, transitionSpeed * Time.deltaTime);
                if (continuousLocomotion != null)
                {
                    continuousLocomotion.ExternalSpeedModifier = currentModifier;
                }
            }
        }

        /// <summary>
        /// Sets a target speed multiplier (e.g. 0.44f for POI observation speed 0.8 m/s relative to 1.8 m/s base speed).
        /// </summary>
        public void SetTargetModifier(float modifier)
        {
            targetModifier = Mathf.Clamp(modifier, 0.0f, 2.0f);
        }

        public void ResetModifier()
        {
            targetModifier = 1.0f;
        }
    }
}
