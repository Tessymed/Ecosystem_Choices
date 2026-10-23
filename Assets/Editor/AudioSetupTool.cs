using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EcosystemChoices.Locomotion;
using EcosystemChoices.Biodiversity;
using System.IO;

namespace EcosystemChoices.Editor
{
    [InitializeOnLoad]
    public static class AudioSetupTool
    {
        private const string FlagFile = "Temp/AudioSetupDone.flag";

        static AudioSetupTool()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(FlagFile))
                {
                    File.WriteAllText(FlagFile, "Done");
                    ConfigureAudio(false);
                }
            };
        }

        [MenuItem("Tools/Audio/Configure Ecosystem Audio")]
        public static void ConfigureAudioMenu()
        {
            ConfigureAudio(true);
        }

        public static void ConfigureAudio(bool showDialog = false)
        {
            Debug.Log("[AudioSetupTool] Starting Ecosystem Audio setup & optimization...");

            // 1. Configure and optimize Audio Importers
            ConfigureAudioImporter("Assets/Audio/Ambience/Ambience_Nature_Serbia.mp3", AudioClipLoadType.Streaming, 0.70f, false);
            ConfigureAudioImporter("Assets/Audio/Locomotion/Footsteps_Gravel_Grass_Continuous.mp3", AudioClipLoadType.CompressedInMemory, 0.80f, false);
            ConfigureAudioImporter("Assets/Audio/Wildlife/Birds_At_4am.mp3", AudioClipLoadType.CompressedInMemory, 0.85f, true); // Force mono for 3D spatialization

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AudioClip ambienceClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Ambience/Ambience_Nature_Serbia.mp3");
            AudioClip footstepClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Locomotion/Footsteps_Gravel_Grass_Continuous.mp3");
            AudioClip birdClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Wildlife/Birds_At_4am.mp3");

            Debug.Log($"[AudioSetupTool] Clips loaded: Ambience={(ambienceClip != null)}, Footsteps={(footstepClip != null)}, Birds={(birdClip != null)}");

            // 2. Configure MovementAudio on Player / OVRCameraRig
            MovementAudio movementAudio = Object.FindAnyObjectByType<MovementAudio>();
            if (movementAudio != null)
            {
                Undo.RecordObject(movementAudio, "Configure Movement Audio");
                SerializedObject so = new SerializedObject(movementAudio);

                SerializedProperty ambProp = so.FindProperty("ambientSoundscape");
                if (ambProp != null && ambienceClip != null) ambProp.objectReferenceValue = ambienceClip;

                SerializedProperty footProp = so.FindProperty("continuousFootstepClip");
                if (footProp != null && footstepClip != null) footProp.objectReferenceValue = footstepClip;

                SerializedProperty birdProp = so.FindProperty("canopyBirdClip");
                if (birdProp != null && birdClip != null) birdProp.objectReferenceValue = birdClip;

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(movementAudio);
                Debug.Log("[AudioSetupTool] MovementAudio successfully configured with Ambience, Continuous Footsteps, and Canopy Birds!");
            }
            else
            {
                Debug.LogWarning("[AudioSetupTool] MovementAudio not found in active scene. Please ensure SampleScene is loaded.");
            }

            // 3. Configure BirdFlockSpawner with 3D sky flyby bird audio
            BirdFlockSpawner birdSpawner = Object.FindAnyObjectByType<BirdFlockSpawner>();
            if (birdSpawner != null && birdClip != null)
            {
                Undo.RecordObject(birdSpawner, "Configure Bird Spawner Audio");
                SerializedObject soBird = new SerializedObject(birdSpawner);
                SerializedProperty flockSoundProp = soBird.FindProperty("flockSound");
                if (flockSoundProp != null) flockSoundProp.objectReferenceValue = birdClip;
                soBird.ApplyModifiedProperties();
                EditorUtility.SetDirty(birdSpawner);
                Debug.Log("[AudioSetupTool] BirdFlockSpawner configured with 3D bird flyby audio clip!");
            }

            // 4. Ensure AudioListener exists on VR Camera (CenterEyeAnchor or Main Camera)
            AudioListener listener = Object.FindAnyObjectByType<AudioListener>();
            if (listener == null)
            {
                Camera targetCam = null;
                GameObject centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null) targetCam = centerEye.GetComponent<Camera>();
                if (targetCam == null) targetCam = Camera.main;

                if (targetCam != null)
                {
                    listener = Undo.AddComponent<AudioListener>(targetCam.gameObject);
                    EditorUtility.SetDirty(targetCam.gameObject);
                    Debug.Log($"[AudioSetupTool] Added AudioListener to {targetCam.gameObject.name} for VR binaural sound.");
                }
            }
            else
            {
                Debug.Log($"[AudioSetupTool] AudioListener already present on {listener.gameObject.name}.");
            }

            // Mark active scene dirty & save
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[AudioSetupTool] Saved scene: {activeScene.name}");
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Ecosystem Audio Configured",
                    "Ecosystem Audio successfully configured!\n\n" +
                    "- Ambience: Ambience_Nature_Serbia (Streaming, Dynamic LFO Breathing Waves)\n" +
                    "- Footsteps: Footsteps_Gravel_Grass_Continuous (Speed/Pitch scaled, Silent when stopped)\n" +
                    "- Wildlife: Birds_At_4am (Spatial 3D Tree Canopy Calls & Sky Flyby Audio)\n" +
                    "- AudioListener: Verified on VR Camera",
                    "OK");
            }
        }

        private static void ConfigureAudioImporter(string path, AudioClipLoadType loadType, float quality, bool forceToMono)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[AudioSetupTool] File not found: {path}");
                return;
            }

            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                bool modified = false;

                if (importer.forceToMono != forceToMono)
                {
                    importer.forceToMono = forceToMono;
                    modified = true;
                }

                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                if (settings.loadType != loadType || settings.compressionFormat != AudioCompressionFormat.Vorbis || Mathf.Abs(settings.quality - quality) > 0.01f)
                {
                    settings.loadType = loadType;
                    settings.compressionFormat = AudioCompressionFormat.Vorbis;
                    settings.quality = quality;
                    importer.defaultSampleSettings = settings;
                    modified = true;
                }

                if (modified)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"[AudioSetupTool] Reimported {Path.GetFileName(path)} (LoadType={loadType}, Quality={quality}, ForceMono={forceToMono})");
                }
            }
        }
    }
}
