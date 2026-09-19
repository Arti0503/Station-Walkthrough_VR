using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
public class VRSetupForQuest
{
    [MenuItem("VR Setup/Configure for Meta Quest")]
    public static void ConfigureForQuest()
    {
        Debug.Log("Starting VR configuration for Meta Quest...");

        // 1. Switch Build Target to Android
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        // 2. Player Settings for Quest
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        // Ensure we don't have auto graphics API if it causes issues, but default is fine in Unity 6 usually.
        // We will leave graphics API to default (Vulkan/GLES3).

        Debug.Log("Build target switched to Android.");
        Debug.Log("Player Settings updated: Color Space -> Linear, Scripting Backend -> IL2CPP, Architecture -> ARM64, Min SDK -> API Level 32.");

        // Instruct user for the next steps
        EditorUtility.DisplayDialog(
            "VR Setup Complete",
            "Project settings for Meta Quest have been configured.\n\n" +
            "Next steps:\n" +
            "1. Go to Edit -> Project Settings -> XR Plug-in Management.\n" +
            "2. Click the Android tab.\n" +
            "3. Check the 'Oculus' or 'OpenXR' plug-in to enable VR support for the headset.\n\n" +
            "Check the Console for more details.",
            "OK"
        );
    }
}
#endif
