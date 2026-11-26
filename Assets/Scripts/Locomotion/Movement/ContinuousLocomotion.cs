using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EcosystemChoices.Locomotion
{
    public enum MovementReferenceSource
    {
        HMD,
        Controller,
        PlayerBody
    }

    /// <summary>
    /// Handles continuous artificial translation using CharacterController.Move().
    /// Implements radial deadzone, directional reference selection, smooth acceleration/deceleration,
    /// gravity, and slope speed adjustments.
    /// </summary>
    [AddComponentMenu("Locomotion/Continuous Locomotion")]
    [RequireComponent(typeof(CharacterController))]
    public class ContinuousLocomotion : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("Base exploration speed in meters per second.")]
        [SerializeField] private float baseSpeed = 1.8f;

        [Tooltip("Acceleration rate in m/s². Natural range 3-5 m/s².")]
        [SerializeField] private float acceleration = 4.0f;

        [Tooltip("Deceleration rate in m/s². Slightly faster than acceleration (4-6 m/s²) to prevent sliding.")]
        [SerializeField] private float deceleration = 5.0f;

        [Tooltip("Radial stick deadzone threshold (0.10 - 0.15).")]
        [SerializeField] private float deadzone = 0.12f;

        [Tooltip("Reference transform for movement direction.")]
        [SerializeField] private MovementReferenceSource referenceSource = MovementReferenceSource.HMD;

        [Header("Transform References")]
        [SerializeField] private Transform hmdTransform;
        [SerializeField] private Transform controllerTransform;

        [Header("Camera & Height Settings")]
        [Tooltip("Default eye height in meters used when head tracking is uninitialized or in Editor.")]
        [SerializeField] private float defaultEyeHeight = 1.75f;
        [SerializeField] private float minCapsuleHeight = 1.0f;
        [SerializeField] private float maxCapsuleHeight = 2.2f;

        [Header("Gravity & Slope Settings")]
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float groundedGroundedGravity = -0.5f;
        [SerializeField] private float slowSlopeAngleThreshold = 30.0f;
        [SerializeField] private float maxSlopeLimit = 45.0f;

#if ENABLE_INPUT_SYSTEM
        [Header("Input System Fallback")]
        [SerializeField] private InputActionProperty moveAction;
