using UnityEngine;

namespace EcosystemChoices.Biodiversity
{
    public enum InsectGaitType
    {
        StandardTripod,
        MillipedeWave
    }

    /// <summary>
    /// Procedurally animates rigged or un-rigged 3D insect models (ants, beetles, spiders, millipedes, bugs)
    /// without requiring pre-baked animation clips.
    /// Supports standard tripod leg gaits as well as multi-segment millipede leg ripple waves.
    /// </summary>
    [AddComponentMenu("Biodiversity/Procedural Insect Animator")]
    public class ProceduralInsectAnimator : MonoBehaviour
    {
        [Header("Gait Type")]
        [Tooltip("StandardTripod for 6-legged insects (ants, beetles); MillipedeWave for multi-legged crawlers (millipedes).")]
        [SerializeField] private InsectGaitType gaitType = InsectGaitType.StandardTripod;

        [Header("Procedural Scurry Dynamics")]
        [Tooltip("Frequency/speed of procedural leg & body crawl cycle.")]
        [SerializeField] private float crawlFrequency = 18.0f;

        [Tooltip("Side-to-side body wiggle angle in degrees.")]
        [SerializeField] private float bodyWiggleAngle = 5.0f;

        [Tooltip("Body roll tilt angle in degrees.")]
        [SerializeField] private float bodyRollAngle = 3.0f;

        [Tooltip("Vertical footstep bobbing height in meters.")]
        [SerializeField] private float verticalBobHeight = 0.006f;

        [Header("Leg Bone Procedural Gait")]
        [Tooltip("Enable procedural leg joint rotation for rigged insect models.")]
        [SerializeField] private bool animateLegBones = true;

        [Tooltip("Leg swing angle in degrees for legs.")]
        [SerializeField] private float legSwingAngle = 18.0f;

        [Tooltip("Phase offset per leg pair for MillipedeWave metachronal wave ripple (radians).")]
        [SerializeField] private float wavePhaseStep = 0.45f;

        [Tooltip("Left leg bone transforms (front to back). Auto-detected if empty.")]
        [SerializeField] private Transform[] leftLegs;

        [Tooltip("Right leg bone transforms (front to back). Auto-detected if empty.")]
        [SerializeField] private Transform[] rightLegs;

        private Vector3 lastPosition;
        private Vector3 initialChildLocalPos;
        private Quaternion initialChildLocalRot;
        private Transform targetModelTransform;
        private Quaternion[] initialLeftLegRots;
        private Quaternion[] initialRightLegRots;

        public InsectGaitType GaitType { get => gaitType; set => gaitType = value; }

        private void Awake()
        {
            targetModelTransform = transform.childCount > 0 ? transform.GetChild(0) : transform;

            if (targetModelTransform != null)
            {
                initialChildLocalPos = targetModelTransform.localPosition;
                initialChildLocalRot = targetModelTransform.localRotation;
            }

            lastPosition = transform.position;

            if (animateLegBones)
            {
                AutoDetectLegBones();
            }
        }

        public void AutoDetectLegBones()
        {
            if (leftLegs == null || leftLegs.Length == 0 || rightLegs == null || rightLegs.Length == 0)
            {
                var allChildren = GetComponentsInChildren<Transform>();
                var leftList = new System.Collections.Generic.List<Transform>();
                var rightList = new System.Collections.Generic.List<Transform>();

                foreach (var t in allChildren)
                {
                    if (t == transform || t == targetModelTransform) continue;
                    string nameLower = t.name.ToLower();
                    if (nameLower.Contains("leg") || nameLower.Contains("foot") || nameLower.Contains("thigh") || nameLower.Contains("hip") || nameLower.Contains("claw") || nameLower.Contains("segment"))
                    {
                        if (nameLower.Contains("l") || nameLower.Contains("left") || nameLower.EndsWith("_l") || nameLower.EndsWith(".l"))
                        {
                            leftList.Add(t);
                        }
                        else if (nameLower.Contains("r") || nameLower.Contains("right") || nameLower.EndsWith("_r") || nameLower.EndsWith(".r"))
                        {
                            rightList.Add(t);
                        }
                    }
                }

                if (leftList.Count > 0) leftLegs = leftList.ToArray();
                if (rightList.Count > 0) rightLegs = rightList.ToArray();
            }

            // Store initial local rotations of leg bones
            if (leftLegs != null && leftLegs.Length > 0)
            {
                initialLeftLegRots = new Quaternion[leftLegs.Length];
                for (int i = 0; i < leftLegs.Length; i++)
                {
                    if (leftLegs[i] != null) initialLeftLegRots[i] = leftLegs[i].localRotation;
                }
            }

            if (rightLegs != null && rightLegs.Length > 0)
            {
                initialRightLegRots = new Quaternion[rightLegs.Length];
                for (int i = 0; i < rightLegs.Length; i++)
                {
                    if (rightLegs[i] != null) initialRightLegRots[i] = rightLegs[i].localRotation;
                }
            }

            // Auto-set MillipedeWave if leg count is large (> 8 legs)
            if (leftLegs != null && leftLegs.Length > 4)
            {
                gaitType = InsectGaitType.MillipedeWave;
            }
        }

