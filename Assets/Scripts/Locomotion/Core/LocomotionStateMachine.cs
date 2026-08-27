using System;
using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Evaluates and maintains the current locomotion state for the player.
    /// Emits events on state transitions for audio, comfort, and interaction systems.
    /// </summary>
    [AddComponentMenu("Locomotion/Locomotion State Machine")]
    public class LocomotionStateMachine : MonoBehaviour
    {
        public LocomotionState CurrentState { get; private set; } = LocomotionState.Grounded;

        public event Action<LocomotionState, LocomotionState> OnStateChanged;

        private ContinuousLocomotion continuousLocomotion;
        private GroundDetector groundDetector;
        private bool isTeleporting;

        private void Awake()
        {
            continuousLocomotion = GetComponent<ContinuousLocomotion>();
            groundDetector = GetComponent<GroundDetector>();
        }

        private void Update()
        {
            LocomotionState newState = EvaluateState();
            if (newState != CurrentState)
            {
                LocomotionState previousState = CurrentState;
                CurrentState = newState;
                OnStateChanged?.Invoke(previousState, newState);
            }
        }

        private LocomotionState EvaluateState()
        {
            if (isTeleporting) return LocomotionState.Teleporting;

            bool grounded = groundDetector != null ? groundDetector.IsGrounded : true;

            if (!grounded)
            {
                return LocomotionState.Airborne;
            }

            if (groundDetector != null && groundDetector.SlopeAngle > 30.0f)
            {
                return LocomotionState.SteepSlope;
            }

            if (continuousLocomotion != null && continuousLocomotion.IsMoving)
            {
                return LocomotionState.Walking;
            }

            return LocomotionState.Grounded;
        }

        public void SetTeleporting(bool teleporting)
        {
            isTeleporting = teleporting;
        }
    }
}
