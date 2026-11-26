using UnityEngine;

namespace EcosystemChoices.Locomotion
{
    /// <summary>
    /// Comfort system providing dynamic FOV reduction (tunneling vignette) during locomotion & acceleration.
    /// Helps reduce motion sickness by obscuring peripheral vision during artificial movement.
    /// Includes a procedural OnGUI/Texture renderer for instant out-of-the-box operation.
    /// </summary>
    [AddComponentMenu("Locomotion/Comfort Vignette")]
    public class ComfortVignette : MonoBehaviour
    {
        [Header("Comfort Settings")]
        [SerializeField] private bool vignetteEnabled = true;

        [Tooltip("Maximum vignette opacity (0.0 to 1.0).")]
        [Range(0f, 1f)]
        [SerializeField] private float maxIntensity = 0.7f;

        [Tooltip("Speed of vignette fade in/out transitions.")]
        [SerializeField] private float transitionSpeed = 3.0f;

        [Tooltip("Minimum movement speed required to activate vignette.")]
        [SerializeField] private float minMovementSpeedThreshold = 0.2f;

        [Tooltip("Sensitivity to acceleration.")]
        [SerializeField] private float accelerationSensitivity = 0.3f;

        [Header("References")]
        [SerializeField] private ContinuousLocomotion continuousLocomotion;

        private float currentVignetteAlpha;
        private Texture2D vignetteTexture;

        public bool Enabled { get => vignetteEnabled; set => vignetteEnabled = value; }
        public float MaxIntensity { get => maxIntensity; set => maxIntensity = value; }

        private void Awake()
        {
            if (continuousLocomotion == null)
            {
                continuousLocomotion = GetComponent<ContinuousLocomotion>();
                if (continuousLocomotion == null) continuousLocomotion = GetComponentInParent<ContinuousLocomotion>();
            }

            CreateVignetteTexture();
        }

        private void Update()
        {
            if (!vignetteEnabled || continuousLocomotion == null)
            {
                currentVignetteAlpha = Mathf.MoveTowards(currentVignetteAlpha, 0f, transitionSpeed * Time.deltaTime);
                return;
            }

            float currentSpeed = continuousLocomotion.CurrentSpeed;
            bool isAccelerating = continuousLocomotion.IsAccelerating;

            float targetAlpha = 0f;
            if (currentSpeed > minMovementSpeedThreshold)
            {
                float speedFactor = Mathf.Clamp01(currentSpeed / 2.0f);
                float accelFactor = isAccelerating ? accelerationSensitivity : 0f;
                targetAlpha = Mathf.Clamp(speedFactor * maxIntensity + accelFactor, 0f, maxIntensity);
            }

            currentVignetteAlpha = Mathf.MoveTowards(currentVignetteAlpha, targetAlpha, transitionSpeed * Time.deltaTime);
        }

        private void CreateVignetteTexture()
        {
            int size = 256;
            vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float maxRadius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxRadius;
                    float alpha = Mathf.Clamp01(Mathf.Pow(dist, 2.5f));
                    vignetteTexture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }
            vignetteTexture.Apply();
        }

        private void OnGUI()
        {
#if UNITY_EDITOR
            if (UnityEngine.XR.XRSettings.isDeviceActive) return;
            if (!vignetteEnabled || currentVignetteAlpha <= 0.01f || vignetteTexture == null) return;

            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, currentVignetteAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vignetteTexture);
            GUI.color = originalColor;
#endif
        }

        private void OnDestroy()
        {
            if (vignetteTexture != null)
            {
                Destroy(vignetteTexture);
            }
        }
    }
}
