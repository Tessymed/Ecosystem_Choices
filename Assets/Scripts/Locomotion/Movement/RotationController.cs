using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EcosystemChoices.Locomotion
{
    public enum RotationMode
    {
        Snap,
        Smooth,
        PhysicalOnly
    }

    /// <summary>
    /// Handles artificial player rotation (Snap turn, Smooth turn, Physical-only).
    /// Rotates the player transform around the HMD center to prevent spatial displacement during rotation.
    /// </summary>
    [AddComponentMenu("Locomotion/Rotation Controller")]
    public class RotationController : MonoBehaviour
    {
        [Header("Rotation Mode")]
        [SerializeField] private RotationMode mode = RotationMode.Snap;

        [Header("Snap Turn Settings")]
        [Tooltip("Snap rotation increment in degrees (15°, 30°, 45°, 60°).")]
        [SerializeField] private float snapAngle = 30.0f;

        [Tooltip("Stick threshold required to trigger snap turn.")]
        [SerializeField] private float snapThreshold = 0.5f;

        [Header("Smooth Turn Settings")]
        [Tooltip("Smooth rotation speed in degrees per second (60°–90°/s).")]
        [SerializeField] private float smoothTurnSpeed = 75.0f;

        [Header("Transform References")]
        [SerializeField] private Transform hmdTransform;

#if ENABLE_INPUT_SYSTEM
        [Header("Input System Fallback")]
        [SerializeField] private InputActionProperty turnAction;
#endif

        private bool hasSnapped;

        private void Awake()
        {
            if (hmdTransform == null)
            {
                GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) hmdTransform = centerEye.transform;
                else if (Camera.main != null) hmdTransform = Camera.main.transform;
            }
        }

        public RotationMode Mode { get => mode; set => mode = value; }
        public float SnapAngle { get => snapAngle; set => snapAngle = value; }
        public float SmoothTurnSpeed { get => smoothTurnSpeed; set => smoothTurnSpeed = value; }

        private void Update()
        {
            if (mode == RotationMode.PhysicalOnly) return;

            Vector2 input = ReadTurnInput();

            if (mode == RotationMode.Snap)
            {
                HandleSnapTurn(input.x);
            }
            else if (mode == RotationMode.Smooth)
            {
                HandleSmoothTurn(input.x);
            }
        }

        private Vector2 ReadTurnInput()
        {
            Vector2 input = Vector2.zero;

#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            input = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick);
#endif

#if ENABLE_INPUT_SYSTEM
            if (input == Vector2.zero && turnAction.action != null)
            {
                input = turnAction.action.ReadValue<Vector2>();
            }

            if (input == Vector2.zero && Keyboard.current != null)
            {
                if (Keyboard.current.qKey.isPressed) input.x = -1.0f;
                if (Keyboard.current.eKey.isPressed) input.x = 1.0f;
            }
#endif

            if (input == Vector2.zero && Application.isEditor)
            {
                try
                {
                    if (Input.GetKey(KeyCode.Q)) input.x = -1.0f;
                    if (Input.GetKey(KeyCode.E)) input.x = 1.0f;
                }
                catch (System.InvalidOperationException) { }
            }

            return input;
        }

        private void HandleSnapTurn(float turnInput)
        {
            if (Mathf.Abs(turnInput) >= snapThreshold)
            {
                if (!hasSnapped)
                {
                    float angle = turnInput > 0 ? snapAngle : -snapAngle;
                    RotateAroundHMD(angle);
                    hasSnapped = true;
                }
            }
            else if (Mathf.Abs(turnInput) < 0.2f)
            {
                hasSnapped = false;
            }
        }

        private void HandleSmoothTurn(float turnInput)
        {
            if (Mathf.Abs(turnInput) > 0.1f)
            {
                float angle = turnInput * smoothTurnSpeed * Time.deltaTime;
                RotateAroundHMD(angle);
            }
        }

        /// <summary>
        /// Rotates the root player transform around the current HMD position so turning is centered on the player's head.
        /// </summary>
        public void RotateAroundHMD(float angleDegrees)
        {
            Vector3 pivotPoint = GetHMDPivotPoint();

            // Rotate player position around pivot point
            transform.RotateAround(pivotPoint, Vector3.up, angleDegrees);
        }

        private Vector3 GetHMDPivotPoint()
        {
            if (hmdTransform != null) return hmdTransform.position;
            if (Camera.main != null) return Camera.main.transform.position;
            return transform.position;
        }
    }
}
