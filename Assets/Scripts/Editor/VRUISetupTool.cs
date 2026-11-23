using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using EcosystemChoices.UI;

[InitializeOnLoad]
public static class VRUISetupTool
{
    static VRUISetupTool()
    {
        EditorApplication.delayCall += AutoRunIfMissing;
    }

    private static void AutoRunIfMissing()
    {
        if (Application.isPlaying) return;
        if (GameObject.Find("VR_UI_MenuSystem") == null)
        {
            SetupVRUISystem();
        }
    }

    [MenuItem("Tools/UI/Setup VR UI System")]
    public static void SetupVRUISystem()
    {
        // 1. Locate or create VRMenuManager Root
        GameObject menuRoot = GameObject.Find("VR_UI_MenuSystem");
        if (menuRoot != null)
        {
            Undo.DestroyObjectImmediate(menuRoot);
        }

        menuRoot = new GameObject("VR_UI_MenuSystem");
        Undo.RegisterCreatedObjectUndo(menuRoot, "Create VR UI System");

        VRMenuManager menuManager = menuRoot.AddComponent<VRMenuManager>();
        menuManager.BuildRuntimeMenu();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[VRUISetupTool] VR UI System successfully generated in the scene!");
    }
}