#endif

        // Public readouts for State Machine / Audio / Comfort
        public Vector3 CurrentVelocity => currentHorizontalVelocity + Vector3.up * verticalVelocity;
        public float CurrentSpeed => currentHorizontalVelocity.magnitude;
        public Vector2 ProcessedInput => lastProcessedInput;
        public bool IsMoving => CurrentSpeed > 0.05f;
        public bool IsAccelerating => isAccelerating;
        public float ExternalSpeedModifier { get; set; } = 1.0f;

        [Header("Path Constraint Reference")]
        [SerializeField] private PathMovementConstraint pathConstraint;

        private CharacterController characterController;
        private GroundDetector groundDetector;
        private Vector3 currentHorizontalVelocity;
        private float verticalVelocity;
        private Vector2 lastProcessedInput;
        private bool isAccelerating;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            groundDetector = GetComponent<GroundDetector>();
            if (pathConstraint == null) pathConstraint = GetComponent<PathMovementConstraint>();

            AutoBindOVRTransforms();

            if (characterController != null)
            {
                characterController.stepOffset = 0.08f;
                characterController.slopeLimit = maxSlopeLimit;
            }
        }

        private void AutoBindOVRTransforms()
        {
            if (hmdTransform == null)
            {
                Transform t = transform.Find("TrackingSpace/CenterEyeAnchor");
                if (t != null) hmdTransform = t;
                else
                {
                    GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                    if (centerEye != null) hmdTransform = centerEye.transform;
                    else if (Camera.main != null) hmdTransform = Camera.main.transform;
                }
            }

            if (controllerTransform == null)
            {
                Transform t = transform.Find("TrackingSpace/LeftHandAnchor");
                if (t != null) controllerTransform = t;
                else
                {
                    GameObject leftHand = GameObject.Find("LeftHandAnchor");
                    if (leftHand != null) controllerTransform = leftHand.transform;
                }
            }
        }

        private void Update()
        {
            SyncCharacterControllerWithHMD();

            Vector2 rawInput = ReadInput();
            lastProcessedInput = ApplyRadialDeadzone(rawInput, deadzone);

            Vector3 moveDirection = CalculateMoveDirection(lastProcessedInput);
            float targetSpeed = CalculateTargetSpeed(moveDirection);

            Vector3 targetHorizontalVelocity = moveDirection * targetSpeed;

            UpdateHorizontalVelocity(targetHorizontalVelocity);
            UpdateVerticalVelocity();

            Vector3 currentPos = transform.position;
            Vector3 proposedStep = (currentHorizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;

            if (pathConstraint != null && pathConstraint.EnableConstraint)
            {
                Vector3 targetPos = currentPos + proposedStep;
                Vector3 constrainedTargetPos = pathConstraint.ConstrainPosition(currentPos, targetPos);
                Vector3 constrainedStep = constrainedTargetPos - currentPos;

                characterController.Move(constrainedStep);
            }
            else
            {
                characterController.Move(proposedStep);
            }
        }

        private void SyncCharacterControllerWithHMD()
        {
            if (characterController == null) return;

            float headHeight = defaultEyeHeight;
            if (hmdTransform != null)
            {
                headHeight = hmdTransform.localPosition.y;
                // If head tracking returns zero or uninitialized height (e.g. in Editor or untracked), fallback to defaultEyeHeight
                if (headHeight < 0.2f)
                {
                    headHeight = defaultEyeHeight;
                }
            }

            float clampedHeight = Mathf.Clamp(headHeight, minCapsuleHeight, maxCapsuleHeight);
            
            // Deadband filter: only update capsule geometry if height changed meaningfully (> 6 cm)
            // Rebuilding the capsule every frame on sub-centimeter head tremors causes violent PhysX vertical oscillation!
            if (Mathf.Abs(characterController.height - clampedHeight) > 0.06f)
            {
                characterController.height = clampedHeight;
                characterController.center = new Vector3(0f, clampedHeight * 0.5f, 0f);
            }
        }

        private Vector2 ReadInput()
        {
            Vector2 input = Vector2.zero;

            // Primary input from OVR SDK if present
#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
#endif

#if ENABLE_INPUT_SYSTEM
            if (input == Vector2.zero && moveAction.action != null)
            {
                input = moveAction.action.ReadValue<Vector2>();
            }

            if (input == Vector2.zero && Keyboard.current != null)
            {
                float h = 0f, v = 0f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
                input = new Vector2(h, v);
            }
#endif

            // Safe legacy input fallback (wrapped in try-catch to avoid exception when legacy input is disabled in Player Settings)
            if (input == Vector2.zero && Application.isEditor)
            {
                try
                {
                    float h = Input.GetAxisRaw("Horizontal");
                    float v = Input.GetAxisRaw("Vertical");
                    input = new Vector2(h, v);
                }
                catch (System.InvalidOperationException) { }
            }

            return Vector2.ClampMagnitude(input, 1.0f);
        }

        private Vector2 ApplyRadialDeadzone(Vector2 input, float deadzoneThreshold)
        {
            float magnitude = input.magnitude;
            if (magnitude < deadzoneThreshold) return Vector2.zero;

            // Remap (deadzone..1) to (0..1) linearly for smooth joystick control
            float remappedMagnitude = (magnitude - deadzoneThreshold) / (1.0f - deadzoneThreshold);
            return input.normalized * remappedMagnitude;
        }

        private Vector3 CalculateMoveDirection(Vector2 input)
        {
            if (input == Vector2.zero) return Vector3.zero;

            Transform refTransform = GetReferenceTransform();
            Vector3 forward = refTransform.forward;
            Vector3 right = refTransform.right;

            // Flatten onto XZ plane
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 desiredDir = (forward * input.y + right * input.x);
            return desiredDir.magnitude > 1.0f ? desiredDir.normalized : desiredDir;
        }

        private Transform GetReferenceTransform()
        {
            switch (referenceSource)
            {
                case MovementReferenceSource.Controller:
                    if (controllerTransform != null) return controllerTransform;
                    break;
                case MovementReferenceSource.PlayerBody:
                    return transform;
                case MovementReferenceSource.HMD:
                default:
                    if (hmdTransform != null) return hmdTransform;
                    if (Camera.main != null) return Camera.main.transform;
                    break;
            }
            return transform;
        }

        private float CalculateTargetSpeed(Vector3 moveDirection)
        {
            if (moveDirection == Vector3.zero) return 0f;

            float speed = baseSpeed * ExternalSpeedModifier;

            // Apply slope slowdown if GroundDetector detects slope > 30°
            if (groundDetector != null && groundDetector.IsGrounded)
            {
                float slope = groundDetector.SlopeAngle;
                if (slope > slowSlopeAngleThreshold)
                {
                    float slopeFactor = Mathf.Clamp01((slope - slowSlopeAngleThreshold) / (maxSlopeLimit - slowSlopeAngleThreshold));
                    // Smoothly reduce speed between 30° and 45° (1.8 m/s -> 0.8 m/s)
                    speed = Mathf.Lerp(speed, speed * 0.44f, slopeFactor);
                }
            }

            return speed;
        }

        private void UpdateHorizontalVelocity(Vector3 targetVelocity)
        {
            float targetSpeed = targetVelocity.magnitude;
            float currentSpeed = currentHorizontalVelocity.magnitude;

            isAccelerating = targetSpeed > currentSpeed + 0.01f;
            float accelRate = isAccelerating ? acceleration : deceleration;

            currentHorizontalVelocity = Vector3.MoveTowards(currentHorizontalVelocity, targetVelocity, accelRate * Time.deltaTime);
        }

        private void UpdateVerticalVelocity()
        {
            bool grounded = characterController.isGrounded || (groundDetector != null && groundDetector.IsGrounded && groundDetector.GroundDistance < 0.1f);

            if (grounded)
            {
                if (verticalVelocity < 0f)
                {
                    verticalVelocity = groundedGroundedGravity;
                }
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
        }

        public void TeleportTo(Vector3 destination)
        {
            characterController.enabled = false;
            transform.position = destination;
            currentHorizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            characterController.enabled = true;
        }

        /// <summary>
        /// Snaps player transform vertically to the ground surface directly beneath it.
        /// Prioritizes terrain heightmap to avoid hitting sky/foliage colliders.
        /// </summary>
        public void SnapToGround()
        {
            if (groundDetector != null && groundDetector.TryGetGroundSurfacePoint(transform.position, out Vector3 surfacePoint))
            {
                TeleportTo(surfacePoint);
            }
            else if (Terrain.activeTerrain != null)
            {
                Vector3 pos = transform.position;
                float terrainY = Terrain.activeTerrain.SampleHeight(pos) + Terrain.activeTerrain.transform.position.y;
                pos.y = terrainY;
                TeleportTo(pos);
            }
            else
            {
                Vector3 rayStart = transform.position + Vector3.up * 2f;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                {
                    TeleportTo(hit.point);
                }
            }
        }
    }
}
