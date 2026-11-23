using System.Collections.Generic;
using UnityEngine;

namespace EcosystemChoices.Environment
{
    /// <summary>
    /// Identifies a wooden bridge structure in the ecosystem.
    /// Provides spatial and collider registration for MovementAudio footstep crossfading,
    /// and ensures the bridge is recognized as a valid walking path by PathMovementConstraint.
    /// </summary>
    [AddComponentMenu("Environment/Wooden Bridge Surface")]
    [DisallowMultipleComponent]
    public class WoodenBridgeSurface : MonoBehaviour
    {
        private static readonly List<WoodenBridgeSurface> activeBridges = new List<WoodenBridgeSurface>();
        public static IReadOnlyList<WoodenBridgeSurface> ActiveBridges => activeBridges;

        [Header("Bridge Audio & Bounds")]
        [Tooltip("Horizontal expansion margin in meters around the bridge bounds for smooth footstep crossfade.")]
        [SerializeField] private float horizontalPadding = 0.8f;

        [Tooltip("Vertical height tolerance in meters above and below bridge deck.")]
        [SerializeField] private float verticalTolerance = 2.2f;

        [Tooltip("Auto-assign this bridge and its child colliders to the 'Path' layer (Layer 7).")]
        [SerializeField] private bool autoAssignPathLayer = true;

        [Header("Runtime Bounds (Read-only)")]
        [SerializeField] private Bounds calculatedBounds;

        private Collider[] cachedColliders;

        private void OnEnable()
        {
            if (!activeBridges.Contains(this))
            {
                activeBridges.Add(this);
            }

            CacheAndInitialize();
        }

        private void OnDisable()
        {
            activeBridges.Remove(this);
        }

        private void Start()
        {
            CacheAndInitialize();
        }

        public void CacheAndInitialize()
        {
            cachedColliders = GetComponentsInChildren<Collider>(true);
            RecalculateBounds();

            if (autoAssignPathLayer)
            {
                AssignPathLayer();
            }
        }

        public void AssignPathLayer()
        {
            int pathLayer = LayerMask.NameToLayer("Path");
            if (pathLayer >= 0)
            {
                SetLayerRecursively(gameObject, pathLayer);
            }
        }

        private void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
            }
        }

        public void RecalculateBounds()
        {
            if (cachedColliders == null || cachedColliders.Length == 0)
            {
                cachedColliders = GetComponentsInChildren<Collider>(true);
            }

            if (cachedColliders != null && cachedColliders.Length > 0)
            {
                Bounds b = cachedColliders[0].bounds;
                for (int i = 1; i < cachedColliders.Length; i++)
                {
                    if (cachedColliders[i] != null)
                    {
                        b.Encapsulate(cachedColliders[i].bounds);
                    }
                }
                calculatedBounds = b;
            }
            else
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                if (renderers != null && renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        if (renderers[i] != null)
                        {
                            b.Encapsulate(renderers[i].bounds);
                        }
                    }
                    calculatedBounds = b;
                }
                else
                {
                    calculatedBounds = new Bounds(transform.position, new Vector3(5f, 3f, 5f));
                }
            }
        }

        public bool ContainsPoint(Vector3 worldPoint)
        {
            float minX = calculatedBounds.min.x - horizontalPadding;
            float maxX = calculatedBounds.max.x + horizontalPadding;
            float minZ = calculatedBounds.min.z - horizontalPadding;
            float maxZ = calculatedBounds.max.z + horizontalPadding;

            float minY = calculatedBounds.min.y - 0.5f;
            float maxY = calculatedBounds.max.y + verticalTolerance;

            return worldPoint.x >= minX && worldPoint.x <= maxX &&
                   worldPoint.z >= minZ && worldPoint.z <= maxZ &&
                   worldPoint.y >= minY && worldPoint.y <= maxY;
        }

        public bool ContainsCollider(Collider col)
        {
            if (col == null) return false;
            if (cachedColliders == null) cachedColliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] == col) return true;
            }

            return false;
        }

        // ================= Static Helpers =================

        public static bool IsPointOnAnyBridge(Vector3 worldPoint)
        {
            for (int i = 0; i < activeBridges.Count; i++)
            {
                WoodenBridgeSurface bridge = activeBridges[i];
                if (bridge != null && bridge.ContainsPoint(worldPoint))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsPointOnAnyBridge(Vector3 worldPoint, out Vector3 bridgeSurfacePoint)
        {
            bridgeSurfacePoint = worldPoint;
            for (int i = 0; i < activeBridges.Count; i++)
            {
                WoodenBridgeSurface bridge = activeBridges[i];
                if (bridge != null && bridge.ContainsPoint(worldPoint))
                {
                    bridgeSurfacePoint = new Vector3(worldPoint.x, bridge.calculatedBounds.max.y, worldPoint.z);
                    return true;
                }
            }
            return false;
        }

        public static bool IsColliderOnAnyBridge(Collider col)
        {
            if (col == null) return false;
            for (int i = 0; i < activeBridges.Count; i++)
            {
                WoodenBridgeSurface bridge = activeBridges[i];
                if (bridge != null && bridge.ContainsCollider(col))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.55f, 0.15f, 0.35f);
            Gizmos.DrawCube(calculatedBounds.center, calculatedBounds.size);
            Gizmos.color = new Color(0.85f, 0.55f, 0.15f, 0.85f);
            Gizmos.DrawWireCube(calculatedBounds.center, calculatedBounds.size + new Vector3(horizontalPadding * 2f, verticalTolerance, horizontalPadding * 2f));
        }
    }
}
