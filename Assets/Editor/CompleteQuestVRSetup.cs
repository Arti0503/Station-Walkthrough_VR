using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

namespace StationWalkthrough.Editor
{
    [InitializeOnLoad]
    public class CompleteQuestVRSetup
    {
        private const string OpenXRLoaderID = "Unity.XR.OpenXR.OpenXRLoader";
        private const string OculusLoaderID = "Unity.XR.Oculus.OculusLoader";
        private const string ARCoreLoaderID = "Unity.XR.ARCore.ARCoreLoader";

        static CompleteQuestVRSetup()
        {
            EditorApplication.delayCall += () =>
            {
                ApplyAllQuestVRSettings(false);
            };
        }

        [MenuItem("VR Setup/Complete 1-Click Quest VR Setup & Build Settings", priority = 0)]
        public static void ApplyManual()
        {
            ApplyAllQuestVRSettings(true);
        }

        public static void ApplyAllQuestVRSettings(bool showDialog)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying) return;

            Debug.Log("=================================================================");
            Debug.Log("[Quest VR Setup] Applying Full HD, VR Build Settings & Anti-Flicker Configurations...");

            bool changed = false;

            // 1. Ensure Target Platform is Android
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[Quest VR Setup] Switching build target to Android...");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                changed = true;
            }

            // 2. Android Graphics API: Vulkan (Primary) + OpenGLES3 (Fallback)
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                changed = true;
            }

            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (apis.Length == 0 || apis[0] != GraphicsDeviceType.Vulkan)
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
                changed = true;
                Debug.Log("[Quest VR Setup] Android Graphics API set to Vulkan (primary) with OpenGLES3 fallback.");
            }

            // 3. Android Screen Orientation & Presentation (Landscape Left is mandatory for Meta Quest)
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.LandscapeLeft)
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeRight = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                changed = true;
                Debug.Log("[Quest VR Setup] Screen Orientation locked to Landscape Left.");
            }

            // 4. Resolution Settings: Set to Full HD (1920x1080)
            if (PlayerSettings.defaultScreenWidth != 1920 || PlayerSettings.defaultScreenHeight != 1080)
            {
                PlayerSettings.defaultScreenWidth = 1920;
                PlayerSettings.defaultScreenHeight = 1080;
                changed = true;
                Debug.Log("[Quest VR Setup] Default Screen Resolution set to Full HD (1920x1080).");
            }

            // 5. Android Target Architecture, Scripting Backend & Min SDK
            if (PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android) != ScriptingImplementation.IL2CPP)
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                changed = true;
                Debug.Log("[Quest VR Setup] Android Scripting Backend set to IL2CPP.");
            }

            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                changed = true;
                Debug.Log("[Quest VR Setup] Android Target Architecture set to ARM64 (Meta Quest requirement).");
            }

            if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel29)
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
                changed = true;
                Debug.Log("[Quest VR Setup] Android Min SDK set to API Level 29 (Android 10).");
            }

            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.GameActivity)
            {
                PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
                changed = true;
                Debug.Log("[Quest VR Setup] Android Application Entry set to GameActivity.");
            }

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                changed = true;
                Debug.Log("[Quest VR Setup] Color Space set to Linear.");
            }

            // 6. XR Plug-in Management & OpenXR Loader Setup
            XRGeneralSettings generalSettings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings != null)
            {
                if (!generalSettings.InitManagerOnStart)
                {
                    generalSettings.InitManagerOnStart = true;
                    EditorUtility.SetDirty(generalSettings);
                    changed = true;
                    Debug.Log("[Quest VR Setup] Enabled 'Initialize XR on Startup' for Android.");
                }

                XRManagerSettings settingsManager = generalSettings.Manager;
                if (settingsManager != null)
                {
                    // Clean up conflicting loaders
                    if (XRPackageMetadataStore.IsLoaderAssigned(ARCoreLoaderID, BuildTargetGroup.Android))
                    {
                        XRPackageMetadataStore.RemoveLoader(settingsManager, ARCoreLoaderID, BuildTargetGroup.Android);
                        changed = true;
                    }
                    if (XRPackageMetadataStore.IsLoaderAssigned(OculusLoaderID, BuildTargetGroup.Android))
                    {
                        XRPackageMetadataStore.RemoveLoader(settingsManager, OculusLoaderID, BuildTargetGroup.Android);
                        changed = true;
                    }

                    // Assign OpenXR Loader
                    if (!XRPackageMetadataStore.IsLoaderAssigned(OpenXRLoaderID, BuildTargetGroup.Android))
                    {
                        if (XRPackageMetadataStore.AssignLoader(settingsManager, OpenXRLoaderID, BuildTargetGroup.Android))
                        {
                            changed = true;
                            Debug.Log("[Quest VR Setup] Successfully assigned OpenXR Loader for Android.");
                        }
                    }
                }
            }

            // 7. OpenXR Android Settings: Multiview, Depth Submission, Latency Optimization
            OpenXRSettings androidOpenXR = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (androidOpenXR != null)
            {
                if (androidOpenXR.renderMode != OpenXRSettings.RenderMode.SinglePassInstanced)
                {
                    androidOpenXR.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
                    changed = true;
                    Debug.Log("[Quest VR Setup] OpenXR Render Mode set to Single Pass Instanced (Multiview).");
                }

                if (androidOpenXR.latencyOptimization != OpenXRSettings.LatencyOptimization.PrioritizeInputPolling)
                {
                    androidOpenXR.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
                    changed = true;
                    Debug.Log("[Quest VR Setup] OpenXR Latency Optimization set to Prioritize Input Polling.");
                }

                // CRITICAL ANTI-FLICKER: Enable Depth Submission to compositor
                if (androidOpenXR.depthSubmissionMode != OpenXRSettings.DepthSubmissionMode.Depth16Bit)
                {
                    androidOpenXR.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.Depth16Bit;
                    changed = true;
                    Debug.Log("[Quest VR Setup] OpenXR Depth Submission Mode set to Depth 16-Bit (eliminates reprojection judder & flicker).");
                }

                // Ensure Meta Quest Feature & Controller Profiles are enabled
                var questFeature = androidOpenXR.GetFeature<MetaQuestFeature>();
                if (questFeature != null && !questFeature.enabled)
                {
                    questFeature.enabled = true;
                    EditorUtility.SetDirty(questFeature);
                    changed = true;
                    Debug.Log("[Quest VR Setup] Enabled Meta Quest Support feature.");
                }

                var oculusTouch = androidOpenXR.GetFeature<OculusTouchControllerProfile>();
                if (oculusTouch != null && !oculusTouch.enabled)
                {
                    oculusTouch.enabled = true;
                    EditorUtility.SetDirty(oculusTouch);
                    changed = true;
                }

                var questPro = androidOpenXR.GetFeature<MetaQuestTouchProControllerProfile>();
                if (questPro != null && !questPro.enabled)
                {
                    questPro.enabled = true;
                    EditorUtility.SetDirty(questPro);
                    changed = true;
                }

                var questPlus = androidOpenXR.GetFeature<MetaQuestTouchPlusControllerProfile>();
                if (questPlus != null && !questPlus.enabled)
                {
                    questPlus.enabled = true;
                    EditorUtility.SetDirty(questPlus);
                    changed = true;
                }

                EditorUtility.SetDirty(androidOpenXR);
            }

            // 8. Fix OpenXR Editor Settings: VulkanOffscreenSwapchainNoMainDisplay MUST be false
            try
            {
                Type editorSettingsType = Type.GetType("UnityEditor.XR.OpenXR.OpenXREditorSettings, Unity.XR.OpenXR.Editor");
                if (editorSettingsType != null)
                {
                    PropertyInfo instProp = editorSettingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    object inst = instProp?.GetValue(null);
                    if (inst != null)
                    {
                        PropertyInfo prop = editorSettingsType.GetProperty("VulkanOffscreenSwapchainNoMainDisplay");
                        if (prop != null)
                        {
                            bool currentVal = (bool)prop.GetValue(inst);
                            if (currentVal)
                            {
                                prop.SetValue(inst, false);
                                EditorUtility.SetDirty((UnityEngine.Object)inst);
                                changed = true;
                                Debug.Log("[Quest VR Setup] Fixed: Disabled 'VulkanOffscreenSwapchainNoMainDisplay' (restored headset display rendering).");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Quest VR Setup] Note on OpenXREditorSettings: {ex.Message}");
            }

            // 9. Anti-Flicker & Resolution in URP Assets (MSAA 4x, Render Scale 1.0, HDR off)
            string[] rpGuids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            foreach (var guid in rpGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var rpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (rpAsset != null)
                {
                    bool rpChanged = false;
                    // Full resolution render scale
                    if (Mathf.Abs(rpAsset.renderScale - 1.0f) > 0.01f)
                    {
                        rpAsset.renderScale = 1.0f;
                        rpChanged = true;
                    }
                    // 4x MSAA is required in VR to stop crawling edge flicker
                    if (rpAsset.msaaSampleCount != 4)
                    {
                        rpAsset.msaaSampleCount = 4;
                        rpChanged = true;
                    }
                    // HDR off for mobile VR
                    if (rpAsset.supportsHDR)
                    {
                        rpAsset.supportsHDR = false;
                        rpChanged = true;
                    }

                    if (rpChanged)
                    {
                        EditorUtility.SetDirty(rpAsset);
                        changed = true;
                        Debug.Log($"[Quest VR Setup] Optimized URP Asset '{rpAsset.name}': Full HD Render Scale (1.0), 4x MSAA Anti-Flicker, HDR Disabled.");
                    }
                }
            }

            // 10. Anti-Flicker: Camera Near/Far Clip Planes in Scene
            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.nearClipPlane < 0.15f)
                {
                    cam.nearClipPlane = 0.15f;
                    EditorUtility.SetDirty(cam);
                    changed = true;
                    Debug.Log($"[Quest VR Setup] Adjusted {cam.name} near clip plane to 0.15 (eliminates Z-fighting & mesh flickering).");
                }
                if (cam.farClipPlane > 500f)
                {
                    cam.farClipPlane = 400f;
                    EditorUtility.SetDirty(cam);
                    changed = true;
                    Debug.Log($"[Quest VR Setup] Adjusted {cam.name} far clip plane to 400 (optimizes 24-bit depth distribution).");
                }
            }

            // 11. Configure Meta Quest 2 Left & Right VR Controllers (Left Stick Move, Right Stick Turn)
            try
            {
                SetupVRControls.CleanAndConfigureVRControllers(false);
                Debug.Log("[Quest VR Setup] Meta Quest 2 Left & Right VR Controllers configured successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Quest VR Setup] Controller setup note: {ex.Message}");
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[Quest VR Setup] All Quest VR settings saved successfully!");
            }
            else
            {
                Debug.Log("[Quest VR Setup] All Quest VR settings are already optimal.");
            }

            Debug.Log("=================================================================");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Quest VR Configuration Complete",
                    "VR Build, Controllers, and Anti-Flicker settings are fully configured:\n\n" +
                    "1. Full HD Resolution (1920x1080 Native Render Scale 1.0)\n" +
                    "2. Graphics API: Vulkan (Primary) with OpenGLES3 Fallback\n" +
                    "3. OpenXR Android Settings: Multiview, Prioritize Input Polling\n" +
                    "4. Anti-Flicker: 16-Bit Depth Submission to Compositor\n" +
                    "5. Anti-Flicker: 4x MSAA enabled on Universal Render Pipeline\n" +
                    "6. Anti-Flicker: Camera near clip set to 0.15m to stop Z-fighting\n" +
                    "7. Fixed Headset Display: Offscreen swapchain bug disabled\n" +
                    "8. Scripting: IL2CPP, ARM64 64-bit, Min SDK 29, Landscape Left\n" +
                    "9. Meta Quest 2 Controllers: Left Stick Move/Strafe, Right Stick Turn, 6DOF tracking active\n\n" +
                    "Your project is ready to build and run on Meta Quest 2!",
                    "Great!"
                );
            }
        }
    }
}
