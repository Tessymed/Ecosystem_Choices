using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EcosystemChoices.UI
{
    /// <summary>
    /// Dual VR Controller & Editor Mouse raycaster for 3D UI interaction.
    /// Projects an elegant laser beam and cursor dot onto world-space cards,
    /// triggering hover, click-in, and haptic feedback on VRButtons.
    /// Uses the new Unity Input System and Meta XR OVRInput with zero legacy input calls.
    /// </summary>
    [AddComponentMenu("UI/VR UI Raycaster")]
    public class VRUIRaycaster : MonoBehaviour
    {
        [Header("Controller Setup")]
        [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.RTouch;
        [SerializeField] private Transform customRayOrigin;

        [Header("Raycast Parameters")]
        [SerializeField] private float maxRayDistance = 10.0f;
        [SerializeField] private LayerMask uiLayers = ~0;

        [Header("Visual Laser & Reticle")]
        [SerializeField] private bool enableLaser = true;
        [SerializeField] private float laserWidth = 0.005f;
        [SerializeField] private Color laserColor = new Color(0.35f, 0.85f, 0.55f, 0.6f);
        [SerializeField] private Color hoverLaserColor = new Color(0.55f, 1.0f, 0.75f, 0.95f);
        [SerializeField] private float reticleSize = 0.025f;

        private LineRenderer lineRenderer;
        private GameObject reticleObj;
        private Material laserMat;
        private VRButton currentHoveredButton;
        public bool IsTriggerDown { get; private set; }

        public OVRInput.Controller Controller { get => controller; set => controller = value; }
        public Transform CustomRayOrigin { get => customRayOrigin; set => customRayOrigin = value; }

        private void Awake()
        {
            SetupLaserAndReticle();
        }

        private void SetupLaserAndReticle()
        {
            // Configure LineRenderer
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = laserWidth;
            lineRenderer.endWidth = laserWidth * 0.4f;
            lineRenderer.positionCount = 2;

            // Shader resolution for URP
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            laserMat = new Material(unlitShader);
            laserMat.SetColor("_BaseColor", laserColor);
            laserMat.SetColor("_Color", laserColor);
            lineRenderer.material = laserMat;
            lineRenderer.enabled = enableLaser;

            int uiLayer = LayerMask.NameToLayer("UI");
            gameObject.layer = uiLayer;

            // Create Reticle Dot
            reticleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            reticleObj.name = "VR_Raycaster_Reticle";
            reticleObj.transform.SetParent(transform);
            reticleObj.transform.localScale = Vector3.one * reticleSize;
            reticleObj.layer = uiLayer;

            Collider col = reticleObj.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            Renderer retRend = reticleObj.GetComponent<Renderer>();
            if (retRend != null)
            {
                Material retMat = new Material(unlitShader);
                retMat.SetColor("_BaseColor", hoverLaserColor);
                retMat.SetColor("_Color", hoverLaserColor);
                retRend.material = retMat;
            }

            reticleObj.SetActive(false);
        }

        private void Update()
        {
            Ray ray = GetInteractionRay();
            bool hasHit = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, uiLayers, QueryTriggerInteraction.Collide);

            Vector3 endPos = ray.origin + ray.direction * maxRayDistance;

            if (hasHit)
            {
                endPos = hit.point;
                VRButton btn = hit.collider.GetComponentInParent<VRButton>();

                if (btn != null)
                {
                    if (currentHoveredButton != btn)
                    {
                        if (currentHoveredButton != null)
                        {
                            currentHoveredButton.OnPointerExit(controller);
                        }
                        currentHoveredButton = btn;
                        currentHoveredButton.OnPointerEnter(controller);
                    }
                }
                else
                {
                    ClearHover();
                }

                if (reticleObj != null)
                {
                    reticleObj.SetActive(true);
                    reticleObj.transform.position = hit.point - ray.direction * 0.005f;
                    reticleObj.transform.rotation = Quaternion.LookRotation(hit.normal);
                }
            }
            else
            {
                ClearHover();
                if (reticleObj != null) reticleObj.SetActive(false);
            }

            // Update Laser line positions and tint
            if (lineRenderer != null && enableLaser)
            {
                lineRenderer.enabled = true;
                lineRenderer.SetPosition(0, ray.origin);
                lineRenderer.SetPosition(1, endPos);

                Color targetCol = currentHoveredButton != null ? hoverLaserColor : laserColor;
                if (laserMat != null)
                {
                    laserMat.SetColor("_BaseColor", targetCol);
                    laserMat.SetColor("_Color", targetCol);
                }
            }

            // Handle Trigger / Click Input
            HandleInput();
        }

        private Ray GetInteractionRay()
        {
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
            // If in Unity Editor, check if mouse interaction is active
            if (Mouse.current != null && Camera.main != null)
            {
                bool isVRTracked = false;
                try
                {
                    isVRTracked = OVRPlugin.userPresent && OVRInput.IsControllerConnected(controller);
                }
                catch { }

                if (!isVRTracked || Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasPressedThisFrame)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    return Camera.main.ScreenPointToRay(mousePos);
                }
            }
#endif

            // VR Controller Ray
            Transform origin = customRayOrigin != null ? customRayOrigin : transform;
            return new Ray(origin.position, origin.forward);
        }

        private void HandleInput()
        {
            bool triggerDown = false;
            bool triggerUp = false;

            // 1. Meta XR Controller Trigger
#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            try
            {
                triggerDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller) ||
                              OVRInput.GetDown(OVRInput.Button.One, controller);
                triggerUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, controller) ||
                            OVRInput.GetUp(OVRInput.Button.One, controller);
            }
            catch { }
#endif

            // 2. Editor Mouse Fallback via New Input System
#if UNITY_EDITOR && ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame) triggerDown = true;
                if (Mouse.current.leftButton.wasReleasedThisFrame) triggerUp = true;
            }
#endif

            if (triggerDown)
            {
                IsTriggerDown = true;
                if (currentHoveredButton != null)
                {
                    currentHoveredButton.OnPointerDown(controller);
                }
            }

            if (triggerUp)
            {
                IsTriggerDown = false;
                if (currentHoveredButton != null)
                {
                    currentHoveredButton.OnPointerUp(controller);
                }
            }
        }

        private void ClearHover()
        {
            if (currentHoveredButton != null)
            {
                currentHoveredButton.OnPointerExit(controller);
                currentHoveredButton = null;
            }
        }

        private void OnDisable()
        {
            ClearHover();
            if (lineRenderer != null) lineRenderer.enabled = false;
            if (reticleObj != null) reticleObj.SetActive(false);
        }
    }
}
