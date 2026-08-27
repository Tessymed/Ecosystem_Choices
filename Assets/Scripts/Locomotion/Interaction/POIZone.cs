using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Point-of-Interest trigger zone that smoothly reduces player locomotion speed
    /// to encourage deliberate observation when approaching specimens, animals, or exhibits.
    /// </summary>
    [AddComponentMenu("Locomotion/POI Zone")]
    [RequireComponent(typeof(Collider))]
    public class POIZone : MonoBehaviour
    {
        [Tooltip("Speed multiplier inside POI zone (0.44 = ~0.8 m/s observation speed).")]
        [SerializeField] private float speedMultiplier = 0.44f;

        private void OnTriggerEnter(Collider other)
        {
            LocomotionModifier modifier = other.GetComponentInParent<LocomotionModifier>();
            if (modifier != null)
            {
                modifier.SetTargetModifier(speedMultiplier);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            LocomotionModifier modifier = other.GetComponentInParent<LocomotionModifier>();
            if (modifier != null)
            {
                modifier.ResetModifier();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.3f, 0.3f);
            Collider col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius * transform.lossyScale.x);
            }
        }
    }
}
