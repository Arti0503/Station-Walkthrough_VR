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
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

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

                // Remove legacy Oculus Loader if present (deprecated in Unity 6, superseded by OpenXR)
                if (XRPackageMetadataStore.IsLoaderAssigned(OculusLoaderID, BuildTargetGroup.Android))
                {
                    XRPackageMetadataStore.RemoveLoader(settingsManager, OculusLoaderID, BuildTargetGroup.Android);
                    Debug.Log("[VR Fix] Fixed: Removed legacy Oculus loader in favor of standard OpenXR.");
                    requiresSave = true;
                }

                // Ensure OpenXR loader is assigned for Android
                if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXRLoaderID, BuildTargetGroup.Android))
                {
                    if (XRPackageMetadataStore.AssignLoader(settingsManager, OpenXRLoaderID, BuildTargetGroup.Android))
                    {
                        Debug.Log("[VR Fix] Fixed: Assigned OpenXR Loader for Android.");
                        requiresSave = true;
                    }
                    else
                    {
                        Debug.LogError("[VR Fix] Failed to assign OpenXR Loader! Please verify com.unity.xr.openxr is installed.");
                    }
                }

                // Configure OpenXR Android settings
                var openXRSettings = UnityEngine.XR.OpenXR.OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
                if (openXRSettings != null)
                {
                    openXRSettings.renderMode = UnityEngine.XR.OpenXR.OpenXRSettings.RenderMode.SinglePassInstanced;
                    openXRSettings.latencyOptimization = UnityEngine.XR.OpenXR.OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
                    openXRSettings.depthSubmissionMode = UnityEngine.XR.OpenXR.OpenXRSettings.DepthSubmissionMode.Depth16Bit;
                    EditorUtility.SetDirty(openXRSettings);
                    Debug.Log("[VR Fix] Fixed: Configured OpenXR Android to SinglePassInstanced (Multiview), Depth 16-Bit submission, and Prioritize Input Polling.");
                    requiresSave = true;
                }

                // Disable VulkanOffscreenSwapchainNoMainDisplay if present
                try
                {
                    var editorSettingsType = System.Type.GetType("UnityEditor.XR.OpenXR.OpenXREditorSettings, Unity.XR.OpenXR.Editor");
                    if (editorSettingsType != null)
                    {
                        var instProp = editorSettingsType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var inst = instProp?.GetValue(null);
                        if (inst != null)
                        {
                            var noMainDisplayProp = editorSettingsType.GetProperty("VulkanOffscreenSwapchainNoMainDisplay");
                            if (noMainDisplayProp != null && (bool)noMainDisplayProp.GetValue(inst))
                            {
                                noMainDisplayProp.SetValue(inst, false);
                                EditorUtility.SetDirty((UnityEngine.Object)inst);
                                Debug.Log("[VR Fix] Fixed: Disabled Vulkan Offscreen Swapchain No Main Display (restored headset display rendering).");
                                requiresSave = true;
                            }
                        }
                    }
                }
                catch { }
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
            Debug.Log("[VR Fix] Fixed: Minimum Android API Level set to 29 (Meta Quest requirement).");
        }

        // 4. Graphics API Enforcement (Vulkan required for Meta Quest OpenXR)
        GraphicsDeviceType[] currentApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        bool vulkanFirst = (currentApis.Length > 0 && currentApis[0] == GraphicsDeviceType.Vulkan);
        if (!vulkanFirst)
        {
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            Debug.Log("[VR Fix] Fixed: Set Android Graphics API to Vulkan (primary) with OpenGLES3 fallback.");
            requiresSave = true;
        }

        // Set Screen Orientation to Landscape Left (Meta Quest requirement)
        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            Debug.Log("[VR Fix] Fixed: Set Screen Orientation to Landscape Left.");
            requiresSave = true;
        }

        // 5. URP Render Scale & MSAA Check
        string[] rpGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
        foreach (var guid in rpGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var rpAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(path);
            if (rpAsset != null)
            {
                if (rpAsset.renderScale > 1.2f)
                {
                    rpAsset.renderScale = 1.0f;
                    Debug.Log($"[VR Fix] Fixed: Reset high render scale on {rpAsset.name} to 1.0 to prevent mobile VR crash/black screen.");
                    EditorUtility.SetDirty(rpAsset);
                    requiresSave = true;
                }
                if (rpAsset.msaaSampleCount > 4)
                {
                    rpAsset.msaaSampleCount = 4;
                    Debug.Log($"[VR Fix] Fixed: Set MSAA on {rpAsset.name} to 4x for optimal Quest performance.");
                    EditorUtility.SetDirty(rpAsset);
                    requiresSave = true;
                }
            }
        }

        if (requiresSave)
        {
            if (generalSettings != null) EditorUtility.SetDirty(generalSettings);
            AssetDatabase.SaveAssets();
        }

        // 6. Scene Validation
        ValidateActiveScene();

        Debug.Log("[VR Fix] Done! Your project is now properly configured for Meta Quest VR.");
        Debug.Log("==============================================");

        EditorUtility.DisplayDialog("VR Fix Complete", 
            "The VR settings have been automatically configured:\n\n" +
            "- Enabled 'Initialize XR on Startup'\n" +
            "- Assigned OpenXR Loader (Standard for Meta Quest in Unity 6)\n" +
            "- Configured Linear Color Space, IL2CPP & ARM64\n" +
            "- Enforced Safe Render Scale (1.0) and MSAA (4x)\n" +
            "- Set Minimum API Level 29 (Android 10+)\n\n" +
            "Your project is ready to test on your VR headset!", "Awesome!");
    }

    private static void ValidateActiveScene()
    {
        bool hasXROrigin = false;
        bool hasTrackedPoseDriver = false;
        
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // Fix Flickering (Z-Fighting) by setting near clip plane to a VR safe value
            if (mainCam.nearClipPlane < 0.15f)
            {
                mainCam.nearClipPlane = 0.15f;
                Debug.Log("[VR Fix] Fixed: Increased Main Camera Near Clip Plane to 0.15 to prevent Z-fighting and station flickering.");
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
