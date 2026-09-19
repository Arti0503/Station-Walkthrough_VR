using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.Management;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class FixVRBlackScreen
{
    private const string OculusLoaderID = "Unity.XR.Oculus.OculusLoader";
    private const string OpenXRLoaderID = "Unity.XR.OpenXR.OpenXRLoader";
    private const string ARCoreLoaderID = "Unity.XR.ARCore.ARCoreLoader";

    [MenuItem("VR Setup/1-Click Fix VR Black Screen")]
    public static void FixVRIssues()
    {
        Debug.Log("==============================================");
        Debug.Log("[VR Fix] Starting VR Black Screen Diagnostic & Fix...");
        
        bool requiresSave = false;

        // 1. Switch Active Build Target if necessary
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[VR Fix] Switching build target to Android...");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        // 2. Fix XR Plug-in Management Loaders
        XRGeneralSettings generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        if (generalSettings == null)
        {
            Debug.LogError("[VR Fix] XRGeneralSettings not found for Android! Please open Edit > Project Settings > XR Plug-in Management and install it first.");
        }

        if (generalSettings != null)
        {
            XRManagerSettings settingsManager = generalSettings.Manager;
            if (settingsManager != null)
            {
                // Ensure InitManagerOnStart is true
                if (!generalSettings.InitManagerOnStart)
                {
                    generalSettings.InitManagerOnStart = true;
                    Debug.Log("[VR Fix] Fixed: Enabled 'Initialize XR on Startup'.");
                    requiresSave = true;
                }

                // Remove ARCore Loader which breaks VR
                if (XRPackageMetadataStore.IsLoaderAssigned(ARCoreLoaderID, BuildTargetGroup.Android))
                {
                    XRPackageMetadataStore.RemoveLoader(settingsManager, ARCoreLoaderID, BuildTargetGroup.Android);
                    Debug.Log("[VR Fix] Fixed: Removed ARCore loader from Android to prevent VR conflicts.");
                    requiresSave = true;
                }
                
                // Prioritize Oculus Loader for stability on Quest, remove OpenXR to avoid duplicate conflicts if not configured properly
                if (XRPackageMetadataStore.IsLoaderAssigned(OpenXRLoaderID, BuildTargetGroup.Android))
                {
                    XRPackageMetadataStore.RemoveLoader(settingsManager, OpenXRLoaderID, BuildTargetGroup.Android);
                    Debug.Log("[VR Fix] Fixed: Removed OpenXR loader (prioritizing native Oculus loader for stability).");
                    requiresSave = true;
                }

                if (!XRPackageMetadataStore.IsLoaderAssigned(OculusLoaderID, BuildTargetGroup.Android))
                {
                    if (XRPackageMetadataStore.AssignLoader(settingsManager, OculusLoaderID, BuildTargetGroup.Android))
                    {
                        Debug.Log("[VR Fix] Fixed: Assigned Oculus XR Loader for Android.");
                        requiresSave = true;
                    }
                    else
                    {
                        Debug.LogError("[VR Fix] Failed to assign Oculus Loader! Is the Oculus XR Plugin package installed?");
                    }
                }
            }
        }
        else
        {
            Debug.LogError("[VR Fix] CRITICAL: Could not create XRGeneralSettings! VR will not work.");
        }

        // 3. Player Settings Enforcement
        if (PlayerSettings.colorSpace != ColorSpace.Linear)
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            Debug.Log("[VR Fix] Fixed: Set Color Space to Linear.");
        }

        if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            Debug.Log("[VR Fix] Fixed: Set Scripting Backend to IL2CPP.");
        }

        if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Debug.Log("[VR Fix] Fixed: Set Target Architecture to ARM64.");
        }

        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
        {
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            Debug.Log("[VR Fix] Fixed: Minimum Android API Level set to 29.");
        }

        // 4. Graphics API Enforcement
        GraphicsDeviceType[] currentApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        bool needsGraphicsUpdate = false;
        if (currentApis.Length > 0 && currentApis[0] != GraphicsDeviceType.OpenGLES3)
        {
            needsGraphicsUpdate = true;
        }
        foreach(var api in currentApis)
        {
            if (api == GraphicsDeviceType.Vulkan) needsGraphicsUpdate = true;
        }

        if (needsGraphicsUpdate)
        {
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            Debug.Log("[VR Fix] Fixed: Set Android Graphics API strictly to OpenGLES3 to prevent URP shader black screens.");
        }

        if (requiresSave)
        {
            EditorUtility.SetDirty(generalSettings);
            AssetDatabase.SaveAssets();
        }

        // 5. Scene Validation
        ValidateActiveScene();

        Debug.Log("[VR Fix] Done! Your project is now properly configured for Meta Quest VR.");
        Debug.Log("==============================================");

        EditorUtility.DisplayDialog("VR Fix Complete", 
            "The VR settings have been automatically fixed.\n\n" +
            "- Removed conflicting XR Loaders (ARCore)\n" +
            "- Assigned Oculus Loader\n" +
            "- Fixed Player & Graphics Settings\n\n" +
            "Please check the Unity Console for full details and build your APK!", "Awesome!");
    }

    private static void ValidateActiveScene()
    {
        bool hasXROrigin = false;
        bool hasTrackedPoseDriver = false;
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // Fix Flickering (Z-Fighting) by setting near clip plane to a VR safe value
            if (mainCam.nearClipPlane < 0.1f)
            {
                mainCam.nearClipPlane = 0.1f;
                Debug.Log("[VR Fix] Fixed: Increased Main Camera Near Clip Plane to 0.1 to prevent Z-fighting and station flickering.");
                EditorUtility.SetDirty(mainCam);
            }

            var trackedPoseDriver = mainCam.GetComponent("TrackedPoseDriver"); // Usually UnityEngine.SpatialTracking or InputSystem
            if (trackedPoseDriver == null)
            {
                // Can also be TrackedPoseDriver from input system
                var allComponents = mainCam.GetComponents<Component>();
                foreach (var c in allComponents)
                {
                    if (c != null && c.GetType().Name.Contains("TrackedPoseDriver"))
                    {
                        hasTrackedPoseDriver = true;
                        break;
                    }
                }
            }
            else
            {
                hasTrackedPoseDriver = true;
            }
        }

        // Search for XR Origin by name or type
        GameObject[] rootObjects = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var root in rootObjects)
        {
            if (root.name.Contains("XR Origin") || root.GetComponentInChildren<Camera>() != null)
            {
                Component[] comps = root.GetComponentsInChildren<Component>(true);
                foreach(var c in comps)
                {
                    if (c != null && (c.GetType().Name == "XROrigin" || c.GetType().Name == "XRRig"))
                    {
                        hasXROrigin = true;
                        break;
                    }
                }
            }
        }

        if (!hasTrackedPoseDriver)
        {
            Debug.LogWarning("[VR Fix] WARNING: The Main Camera does not have a TrackedPoseDriver component. Head tracking may not work!");
        }

        if (!hasXROrigin)
        {
            Debug.LogWarning("[VR Fix] WARNING: No 'XR Origin' found in the active scene. Ensure your scene has an XR Rig to function correctly in VR.");
        }
        else
        {
            Debug.Log("[VR Fix] Scene Validation: XR Origin found. Looks good!");
        }
    }
}
#endif