        private void Update()
        {
            Vector3 currentPos = transform.position;
            Vector3 moveDelta = currentPos - lastPosition;
            moveDelta.y = 0f;
            lastPosition = currentPos;

            float speed = moveDelta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

            if (speed > 0.01f)
            {
                AnimateProceduralCrawl(speed);
            }
            else
            {
                ResetPose();
            }
        }

        private void AnimateProceduralCrawl(float speed)
        {
            float timeFactor = Time.time * crawlFrequency * Mathf.Clamp(speed * 1.5f, 0.5f, 3.0f);

            // 1. Procedural Body Scurry (Yaw Wiggle, Roll Tilt, Vertical Bob)
            float yawScale = gaitType == InsectGaitType.MillipedeWave ? 0.3f : 1.0f;
            float yaw = Mathf.Sin(timeFactor) * bodyWiggleAngle * yawScale;
            float roll = Mathf.Cos(timeFactor * 2.0f) * bodyRollAngle * yawScale;
            float bob = Mathf.Abs(Mathf.Sin(timeFactor * 2.0f)) * verticalBobHeight;

            if (targetModelTransform != null)
            {
                targetModelTransform.localPosition = initialChildLocalPos + Vector3.up * bob;
                targetModelTransform.localRotation = initialChildLocalRot * Quaternion.Euler(0f, yaw, roll);
            }

            // 2. Procedural Leg Gait
            if (animateLegBones)
            {
                if (gaitType == InsectGaitType.MillipedeWave)
                {
                    // Millipede Metachronal Wave Ripple Gait
                    if (leftLegs != null && initialLeftLegRots != null)
                    {
                        for (int i = 0; i < leftLegs.Length; i++)
                        {
                            if (leftLegs[i] == null) continue;
                            float legPhase = timeFactor + (i * wavePhaseStep);
                            float swing = Mathf.Sin(legPhase) * legSwingAngle;
                            leftLegs[i].localRotation = initialLeftLegRots[i] * Quaternion.Euler(0f, swing, 0f);
                        }
                    }

                    if (rightLegs != null && initialRightLegRots != null)
                    {
                        for (int i = 0; i < rightLegs.Length; i++)
                        {
                            if (rightLegs[i] == null) continue;
                            float legPhase = timeFactor + (i * wavePhaseStep) + Mathf.PI * 0.5f;
                            float swing = Mathf.Sin(legPhase) * legSwingAngle;
                            rightLegs[i].localRotation = initialRightLegRots[i] * Quaternion.Euler(0f, swing, 0f);
                        }
                    }
                }
                else
                {
                    // Standard Tripod Gait
                    if (leftLegs != null && initialLeftLegRots != null)
                    {
                        for (int i = 0; i < leftLegs.Length; i++)
                        {
                            if (leftLegs[i] == null) continue;
                            float phase = (i % 2 == 0) ? timeFactor : timeFactor + Mathf.PI;
                            float swing = Mathf.Sin(phase) * legSwingAngle;
                            leftLegs[i].localRotation = initialLeftLegRots[i] * Quaternion.Euler(0f, swing, 0f);
                        }
                    }

                    if (rightLegs != null && initialRightLegRots != null)
                    {
                        for (int i = 0; i < rightLegs.Length; i++)
                        {
                            if (rightLegs[i] == null) continue;
                            float phase = (i % 2 == 0) ? timeFactor + Mathf.PI : timeFactor;
                            float swing = Mathf.Sin(phase) * legSwingAngle;
                            rightLegs[i].localRotation = initialRightLegRots[i] * Quaternion.Euler(0f, swing, 0f);
                        }
                    }
                }
            }
        }

        private void ResetPose()
        {
            if (targetModelTransform != null)
            {
                targetModelTransform.localPosition = Vector3.Lerp(targetModelTransform.localPosition, initialChildLocalPos, Time.deltaTime * 10f);
                targetModelTransform.localRotation = Quaternion.Slerp(targetModelTransform.localRotation, initialChildLocalRot, Time.deltaTime * 10f);
            }

            if (animateLegBones)
            {
                if (leftLegs != null && initialLeftLegRots != null)
                {
                    for (int i = 0; i < leftLegs.Length; i++)
                    {
                        if (leftLegs[i] != null) leftLegs[i].localRotation = Quaternion.Slerp(leftLegs[i].localRotation, initialLeftLegRots[i], Time.deltaTime * 10f);
                    }
                }

                if (rightLegs != null && initialRightLegRots != null)
                {
                    for (int i = 0; i < rightLegs.Length; i++)
                    {
                        if (rightLegs[i] != null) rightLegs[i].localRotation = Quaternion.Slerp(rightLegs[i].localRotation, initialRightLegRots[i], Time.deltaTime * 10f);
                    }
                }
            }
        }
    }
}
