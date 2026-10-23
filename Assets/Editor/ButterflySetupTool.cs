using UnityEngine;
using UnityEditor;
using EcosystemChoices.Biodiversity;
using System.IO;

namespace EcosystemChoices.Editor
{
    [InitializeOnLoad]
    public static class ButterflySetupTool
    {
        static ButterflySetupTool()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists("Temp/ButterflySetupDone.flag"))
                {
                    File.WriteAllText("Temp/ButterflySetupDone.flag", "Done");
                    SetupButterfly(false);
                }
            };
        }

        [MenuItem("Tools/Biodiversity/Setup Butterfly Prefab")]
        public static void SetupButterflyMenu()
        {
            SetupButterfly(true);
        }

        public static void SetupButterfly(bool showDialog = false)
        {
            Debug.Log("[ButterflySetupTool] Starting Butterfly material and prefab configuration...");

            // 1. Refresh & reimport textures
            string[] texturePaths = new string[]
            {
                "Assets/Prefabs/Materials/Butterfly/T_Butterfly_Wings_AlbedoAlpha.png",
                "Assets/Prefabs/Materials/Butterfly/T_Butterfly_Wings_Normal.jpg",
                "Assets/Prefabs/Materials/Butterfly/body_albedo.png",
                "Assets/Prefabs/Materials/Butterfly/T_Butterfly_Body_Normal.png"
            };

            foreach (var tp in texturePaths)
            {
                if (File.Exists(tp))
                {
                    AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceUpdate);
                }
            }

            Texture2D wingAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Materials/Butterfly/T_Butterfly_Wings_AlbedoAlpha.png");
            Texture2D wingNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Materials/Butterfly/T_Butterfly_Wings_Normal.jpg");
            Texture2D bodyAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Materials/Butterfly/body_albedo.png");
            Texture2D bodyNormal = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Materials/Butterfly/T_Butterfly_Body_Normal.png");

            // 2. Configure Butterfly_Wings material
            Material wingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prefabs/Materials/Butterfly_Wings.mat");
            if (wingMat != null)
            {
                if (wingAlbedo != null) wingMat.SetTexture("_BaseMap", wingAlbedo);
                if (wingNormal != null) wingMat.SetTexture("_BumpMap", wingNormal);
                wingMat.SetColor("_BaseColor", Color.white);
                wingMat.SetFloat("_Cull", 0f); // Two-sided / Double-Sided
                wingMat.SetFloat("_AlphaClip", 1f); // Alpha Cutout
                wingMat.SetFloat("_Cutoff", 0.15f);
                wingMat.SetFloat("_Smoothness", 0.65f);
                wingMat.SetFloat("_BumpScale", 1.0f);
                wingMat.EnableKeyword("_ALPHATEST_ON");
                wingMat.EnableKeyword("_NORMALMAP");
                wingMat.SetOverrideTag("RenderType", "TransparentCutout");
                wingMat.doubleSidedGI = true;
                EditorUtility.SetDirty(wingMat);
                Debug.Log("[ButterflySetupTool] Butterfly_Wings.mat successfully configured as two-sided cutout with textures.");
            }

            // 3. Configure Body materials
            string[] bodyMatNames = new string[]
            {
                "abdomen.mat", "head.mat", "thorax.mat", "antenna.mat", "joint.mat", "eyes.mat", "legs.mat"
            };

            foreach (var bmn in bodyMatNames)
            {
                Material bm = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Prefabs/Materials/{bmn}");
                if (bm != null)
                {
                    if (bodyAlbedo != null) bm.SetTexture("_BaseMap", bodyAlbedo);
                    if (bodyNormal != null) bm.SetTexture("_BumpMap", bodyNormal);
                    bm.SetColor("_BaseColor", Color.white);
                    bm.SetFloat("_Smoothness", 0.4f);
                    bm.EnableKeyword("_NORMALMAP");
                    EditorUtility.SetDirty(bm);
                }
            }
            Debug.Log("[ButterflySetupTool] All body materials configured with high-res albedo & normal maps.");

            // 4. Configure butterfly_free.prefab
            string prefabPath = "Assets/Prefabs/butterfly_free.prefab";
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot != null)
            {
                var controller = prefabRoot.GetComponent<ProceduralButterflyController>();
                if (controller == null)
                {
                    controller = prefabRoot.AddComponent<ProceduralButterflyController>();
                }

                // Locate wing transforms directly in prefab hierarchy
                Transform leftUpper = null, leftLower = null, rightUpper = null, rightLower = null;
                Transform leftTip = null, rightTip = null;

                foreach (var t in prefabRoot.GetComponentsInChildren<Transform>(true))
                {
                    string nl = t.name.ToLower();
                    if (nl.Contains("controller") || nl.Contains("control")) continue;

                    if (nl == "wing_upper.l") leftUpper = t;
                    else if (nl == "wing_lower.l") leftLower = t;
                    else if (nl == "wing_upper.r") rightUpper = t;
                    else if (nl == "wing_lower.r") rightLower = t;
                    else if (nl == "wing_upper2.l") leftTip = t;
                    else if (nl == "wing_upper2.r") rightTip = t;
                }

                SerializedObject so = new SerializedObject(controller);
                if (leftUpper != null) so.FindProperty("leftUpperWing").objectReferenceValue = leftUpper;
                if (leftLower != null) so.FindProperty("leftLowerWing").objectReferenceValue = leftLower;
                if (rightUpper != null) so.FindProperty("rightUpperWing").objectReferenceValue = rightUpper;
                if (rightLower != null) so.FindProperty("rightLowerWing").objectReferenceValue = rightLower;
                if (leftTip != null) so.FindProperty("leftWingTip").objectReferenceValue = leftTip;
                if (rightTip != null) so.FindProperty("rightWingTip").objectReferenceValue = rightTip;

                so.FindProperty("flapFrequency").floatValue = 11.0f;
                so.FindProperty("maxFlapAngle").floatValue = 48.0f;
                so.FindProperty("flightSpeed").floatValue = 1.8f;
                so.FindProperty("turbulenceScale").floatValue = 1.3f;
                so.FindProperty("modelRotationOffset").vector3Value = new Vector3(-90f, 0f, 0f);
                so.FindProperty("flightPitchAngle").floatValue = 6.0f;
                so.FindProperty("meanderRadius").floatValue = 7.0f;
                so.FindProperty("randomizeColor").boolValue = true;

                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                Debug.Log($"[ButterflySetupTool] Configured butterfly_free.prefab with wing pairs: LeftUpper={leftUpper?.name}, LeftLower={leftLower?.name}, RightUpper={rightUpper?.name}, RightLower={rightLower?.name}");
            }

            // 5. Update ButterflySpawner in active scene if open
            var spawner = Object.FindAnyObjectByType<ButterflySpawner>();
            if (spawner != null)
            {
                GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (savedPrefab != null)
                {
                    SerializedObject spawnerSo = new SerializedObject(spawner);
                    SerializedProperty prefabsProp = spawnerSo.FindProperty("butterflyPrefabs");

                    bool inList = false;
                    for (int i = 0; i < prefabsProp.arraySize; i++)
                    {
                        if (prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue == savedPrefab)
                        {
                            inList = true;
                            break;
                        }
                    }

                    if (!inList)
                    {
                        prefabsProp.arraySize++;
                        prefabsProp.GetArrayElementAtIndex(prefabsProp.arraySize - 1).objectReferenceValue = savedPrefab;
                        spawnerSo.ApplyModifiedProperties();
                        EditorUtility.SetDirty(spawner);
                        Debug.Log("[ButterflySetupTool] Assigned butterfly_free.prefab to active ButterflySpawner in scene.");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            File.WriteAllText("Temp/ButterflySetupDone.txt", "Completed at " + System.DateTime.Now);

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Butterfly Setup Complete",
                    "Butterfly materials and wings have been configured successfully!\n\n" +
                    "• Double-Sided Cutout Wings Material (Cull Off)\n" +
                    "• Full High-Res Body & Wing Textures Applied\n" +
                    "• Dual Upper & Lower Wing Pairs Bound to Procedural Flapping\n" +
                    "• Aerodynamic Wing Tip Flexing and Dynamic Color Variation Enabled", "Awesome!");
            }
        }
    }
}
