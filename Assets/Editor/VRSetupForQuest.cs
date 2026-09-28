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
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("[VR Setup] Switched active build target to Android.");
        }

        // 2. Run Complete VR Setup & Fix
        StationWalkthrough.Editor.CompleteQuestVRSetup.ApplyAllQuestVRSettings(true);
    }
}
#endif
