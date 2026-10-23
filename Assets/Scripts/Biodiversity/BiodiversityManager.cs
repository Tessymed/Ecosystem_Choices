using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    /// <summary>
    /// Master coordinator for ecosystem biodiversity elements (birds, millipedes/crawlers, butterflies, wildlife).
    /// Provides global activity controls, density scaling, and scene subsystem initialization.
    /// </summary>
    [AddComponentMenu("Biodiversity/Biodiversity Manager")]
    public class BiodiversityManager : MonoBehaviour
    {
        [Header("System Toggles")]
        [Tooltip("Enable sky bird flock flyby events.")]
        [SerializeField] private bool enableBirds = true;

        [Tooltip("Enable ant / millipede path crossing events.")]
        [SerializeField] private bool enableInsects = true;

        [Tooltip("Enable butterfly fluttering in green flora areas.")]
        [SerializeField] private bool enableButterflies = true;

        [Header("Global Density & Speed Controls")]
        [Tooltip("Activity frequency multiplier (1.0 = normal, 2.0 = double frequency, 0.5 = half frequency).")]
        [Range(0.2f, 3.0f)]
        [SerializeField] private float globalActivityDensity = 1.0f;

        [Tooltip("Global speed multiplier for crawling millipedes and insects.")]
        [Range(0.1f, 3.0f)]
        [SerializeField] private float insectSpeedMultiplier = 1.0f;

        [Header("Subsystem References")]
        [SerializeField] private BirdFlockSpawner birdSpawner;
        [SerializeField] private InsectLineSpawner insectSpawner;
        [SerializeField] private ButterflySpawner butterflySpawner;

        public bool EnableBirds
        {
            get => enableBirds;
            set
            {
                enableBirds = value;
                if (birdSpawner != null) birdSpawner.enabled = value;
            }
        }

        public bool EnableInsects
        {
            get => enableInsects;
            set
            {
                enableInsects = value;
                if (insectSpawner != null) insectSpawner.enabled = value;
            }
        }

        public bool EnableButterflies
        {
            get => enableButterflies;
            set
            {
                enableButterflies = value;
                if (butterflySpawner != null) butterflySpawner.enabled = value;
            }
        }

        public float GlobalActivityDensity
        {
            get => globalActivityDensity;
            set => globalActivityDensity = Mathf.Clamp(value, 0.2f, 3.0f);
        }

        public float InsectSpeedMultiplier
        {
            get => insectSpeedMultiplier;
            set
            {
                insectSpeedMultiplier = Mathf.Clamp(value, 0.1f, 3.0f);
                if (insectSpawner != null) insectSpawner.SpeedMultiplier = insectSpeedMultiplier;
            }
        }

        private void Awake()
        {
            if (birdSpawner == null) birdSpawner = GetComponentInChildren<BirdFlockSpawner>();
            if (insectSpawner == null) insectSpawner = GetComponentInChildren<InsectLineSpawner>();
            if (butterflySpawner == null) butterflySpawner = GetComponentInChildren<ButterflySpawner>();
        }

        private void Start()
        {
            ApplySettings();
        }

        /// <summary>
        /// Applies global activity settings to sub-spawners.
        /// </summary>
        public void ApplySettings()
        {
            if (birdSpawner != null) birdSpawner.enabled = enableBirds;
            if (insectSpawner != null)
            {
                insectSpawner.enabled = enableInsects;
                insectSpawner.SpeedMultiplier = insectSpeedMultiplier;
            }
            if (butterflySpawner != null) butterflySpawner.enabled = enableButterflies;
        }

        /// <summary>
        /// Instantly triggers a bird flyby event for testing or script triggers.
        /// </summary>
        public void TriggerBirdFlyby()
        {
            if (birdSpawner != null) birdSpawner.TriggerBirdFlock();
        }

        /// <summary>
        /// Instantly triggers an insect/millipede line crossing event for testing or script triggers.
        /// </summary>
        public void TriggerInsectCrossing()
        {
            if (insectSpawner != null) insectSpawner.TriggerInsectCrossing();
        }

        /// <summary>
        /// Instantly triggers a butterfly flight event on green flora terrain.
        /// </summary>
        public void TriggerButterflySpawn()
        {
            if (butterflySpawner != null) butterflySpawner.TriggerButterflySpawn();
        }
    }
}
