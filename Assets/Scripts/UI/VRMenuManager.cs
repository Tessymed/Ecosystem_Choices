using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace EcosystemChoices.UI
{
    public enum MenuPage
    {
        Home,
        About,
        Quit
    }

    /// <summary>
    /// Coordinates the VR startup flow in a total pitch-black void, comfortable eye-level UI positioning,
    /// page switching (Home, About, Quit), audio-haptic feedback, locomotion locking,
    /// cinematic fade-in into the experience, and in-game Pause/Quit menu toggling.
    /// </summary>
    [AddComponentMenu("UI/VR Menu Manager")]
    public class VRMenuManager : MonoBehaviour
    {
        [Header("UI Cards / Pages")]
        [SerializeField] private GameObject homeCard;
        [SerializeField] private GameObject aboutCard;
        [SerializeField] private GameObject quitCard;

        [Header("Card Placement in VR")]
        [Tooltip("Distance (meters) in front of player HMD.")]
        [SerializeField] private float cardDistance = 1.5f;

        [Tooltip("Height offset relative to HMD eye level.")]
        [SerializeField] private float eyeHeightOffset = -0.05f;

        [Header("Void & Cinematic Fade")]
        [SerializeField] private float fadeDuration = 1.8f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioClip startChime;
        [SerializeField] private AudioClip pageSwitchSound;

        [Header("VR Raycaster")]
        [SerializeField] private VRUIRaycaster rightRaycaster;

        private Transform playerHmd;
        private MenuPage currentPage = MenuPage.Home;
        private bool isExperienceStarted = false;
        private bool isGamePaused = false;
        private AudioSource menuAudioSource;
        private Vector3 initialCanvasScale;

        private struct CameraBackup
        {
            public Camera cam;
            public int cullingMask;
            public CameraClearFlags clearFlags;
            public Color bgColor;
        }
        private readonly List<CameraBackup> cameraBackups = new List<CameraBackup>();

        public GameObject HomeCard { get => homeCard; set => homeCard = value; }
        public GameObject AboutCard { get => aboutCard; set => aboutCard = value; }
        public GameObject QuitCard { get => quitCard; set => quitCard = value; }
        public AudioClip StartChime { get => startChime; set => startChime = value; }
        public AudioClip PageSwitchSound { get => pageSwitchSound; set => pageSwitchSound = value; }
        public VRUIRaycaster RightRaycaster { get => rightRaycaster; set => rightRaycaster = value; }
        public bool IsExperienceStarted => isExperienceStarted;
        public bool IsGamePaused => isGamePaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureMenuSystemExists()
        {
            if (FindAnyObjectByType<VRMenuManager>() == null)
            {
                GameObject menuRoot = new GameObject("VR_UI_MenuSystem");
                menuRoot.AddComponent<VRMenuManager>();
                Debug.Log("[VRMenuManager] Auto-spawned VR_UI_MenuSystem on scene load.");
            }
        }

        private void Awake()
        {
            initialCanvasScale = transform.localScale;

            menuAudioSource = gameObject.AddComponent<AudioSource>();
            menuAudioSource.playOnAwake = false;
            menuAudioSource.spatialBlend = 0f; // Crisp 2D UI audio in HMD
            menuAudioSource.ignoreListenerVolume = true; // Plays even when AudioListener is muted!

            FindPlayerHmd();

            // Lock all locomotion and teleportation during the startup void phase
            SetLocomotionActive(false);

            // Mute all world/nature audio completely during the void phase
            MuteSceneAudio(true);

            // Ensure this Canvas and all card children are assigned to the UI layer (Layer 5)
            int uiLayer = LayerMask.NameToLayer("UI");
            SetLayerRecursively(gameObject, uiLayer);

            if (rightRaycaster != null)
            {
                SetLayerRecursively(rightRaycaster.gameObject, uiLayer);
            }

            // Apply camera culling mask: Only UI layer renders against solid black background!
            ApplyVoidCulling();

            // Build menu hierarchy if not already baked
            if (homeCard == null || aboutCard == null || quitCard == null)
            {
                BuildRuntimeMenu();
            }

            // Apply ZTest Always to all UI card images so they render crisply over the void
            ApplyZTestAlwaysToCards();

            BindButtons();
        }

        private IEnumerator Start()
        {
            PositionCardsInFrontOfHmd();
            ShowPage(MenuPage.Home, instant: true);

            // Wait 1 frame for VR tracking runtime to establish tracking space pose
            yield return null;

            // Re-apply void culling in case VR camera initialized on frame 1
            ApplyVoidCulling();

            // Calibrate card position directly in front of actual tracked head position & rotation
            PositionCardsInFrontOfHmd();

            if (rightRaycaster != null)
            {
                SetLayerRecursively(rightRaycaster.gameObject, LayerMask.NameToLayer("UI"));
            }
        }

        private void Update()
        {
            if (!isExperienceStarted) return;

            // In-game Pause/Quit menu toggle via VR Menu button or Escape key
            bool menuPressed = false;

#if UNITY_ANDROID || UNITY_STANDALONE_WIN
            try
            {
                // Left controller Menu button (standard VR pause button) or B/Y buttons
                menuPressed = OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch) ||
                              OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||   // B button
                              OVRInput.GetDown(OVRInput.Button.Four, OVRInput.Controller.LTouch);  // Y button
            }
            catch { }
#endif

#if ENABLE_INPUT_SYSTEM
            if (!menuPressed && Keyboard.current != null)
            {
                menuPressed = Keyboard.current.escapeKey.wasPressedThisFrame ||
                              Keyboard.current.pKey.wasPressedThisFrame;
            }
#endif

            if (menuPressed)
            {
                ToggleInGameMenu();
            }
        }

        private void FindPlayerHmd()
        {
            GameObject hmd = GameObject.Find("CenterEyeAnchor");
            if (hmd != null)
            {
                playerHmd = hmd.transform;
                return;
            }

            Camera cam = Camera.main;
            if (cam != null)
            {
                playerHmd = cam.transform;
                return;
            }

            GameObject rigObj = GameObject.Find("OVRCameraRig");
            if (rigObj != null)
            {
                Transform centerEye = rigObj.transform.Find("TrackingSpace/CenterEyeAnchor");
                if (centerEye != null)
                {
                    playerHmd = centerEye;
                    return;
                }
            }

            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerHmd = player.transform;
        }

        private void SetLocomotionActive(bool active)
        {
            var locMgr = FindAnyObjectByType<EcosystemChoices.Locomotion.LocomotionManager>();
            if (locMgr != null) locMgr.enabled = active;

            var contLoc = FindAnyObjectByType<EcosystemChoices.Locomotion.ContinuousLocomotion>();
            if (contLoc != null) contLoc.enabled = active;

            var rotCtrl = FindAnyObjectByType<EcosystemChoices.Locomotion.RotationController>();
            if (rotCtrl != null) rotCtrl.enabled = active;

            var teleCtrl = FindAnyObjectByType<EcosystemChoices.Locomotion.TeleportController>();
            if (teleCtrl != null)
            {
                teleCtrl.enabled = active;
                teleCtrl.Enabled = active;
            }
        }

        private void MuteSceneAudio(bool mute)
        {
            // Mute master AudioListener so no ambient/nature/flock sound plays
            AudioListener.volume = mute ? 0f : 1f;

            // Also explicitly stop RiverSoundController updates while in menu
            var riverCtrl = FindAnyObjectByType<EcosystemChoices.Environment.RiverSoundController>();
            if (riverCtrl != null)
            {
                riverCtrl.enabled = !mute;
            }
        }

        private void ApplyVoidCulling()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            int uiMask = (1 << uiLayer);

            Camera[] cams = Camera.allCameras;
            foreach (Camera c in cams)
            {
                if (c == null) continue;

                // Only record original camera parameters once
                if (!cameraBackups.Exists(b => b.cam == c))
                {
                    int origMask = c.cullingMask;
                    // Guard: if camera was already culled or 0, default to full scene (~0)
                    if (origMask == uiMask || origMask == 0) origMask = ~0;

                    CameraClearFlags origClear = c.clearFlags;
                    if (origClear == CameraClearFlags.SolidColor && c.backgroundColor == Color.black)
                    {
                        origClear = CameraClearFlags.Skybox;
                    }

                    cameraBackups.Add(new CameraBackup
                    {
                        cam = c,
                        cullingMask = origMask,
                        clearFlags = origClear,
                        bgColor = c.backgroundColor
                    });
                }

                c.clearFlags = CameraClearFlags.SolidColor;
                c.backgroundColor = Color.black;
                c.cullingMask = uiMask;
            }
        }

        private void RestoreSceneCulling()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            int uiMask = (1 << uiLayer);

            foreach (var b in cameraBackups)
            {
                if (b.cam != null)
                {
                    b.cam.clearFlags = (b.clearFlags == CameraClearFlags.SolidColor) ? CameraClearFlags.Skybox : b.clearFlags;
                    b.cam.backgroundColor = b.bgColor;
                    b.cam.cullingMask = (b.cullingMask == uiMask || b.cullingMask == 0) ? ~0 : b.cullingMask;
                }
            }

            // Fallback & guarantee for all cameras in scene (CenterEyeAnchor, Camera.main, etc.)
            Camera[] allCams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            foreach (Camera c in allCams)
            {
                if (c == null) continue;
                if (c.cullingMask == uiMask || c.cullingMask == 0)
                {
                    c.cullingMask = ~0;
                }
                if (c.clearFlags == CameraClearFlags.SolidColor)
                {
                    c.clearFlags = CameraClearFlags.Skybox;
                }
            }

            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.Skybox;
                Camera.main.cullingMask = ~0;
            }
        }

        public static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void ApplyZTestAlwaysToCards()
        {
            try
            {
                Shader uiShader = Shader.Find("UI/Default");
                if (uiShader == null) uiShader = Shader.Find("Universal Render Pipeline/Unlit");

                if (uiShader != null)
                {
                    Material uiCardMat = new Material(uiShader);
                    uiCardMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                    uiCardMat.renderQueue = 3000;

                    RawImage[] images = GetComponentsInChildren<RawImage>(true);
                    foreach (var img in images)
                    {
                        img.material = uiCardMat;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[VRMenuManager] Notice applying ZTest: " + ex.Message);
            }
        }

        public void BindButtons()
        {
            VRButton[] buttons = GetComponentsInChildren<VRButton>(true);
            foreach (var btn in buttons)
            {
                btn.onClick.RemoveAllListeners();
                switch (btn.ButtonId)
                {
                    case "Start":
                        btn.onClick.AddListener(OnClickStart);
                        break;
                    case "Options":
                        btn.onClick.AddListener(OnClickAbout);
                        break;
                    case "Quit":
                        btn.onClick.AddListener(OnClickQuit);
                        break;
                    case "Back":
                        btn.onClick.AddListener(OnClickBackToHome);
                        break;
                    case "Yes":
                        btn.onClick.AddListener(OnClickConfirmQuit);
                        break;
                    case "No":
                        btn.onClick.AddListener(OnClickCancelQuit);
                        break;
                }
            }
        }

        public void PositionCardsInFrontOfHmd()
        {
            if (playerHmd == null) FindPlayerHmd();
            if (playerHmd == null) return;

            // Horizontal forward vector (level with horizon)
            Vector3 forward = playerHmd.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 spawnPos = playerHmd.position + forward * cardDistance;
            spawnPos.y = playerHmd.position.y + eyeHeightOffset;

            transform.position = spawnPos;
            transform.rotation = Quaternion.LookRotation(forward);
        }

        public void ShowPage(MenuPage page, bool instant = false)
        {
            currentPage = page;

            if (pageSwitchSound != null && !instant && menuAudioSource != null)
            {
                menuAudioSource.PlayOneShot(pageSwitchSound, 0.65f);
            }

            if (homeCard != null) homeCard.SetActive(page == MenuPage.Home);
            if (aboutCard != null) aboutCard.SetActive(page == MenuPage.About);
            if (quitCard != null) quitCard.SetActive(page == MenuPage.Quit);
        }

        // ================= Button Callbacks =================

        public void OnClickStart()
        {
            if (isExperienceStarted) return;
            isExperienceStarted = true;

            StartCoroutine(StartExperienceRoutine());
        }

        public void OnClickAbout()
        {
            ShowPage(MenuPage.About);
        }

        public void OnClickQuit()
        {
            ShowPage(MenuPage.Quit);
        }

        public void OnClickBackToHome()
        {
            ShowPage(MenuPage.Home);
        }

        public void OnClickConfirmQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OnClickCancelQuit()
        {
            if (isGamePaused)
            {
                ResumeExperience();
            }
            else
            {
                ShowPage(MenuPage.Home);
            }
        }

        private IEnumerator StartExperienceRoutine()
        {
            // 1. Play Start Chime
            if (startChime != null && menuAudioSource != null)
            {
                menuAudioSource.PlayOneShot(startChime, 0.95f);
            }

            // 2. Disable UI raycaster laser immediately
            if (rightRaycaster != null)
            {
                rightRaycaster.enabled = false;
            }

            // 3. Animate UI Card disappearance (shrink inward smoothly)
            float shrinkDuration = 0.35f;
            float elapsed = 0f;
            Vector3 curScale = transform.localScale;
            while (elapsed < shrinkDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / shrinkDuration);
                transform.localScale = Vector3.Lerp(curScale, Vector3.zero, t * t);
                yield return null;
            }

            // Hide cards and restore transform scale for pause menu later
            transform.localScale = initialCanvasScale;
            if (homeCard != null) homeCard.SetActive(false);
            if (aboutCard != null) aboutCard.SetActive(false);
            if (quitCard != null) quitCard.SetActive(false);

            // 4. Create Screen Fade Overlay (WorldSpace Canvas pinned to HMD, 100% shader-independent)
            GameObject fadeCanvasObj = null;
            CanvasGroup fadeGroup = null;

            if (playerHmd == null) FindPlayerHmd();

            if (playerHmd != null)
            {
                try
                {
                    fadeCanvasObj = new GameObject("VR_ScreenFade_Canvas");
                    fadeCanvasObj.transform.SetParent(playerHmd, false);
                    fadeCanvasObj.transform.localPosition = new Vector3(0f, 0f, 0.25f);
                    fadeCanvasObj.transform.localRotation = Quaternion.identity;
                    fadeCanvasObj.transform.localScale = Vector3.one * 0.001f;

                    Canvas canvas = fadeCanvasObj.AddComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.sortingOrder = 32767;

                    fadeGroup = fadeCanvasObj.AddComponent<CanvasGroup>();
                    fadeGroup.alpha = 1f;

                    GameObject blackPanel = new GameObject("BlackOverlay");
                    blackPanel.transform.SetParent(fadeCanvasObj.transform, false);
                    RectTransform rt = blackPanel.AddComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(6000f, 6000f);

                    Image img = blackPanel.AddComponent<Image>();
                    img.color = Color.black;
                    img.raycastTarget = false;

                    int uiLayer = LayerMask.NameToLayer("UI");
                    fadeCanvasObj.layer = uiLayer;
                    blackPanel.layer = uiLayer;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[VRMenuManager] Notice creating fade canvas: " + ex.Message);
                }
            }

            // 5. Restore full 3D camera rendering behind the black fade canvas
            RestoreSceneCulling();

            try
            {
                // 6. Smooth Cinematic Fade-In over fadeDuration (1.8s)
                float fadeElapsed = 0f;
                while (fadeElapsed < fadeDuration)
                {
                    fadeElapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(fadeElapsed / fadeDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, progress);

                    if (fadeGroup != null)
                    {
                        fadeGroup.alpha = 1f - smoothT;
                    }

                    // Ambient world audio gently swells into the user's ears
                    AudioListener.volume = smoothT;

                    yield return null;
                }
            }
            finally
            {
                // 7. GUARANTEED STATE RESTORATION & CLEANUP
                if (fadeCanvasObj != null)
                {
                    Destroy(fadeCanvasObj);
                }

                // Restore full audio volume and river sound
                AudioListener.volume = 1f;
                var riverCtrl = FindAnyObjectByType<EcosystemChoices.Environment.RiverSoundController>();
                if (riverCtrl != null)
                {
                    riverCtrl.enabled = true;
                    riverCtrl.EnsureAudioPlaying();
                }

                // Guarantee full camera rendering and player locomotion
                RestoreSceneCulling();
                SetLocomotionActive(true);
                if (rightRaycaster != null) rightRaycaster.enabled = false;

                Debug.Log("[VRMenuManager] Experience started smoothly! Player in position with zero camera drop.");
            }
        }

        // ================= Mid-Experience Pause / Quit System =================

        public void ToggleInGameMenu()
        {
            if (isGamePaused)
            {
                ResumeExperience();
            }
            else
            {
                PauseExperience();
            }
        }

        public void PauseExperience()
        {
            isGamePaused = true;

            // Position card directly in front of player's current view at eye level
            PositionCardsInFrontOfHmd();

            // Show Quit confirmation card ("Are you sure you want to quit?")
            // Clicking "Yes" exits application; clicking "No" calls OnClickCancelQuit() -> ResumeExperience()
            ShowPage(MenuPage.Quit);

            // Enable raycaster so player can point and click
            if (rightRaycaster != null)
            {
                rightRaycaster.enabled = true;
                SetLayerRecursively(rightRaycaster.gameObject, LayerMask.NameToLayer("UI"));
            }

            // Lock locomotion while in pause menu
            SetLocomotionActive(false);

            Debug.Log("[VRMenuManager] Experience paused. Quit menu displayed.");
        }

        public void ResumeExperience()
        {
            isGamePaused = false;

            // Hide cards
            if (homeCard != null) homeCard.SetActive(false);
            if (aboutCard != null) aboutCard.SetActive(false);
            if (quitCard != null) quitCard.SetActive(false);

            // Disable raycaster during gameplay
            if (rightRaycaster != null)
            {
                rightRaycaster.enabled = false;
            }

            // Re-enable locomotion
            SetLocomotionActive(true);

            Debug.Log("[VRMenuManager] Experience resumed.");
        }

        // ================= Dynamic Menu Construction =================

        public void BuildRuntimeMenu()
        {
            // Setup WorldSpace Canvas
            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;
            scaler.referencePixelsPerUnit = 100;

            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            canvasRect.localScale = Vector3.one * 0.00075f; // ~1.44m x 0.81m
            initialCanvasScale = canvasRect.localScale;

            // Load Textures
            Texture2D homeTex = LoadTexture("Assets/UI/Home Page.jpg");
            Texture2D aboutTex = LoadTexture("Assets/UI/About Page.jpg");
            Texture2D quitTex = LoadTexture("Assets/UI/Quit Page.jpg");

            // Load Audio Clips
            AudioClip hoverClip = LoadAudioClip("Assets/Audio/UI/UI_Hover.wav");
            AudioClip clickClip = LoadAudioClip("Assets/Audio/UI/UI_Click.wav");
            AudioClip startClip = LoadAudioClip("Assets/Audio/UI/UI_Start.wav");

            startChime = startClip;
            pageSwitchSound = clickClip;

            // 1. Build Home Card
            if (homeCard == null)
            {
                homeCard = CreateCardPage("Card_Home", transform, homeTex);
                CreateButton("Btn_StartHere", homeCard.transform, new Vector2(0f, -258f), new Vector2(360f, 85f), hoverClip, clickClip, "Start", null);
                CreateButton("Btn_Options", homeCard.transform, new Vector2(0f, -345f), new Vector2(300f, 80f), hoverClip, clickClip, "Options", null);
                CreateButton("Btn_Quit", homeCard.transform, new Vector2(0f, -438f), new Vector2(260f, 75f), hoverClip, clickClip, "Quit", null);
            }

            // 2. Build About Card
            if (aboutCard == null)
            {
                aboutCard = CreateCardPage("Card_About", transform, aboutTex);
                CreateButton("Btn_Back", aboutCard.transform, new Vector2(0f, -470f), new Vector2(280f, 70f), hoverClip, clickClip, "Back", "Back to Menu");
                aboutCard.SetActive(false);
            }

            // 3. Build Quit Card
            if (quitCard == null)
            {
                quitCard = CreateCardPage("Card_Quit", transform, quitTex);
                CreateButton("Btn_Yes", quitCard.transform, new Vector2(-268f, -366f), new Vector2(220f, 220f), hoverClip, clickClip, "Yes", null);
                CreateButton("Btn_No", quitCard.transform, new Vector2(218f, -368f), new Vector2(220f, 220f), hoverClip, clickClip, "No", null);
                quitCard.SetActive(false);
            }

            // 4. Setup Raycaster on Controller Anchor
            SetupRaycaster();

            BindButtons();
            PositionCardsInFrontOfHmd();
        }

        private void SetupRaycaster()
        {
            if (rightRaycaster != null) return;

            GameObject rightHandAnchor = GameObject.Find("RightControllerInHandAnchor");
            if (rightHandAnchor == null) rightHandAnchor = GameObject.Find("RightHandAnchor");
            if (rightHandAnchor == null && playerHmd != null) rightHandAnchor = playerHmd.gameObject;

            if (rightHandAnchor != null)
            {
                rightRaycaster = rightHandAnchor.GetComponent<VRUIRaycaster>();
                if (rightRaycaster == null) rightRaycaster = rightHandAnchor.AddComponent<VRUIRaycaster>();
                rightRaycaster.Controller = OVRInput.Controller.RTouch;
            }
        }

        private static GameObject CreateCardPage(string name, Transform parent, Texture2D bgTexture)
        {
            GameObject cardObj = new GameObject(name);
            cardObj.transform.SetParent(parent, false);

            RectTransform rt = cardObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (bgTexture != null)
            {
                RawImage img = cardObj.AddComponent<RawImage>();
                img.texture = bgTexture;
                img.color = Color.white;
                img.raycastTarget = false;
            }

            return cardObj;
        }

        private static VRButton CreateButton(string name, Transform parent, Vector2 pos, Vector2 size, AudioClip hover, AudioClip click, string id, string visibleLabel)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            // Visual animated child
            GameObject visualChild = new GameObject("Visual");
            visualChild.transform.SetParent(btnObj.transform, false);
            RectTransform vRt = visualChild.AddComponent<RectTransform>();
            vRt.anchorMin = Vector2.zero;
            vRt.anchorMax = Vector2.one;
            vRt.offsetMin = Vector2.zero;
            vRt.offsetMax = Vector2.zero;

            if (!string.IsNullOrEmpty(visibleLabel))
            {
                // Visible pill button for Back button
                Image bg = visualChild.AddComponent<Image>();
                bg.color = new Color(0.14f, 0.32f, 0.18f, 0.95f); // Rich forest green

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(visualChild.transform, false);
                RectTransform tRt = textObj.AddComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.offsetMin = Vector2.zero;
                tRt.offsetMax = Vector2.zero;

                Text label = textObj.AddComponent<Text>();
                label.text = visibleLabel;
                label.fontSize = 28;
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            else
            {
                // Subtle transparent highlight overlay for still-image buttons
                Image highlightImg = visualChild.AddComponent<Image>();
                highlightImg.color = new Color(1f, 1f, 1f, 0.04f);
                highlightImg.raycastTarget = false;
            }

            // BoxCollider for laser pointer raycasting (40 units deep = 3cm in world space)
            BoxCollider box = btnObj.AddComponent<BoxCollider>();
            box.size = new Vector3(size.x, size.y, 40f);
            box.isTrigger = true;

            VRButton btn = btnObj.AddComponent<VRButton>();
            btn.ButtonId = id;
            btn.HoverSound = hover;
            btn.ClickSound = click;
            btn.VisualTransform = visualChild.transform;

            return btn;
        }

        private static Texture2D LoadTexture(string path)
        {
#if UNITY_EDITOR
            Texture2D edTex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (edTex != null) return edTex;
#endif
            if (File.Exists(path))
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D t = new Texture2D(2, 2);
                t.LoadImage(bytes);
                return t;
            }
            return null;
        }

        private static AudioClip LoadAudioClip(string path)
        {
#if UNITY_EDITOR
            AudioClip edClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (edClip != null) return edClip;
#endif
            return null;
        }
    }
}
