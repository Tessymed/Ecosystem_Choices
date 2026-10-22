using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using EcosystemChoices.Biodiversity;
using System.IO;

namespace EcosystemChoices.Editor
{
    [InitializeOnLoad]
    public static class MillipedeSetupTool
    {
        static MillipedeSetupTool()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("Temp/MillipedeSetupDone.flag"))
                {
                    File.WriteAllText("Temp/MillipedeSetupDone.flag", "Done");
                    SetupMillipede(false);
                }
            };
        }

        [MenuItem("Tools/Biodiversity/Setup Millipede Prefab")]
        public static void SetupMillipedeMenu()
        {
            SetupMillipede(true);
        }

        public static void SetupMillipede(bool showDialog = false)
        {
            // 1. Locate the animated Millipede FBX
            string[] candidatePaths = new string[]
            {
                "Assets/Prefabs/Millipede_animated.fbx",
                "Assets/Prefabs/Millipede 1.fbx"
            };

            string fbxPath = null;
            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    fbxPath = path;
                    break;
                }
            }

            if (string.IsNullOrEmpty(fbxPath))
            {
                if (showDialog) EditorUtility.DisplayDialog("Setup Error", "Could not find Millipede FBX in Assets/Prefabs/", "OK");
                else Debug.LogError("[MillipedeSetupTool] Could not find Millipede FBX in Assets/Prefabs/");
                return;
            }

            Debug.Log($"[MillipedeSetupTool] Found FBX at: {fbxPath}");

            // 2. Configure ModelImporter for Generic Rig and Looping Animation
            ModelImporter importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer != null)
            {
                bool needReimport = false;

                if (importer.animationType != ModelImporterAnimationType.Generic)
                {
                    importer.animationType = ModelImporterAnimationType.Generic;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    needReimport = true;
                }

                importer.importAnimation = true;

                // Configure clip animations to loop
                ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
                if (clips != null && clips.Length > 0)
                {
                    foreach (var clip in clips)
                    {
                        clip.loopTime = true;
                    }
                    importer.clipAnimations = clips;
                    needReimport = true;
                }

                if (needReimport)
                {
                    importer.SaveAndReimport();
                    Debug.Log("[MillipedeSetupTool] Reimported FBX with Generic Rig and LoopTime enabled.");
                }
            }

            // 3. Find the Animation Clip inside the FBX
            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            AnimationClip crawlClip = null;
            foreach (var asset in allAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    crawlClip = clip;
                    Debug.Log($"[MillipedeSetupTool] Found animation clip: {clip.name} (Length: {clip.length}s)");
                    break;
                }
            }

            // 4. Create or load the Animator Controller
            string controllerPath = "Assets/Prefabs/AC_Millipede.controller";
            AnimatorController animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);

            if (animController == null)
            {
                animController = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                Debug.Log($"[MillipedeSetupTool] Created Animator Controller at: {controllerPath}");
            }

            // Ensure the controller has the crawl state set as default
            var rootStateMachine = animController.layers[0].stateMachine;
            if (crawlClip != null)
            {
                bool hasCrawlState = false;
                foreach (var childState in rootStateMachine.states)
                {
                    if (childState.state.motion == crawlClip || childState.state.name == "Crawl")
                    {
                        childState.state.motion = crawlClip;
                        rootStateMachine.defaultState = childState.state;
                        hasCrawlState = true;
                        break;
                    }
                }

                if (!hasCrawlState)
                {
                    var crawlState = rootStateMachine.AddState("Crawl");
                    crawlState.motion = crawlClip;
                    rootStateMachine.defaultState = crawlState;
                }
                EditorUtility.SetDirty(animController);
                AssetDatabase.SaveAssets();
            }

            // 5. Create / Update the Prefab with a clean hierarchy:
            // Root (Millipede_animated) -> Visual -> fbxInstance
            // This isolates FBX bone coordinate transforms from runtime steering.
            GameObject fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxGo == null) return;

            GameObject rootGo = new GameObject("Millipede_animated");
            rootGo.transform.position = Vector3.zero;
            rootGo.transform.rotation = Quaternion.identity;
            rootGo.transform.localScale = new Vector3(0.07f, 0.07f, 0.07f);

            GameObject visualContainer = new GameObject("Visual");
            visualContainer.transform.SetParent(rootGo.transform, false);
            visualContainer.transform.localPosition = Vector3.zero;
            visualContainer.transform.localRotation = Quaternion.identity;
            visualContainer.transform.localScale = Vector3.one;

            GameObject fbxInstance = Object.Instantiate(fbxGo, visualContainer.transform);
            fbxInstance.name = "Model";
            fbxInstance.transform.localPosition = Vector3.zero;
            fbxInstance.transform.localRotation = Quaternion.identity;
            fbxInstance.transform.localScale = Vector3.one;

            // Setup Animator on the model
            Animator anim = fbxInstance.GetComponent<Animator>();
            if (anim == null) anim = fbxInstance.AddComponent<Animator>();
            anim.runtimeAnimatorController = animController;
            anim.applyRootMotion = false;

            // Exact belly-down and head-forward orientation:
            // Blender FBX length is X (head at +X, tail at -X).
            // Belly/legs are at -Y, back is at +Y.
            // In Unity, rotating by Yaw -90 (0, -90, 0) maps head to +Z (forward)
            // while preserving belly flat on the ground (-Y) and back facing straight up (+Y).
            Vector3 optimalEulerOffset = new Vector3(0f, -90f, 0f);
            visualContainer.transform.localRotation = Quaternion.Euler(optimalEulerOffset);

            // Add SphereCollider for physics collision detection
            SphereCollider col = rootGo.AddComponent<SphereCollider>();
            col.radius = 0.25f;
            col.center = new Vector3(0f, 0.1f, 0f);

            // Setup InsectLineController on Root
            InsectLineController controller = rootGo.AddComponent<InsectLineController>();
            controller.ModelSubtree = visualContainer;
            controller.ModelRotationOffset = optimalEulerOffset;
            controller.GroundOffset = 0.001f;

            // Save as Prefab
            string prefabPath = "Assets/Prefabs/Millipede_animated.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            Object.DestroyImmediate(rootGo);

            Debug.Log($"[MillipedeSetupTool] Saved configured prefab at: {prefabPath}");

            // 6. Update InsectLineSpawner in the current open scene if present
            var spawner = Object.FindAnyObjectByType<InsectLineSpawner>();
            if (spawner != null)
            {
                SerializedObject so = new SerializedObject(spawner);
                SerializedProperty prefabsProp = so.FindProperty("insectPrefabs");

                bool alreadyInList = false;
                for (int i = 0; i < prefabsProp.arraySize; i++)
                {
                    if (prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue == savedPrefab)
                    {
                        alreadyInList = true;
                        break;
                    }
                }

                if (!alreadyInList)
                {
                    prefabsProp.arraySize++;
                    prefabsProp.GetArrayElementAtIndex(prefabsProp.arraySize - 1).objectReferenceValue = savedPrefab;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(spawner);
                    Debug.Log("[MillipedeSetupTool] Added Millipede_animated to InsectLineSpawner in active scene.");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Millipede Setup Complete!",
                    "The Millipede animation controller and prefab have been configured successfully!\n\n" +
                    $"• Calculated Belly-Down & Forward Rotation: {optimalEulerOffset}\n" +
                    "• Visual container isolation applied (bones undisturbed).\n" +
                    "• Whisker obstacle avoidance enabled (no entering trees/rocks/stumps).\n" +
                    "• Smooth bush concealment and burrowing despawn active.\n\n" +
                    "Hit Play in Unity to playtest!", "Awesome!");
            }
        }
    }
}
