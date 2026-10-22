using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Self-contained Teleportation system providing parabolic trajectory calculation,
    /// surface slope validation, target landing marker rendering, and instant teleportation.
    /// </summary>
    [AddComponentMenu("Locomotion/Teleport Controller")]
    public class TeleportController : MonoBehaviour
    {
        [Header("Teleport Input Settings")]
        [Tooltip("Enable teleportation functionality.")]
        [SerializeField] private bool teleportEnabled = true;

        [Tooltip("Max slope angle allowed for teleportation landing (default 45°).")]
        [SerializeField] private float maxValidSlopeAngle = 45.0f;

        [Header("Trajectory Physics")]
        [SerializeField] private float initialVelocity = 10.0f;
        [SerializeField] private int trajectoryPointCount = 30;
        [SerializeField] private float timeStep = 0.05f;
        [SerializeField] private LayerMask teleportLayers = ~0;

        [Header("Visual Feedback")]
        [SerializeField] private LineRenderer lineRenderer;
        [SerializeField] private GameObject landingMarker;
        [SerializeField] private Material validMaterial;
        [SerializeField] private Material invalidMaterial;

        [Header("References")]
        [SerializeField] private Transform handTransform;
        [SerializeField] private ContinuousLocomotion continuousLocomotion;
        [SerializeField] private LocomotionStateMachine stateMachine;
        [SerializeField] private PathMovementConstraint pathConstraint;

#if ENABLE_INPUT_SYSTEM
        [Header("Input System Fallback")]
        [SerializeField] private InputActionProperty teleportAction;
#endif

        private bool isAiming;
        private bool isValidLandingPoint;
        private Vector3 landingPoint;
        private Renderer markerRenderer;

        public bool Enabled { get => teleportEnabled; set => teleportEnabled = value; }

        private void Awake()
        {
            if (continuousLocomotion == null) continuousLocomotion = GetComponentInParent<ContinuousLocomotion>();
            if (stateMachine == null) stateMachine = GetComponentInParent<LocomotionStateMachine>();
            if (pathConstraint == null) pathConstraint = GetComponentInParent<PathMovementConstraint>();

            if (handTransform == null)
            {
                GameObject hand = GameObject.Find("RightHandAnchor");
                if (hand == null) hand = GameObject.Find("LeftHandAnchor");
                if (hand != null) handTransform = hand.transform;
            }

            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.startWidth = 0.02f;
                lineRenderer.endWidth = 0.02f;
                lineRenderer.positionCount = 0;
            }

            if (landingMarker != null)
            {
                markerRenderer = landingMarker.GetComponentInChildren<Renderer>();
                landingMarker.SetActive(false);
            }
        }

        private void Update()
        {
            if (!teleportEnabled) return;

            bool aimInput = ReadTeleportInput();

            if (aimInput)
            {
                if (!isAiming)
                {
                    isAiming = true;
                    if (stateMachine != null) stateMachine.SetTeleporting(true);
                }

                UpdateTrajectory();
            }
            else
            {
                if (isAiming)
                {
                    if (isValidLandingPoint)
                    {
                        ExecuteTeleport();
                    }

                    isAiming = false;
                    if (stateMachine != null) stateMachine.SetTeleporting(false);
                    ClearVisuals();
                }
            }
        }

        private bool ReadTeleportInput()
        {
            bool active = false;

#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            // Active when left or right hand index trigger held halfway
            active = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch) ||
                     OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
#endif

#if ENABLE_INPUT_SYSTEM
            if (!active && teleportAction.action != null)
            {
                active = teleportAction.action.ReadValue<float>() > 0.5f;
            }

            if (!active && Keyboard.current != null)
            {
                active = Keyboard.current.tKey.isPressed;
            }
#endif

            if (!active && Application.isEditor)
            {
                try
                {
                    active = Input.GetKey(KeyCode.T);
                }
                catch (System.InvalidOperationException) { }
            }

            return active;
        }

        private void UpdateTrajectory()
        {
            Transform origin = handTransform != null ? handTransform : transform;
            Vector3 startPos = origin.position;
            Vector3 startVel = origin.forward * initialVelocity;

            List<Vector3> points = new List<Vector3> { startPos };
            isValidLandingPoint = false;

            Vector3 currentPos = startPos;
            Vector3 currentVel = startVel;

            for (int i = 0; i < trajectoryPointCount; i++)
            {
                Vector3 nextPos = currentPos + currentVel * timeStep + 0.5f * Physics.gravity * timeStep * timeStep;
                currentVel += Physics.gravity * timeStep;

                if (Physics.Linecast(currentPos, nextPos, out RaycastHit hit, teleportLayers, QueryTriggerInteraction.Ignore))
                {
                    points.Add(hit.point);
                    landingPoint = hit.point;

                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    bool validSlope = slope <= maxValidSlopeAngle;
                    bool validPath = pathConstraint == null || pathConstraint.IsPositionValid(hit.point, out _);

                    isValidLandingPoint = validSlope && validPath;

                    UpdateMarker(hit.point, hit.normal, isValidLandingPoint);
                    break;
                }

                points.Add(nextPos);
                currentPos = nextPos;
            }

            if (!isValidLandingPoint && landingMarker != null)
            {
                landingMarker.SetActive(false);
            }

            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPositions(points.ToArray());
        }

        private void UpdateMarker(Vector3 position, Vector3 normal, bool valid)
        {
            if (landingMarker == null) return;

            landingMarker.SetActive(true);
            landingMarker.transform.position = position + normal * 0.01f;
            landingMarker.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal);

            if (markerRenderer != null)
            {
                if (valid && validMaterial != null) markerRenderer.material = validMaterial;
                else if (!valid && invalidMaterial != null) markerRenderer.material = invalidMaterial;
            }
        }

        private void ExecuteTeleport()
        {
            if (continuousLocomotion != null)
            {
                continuousLocomotion.TeleportTo(landingPoint);
            }
            else
            {
                transform.root.position = landingPoint;
            }
        }

        private void ClearVisuals()
        {
            if (lineRenderer != null) lineRenderer.positionCount = 0;
            if (landingMarker != null) landingMarker.SetActive(false);
        }
    }
}
