using System;
using UnityEngine;
using UnityEngine.Events;

namespace EcosystemChoices.UI
{
    /// <summary>
    /// Highly responsive, tactile VR button designed for world-space cards.
    /// Implements visual mechanical click-in depression, hover expansion, audio triggers,
    /// and Meta Quest controller haptic feedback.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("UI/VR Button")]
    public class VRButton : MonoBehaviour
    {
        [Header("Button Identification")]
        [SerializeField] private string buttonId = "Button";
        public string ButtonId { get => buttonId; set => buttonId = value; }

        [Header("Click-in Mechanical Animation (Canvas Units)")]
        [Tooltip("How far the button depresses inward (canvas units) when clicked. In 0.00075 scale canvas, 25 units ≈ 1.9 cm.")]
        [SerializeField] private float pressDepth = 25.0f;

        [Tooltip("Scale multiplier when hovered.")]
        [SerializeField] private float hoverScaleMultiplier = 1.06f;

        [Tooltip("Scale multiplier when depressed.")]
        [SerializeField] private float pressedScaleMultiplier = 0.94f;

        [Tooltip("Speed of the smooth mechanical spring animation.")]
        [SerializeField] private float animationSpeed = 18.0f;

        [Header("Visual Highlighting")]
        [Tooltip("Optional child transform that animates (defaults to this transform).")]
        [SerializeField] private Transform visualTransform;

        [Tooltip("Optional renderer to apply hover tint.")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = new Color(1.2f, 1.2f, 1.2f, 1f);

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip hoverSound;
        [SerializeField] private AudioClip clickSound;

        [Header("Haptics")]
        [Range(0f, 1f)]
        [SerializeField] private float hoverHapticAmplitude = 0.25f;
        [Range(0f, 1f)]
        [SerializeField] private float clickHapticAmplitude = 0.75f;
        [SerializeField] private float hapticDuration = 0.08f;

        [Header("Events")]
        public UnityEvent onClick = new UnityEvent();

        public bool IsHovered { get; private set; }
        public bool IsPressed { get; private set; }

        public AudioClip HoverSound { get => hoverSound; set => hoverSound = value; }
        public AudioClip ClickSound { get => clickSound; set => clickSound = value; }
        public Transform VisualTransform { get => visualTransform; set => visualTransform = value; }

        private Vector3 initialLocalPos;
        private Vector3 initialScale;
        private Vector3 targetLocalPos;
        private Vector3 targetScale;
        private AudioSource audioSource;
        private MaterialPropertyBlock propBlock;

        private void Awake()
        {
            if (visualTransform == null) visualTransform = transform;
            initialLocalPos = visualTransform.localPosition;
            initialScale = visualTransform.localScale;
            targetLocalPos = initialLocalPos;
            targetScale = initialScale;

            propBlock = new MaterialPropertyBlock();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f; // 2D UI sound for crisp feedback in HMD
            }
            audioSource.ignoreListenerVolume = true; // Plays clearly even when scene audio is muted in void menu
        }

        private void Update()
        {
            // Smooth spring lerp for position and scale
            if (visualTransform != null)
            {
                visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, targetLocalPos, Time.unscaledDeltaTime * animationSpeed);
                visualTransform.localScale = Vector3.Lerp(visualTransform.localScale, targetScale, Time.unscaledDeltaTime * animationSpeed);
            }
        }

        public void OnPointerEnter(OVRInput.Controller controller)
        {
            if (IsHovered) return;
            IsHovered = true;

            if (!IsPressed)
            {
                targetScale = initialScale * hoverScaleMultiplier;
                // Float slightly forward towards user (negative Z in canvas space)
                targetLocalPos = initialLocalPos - Vector3.forward * (pressDepth * 0.4f);
            }

            PlaySound(hoverSound, 0.45f);
            TriggerHaptic(controller, hoverHapticAmplitude, hapticDuration * 0.6f);
            SetTint(hoverColor);
        }

        public void OnPointerExit(OVRInput.Controller controller)
        {
            if (!IsHovered) return;
            IsHovered = false;
            IsPressed = false;

            targetScale = initialScale;
            targetLocalPos = initialLocalPos;
            SetTint(normalColor);
        }

        public void OnPointerDown(OVRInput.Controller controller)
        {
            IsPressed = true;

            // Depress physically inward away from user ("click in", positive Z in canvas space)
            targetLocalPos = initialLocalPos + Vector3.forward * pressDepth;
            targetScale = initialScale * pressedScaleMultiplier;

            PlaySound(clickSound, 0.85f);
            TriggerHaptic(controller, clickHapticAmplitude, hapticDuration);
        }

        public void OnPointerUp(OVRInput.Controller controller)
        {
            if (!IsPressed) return;
            IsPressed = false;

            if (IsHovered)
            {
                targetScale = initialScale * hoverScaleMultiplier;
                targetLocalPos = initialLocalPos - Vector3.forward * (pressDepth * 0.4f);
                onClick.Invoke();
            }
            else
            {
                targetScale = initialScale;
                targetLocalPos = initialLocalPos;
            }
        }

        private void PlaySound(AudioClip clip, float vol)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, vol);
            }
        }

        private void TriggerHaptic(OVRInput.Controller controller, float amplitude, float duration)
        {
#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            try
            {
                if (controller != OVRInput.Controller.None)
                {
                    OVRInput.SetControllerVibration(1.0f, amplitude, controller);
                    StartCoroutine(StopHapticsCoroutine(controller, duration));
                }
            }
            catch { }
#endif
        }

        private System.Collections.IEnumerator StopHapticsCoroutine(OVRInput.Controller controller, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            try
            {
                OVRInput.SetControllerVibration(0f, 0f, controller);
            }
            catch { }
        }

        private void SetTint(Color col)
        {
            if (targetRenderer != null)
            {
                targetRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", col);
                propBlock.SetColor("_Color", col);
                targetRenderer.SetPropertyBlock(propBlock);
            }
        }

        private void OnDisable()
        {
            IsHovered = false;
            IsPressed = false;
            if (visualTransform != null)
            {
                visualTransform.localPosition = initialLocalPos;
                visualTransform.localScale = initialScale;
            }
        }
    }
}
