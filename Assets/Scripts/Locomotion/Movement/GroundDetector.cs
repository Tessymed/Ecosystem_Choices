using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Information provider that queries the environment beneath the CharacterController.
    /// Provides grounding status, ground distance, surface normal, slope angle, and raycast hit data.
    /// </summary>
    [AddComponentMenu("Locomotion/Ground Detector")]
    [RequireComponent(typeof(CharacterController))]
    public class GroundDetector : MonoBehaviour
    {
        [Header("Ground Detection Settings")]
        [Tooltip("Layer mask for surfaces considered walkable ground.")]
        [SerializeField] private LayerMask groundLayers = ~0;

        [Tooltip("Extra distance below capsule bottom to probe for ground.")]
        [SerializeField] private float raycastDistance = 0.5f;

        [Tooltip("Radius multiplier relative to CharacterController radius for sphere casting.")]
        [SerializeField] private float sphereCastRadiusScale = 0.9f;

        // Public readout properties
        public bool IsGrounded { get; private set; }
        public float GroundDistance { get; private set; }
        public Vector3 GroundNormal { get; private set; } = Vector3.up;
        public float SlopeAngle { get; private set; }
        public RaycastHit SurfaceHit => currentHit;

        private CharacterController characterController;
        private RaycastHit currentHit;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            DetectGround();
        }

        private void DetectGround()
        {
            if (characterController == null) return;

            // Compute bottom of capsule in world space
            Vector3 capsuleBottom = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f - characterController.radius);
            float radius = characterController.radius * sphereCastRadiusScale;
            float maxCastDistance = raycastDistance + characterController.skinWidth;

            // Perform SphereCast downwards
            if (Physics.SphereCast(capsuleBottom, radius, Vector3.down, out currentHit, maxCastDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                IsGrounded = true;
                GroundDistance = Mathf.Max(0f, currentHit.distance - characterController.skinWidth);
                GroundNormal = currentHit.normal;
                SlopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
            }
            else
            {
                // Fallback Raycast for high precision directly below center
                if (Physics.Raycast(capsuleBottom, Vector3.down, out currentHit, maxCastDistance, groundLayers, QueryTriggerInteraction.Ignore))
                {
                    IsGrounded = true;
                    GroundDistance = Mathf.Max(0f, currentHit.distance - characterController.skinWidth);
                    GroundNormal = currentHit.normal;
                    SlopeAngle = Vector3.Angle(GroundNormal, Vector3.up);
                }
                else
                {
                    IsGrounded = characterController.isGrounded;
                    GroundDistance = characterController.isGrounded ? 0f : float.MaxValue;
                    GroundNormal = Vector3.up;
                    SlopeAngle = 0f;
                }
            }
        }

        /// <summary>
        /// Attempts to find the exact ground surface point beneath a specified position.
        /// </summary>
        public bool TryGetGroundSurfacePoint(Vector3 searchOrigin, out Vector3 surfacePoint)
        {
            surfacePoint = searchOrigin;
            Vector3 rayStart = searchOrigin + Vector3.up * 500f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 1000f, groundLayers, QueryTriggerInteraction.Ignore))
            {
                surfacePoint = hit.point;
                return true;
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            if (characterController == null) return;

            Vector3 capsuleBottom = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f - characterController.radius);
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(capsuleBottom, characterController.radius * sphereCastRadiusScale);
            Gizmos.DrawRay(capsuleBottom, Vector3.down * (raycastDistance + characterController.skinWidth));

            if (IsGrounded)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(currentHit.point, GroundNormal * 0.5f);
            }
        }
    }
}
