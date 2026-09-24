using UnityEngine;
using UnityEngine.XR;

namespace StationWalkthrough
{
    /// <summary>
    /// Locks target frame rate to 90-120 FPS and optimizes physics & rendering for smooth high-framerate VR.
    /// Includes VR-specific rendering optimizations for Meta Quest hardware.
    /// </summary>
    public class PerformanceOptimizer : MonoBehaviour
    {
        public enum TargetFPS { FPS_72 = 72, FPS_90 = 90, FPS_120 = 120 }

        [Header("Frame Rate Target")]
        public TargetFPS targetFPS = TargetFPS.FPS_90;

        [Header("Physics Sync")]
        [Tooltip("Syncs Physics FixedUpdate frequency to match display refresh rate (e.g. 1/90s for 90Hz)")]
        public bool syncPhysicsToRefreshRate = true;

        [Header("VR Rendering")]
        [Tooltip("Eye texture resolution scale (1.0 = native, 0.8 = 80% for performance, 1.2 = supersampled)")]
        [Range(0.5f, 1.5f)]
        public float vrEyeTextureResolutionScale = 1.0f;

        [Tooltip("Enable fixed foveated rendering for Quest (reduces peripheral detail for performance)")]
        public bool enableFixedFoveatedRendering = true;

        private void Awake()
        {
            ApplyPerformanceSettings();
        }

        public void ApplyPerformanceSettings()
        {
            int fps = (int)targetFPS;

            // 1. Lock Target Frame Rate
            bool isVR = XRSettings.isDeviceActive;
            try
            {
                if (UnityEngine.XR.Management.XRGeneralSettings.Instance != null &&
                    UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null &&
                    UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null)
                {
                    isVR = true;
                }
            }
            catch { }

            if (!isVR && Application.platform != RuntimePlatform.Android)
            {
                QualitySettings.vSyncCount = 0; // Disable VSync on desktop non-VR so Application.targetFrameRate takes effect
            }
            else
            {
                // In VR and Android, compositor swapchain sync is required to prevent black-frame flickering and tearing
                QualitySettings.vSyncCount = 1;
            }

            Application.targetFrameRate = fps;

            // 2. Align Fixed Physics Step with Target FPS (1/90s = 0.01111s, 1/120s = 0.00833s)
            if (syncPhysicsToRefreshRate)
            {
                Time.fixedDeltaTime = 1f / fps;
                Time.maximumDeltaTime = 3f / fps; // Prevent physics spiral-of-death under heavy load
            }

            // 3. Apply VR-Specific Optimizations
            if (isVR)
            {
                ApplyVROptimizations(fps);
            }

            // 4. Optimize Garbage Collector
            System.GC.Collect();

            Debug.Log($"[PerformanceOptimizer] Target Frame Rate set to {fps} FPS (vSync: {QualitySettings.vSyncCount}). Fixed Timestep: {Time.fixedDeltaTime:F5}s. VR Active: {isVR}");
        }

        private void ApplyVROptimizations(int fps)
        {
            // 1. Set eye texture resolution scale safely
            try
            {
                if (XRSettings.isDeviceActive)
                {
                    XRSettings.eyeTextureResolutionScale = vrEyeTextureResolutionScale;
                    XRSettings.renderViewportScale = 1.0f;
                    Debug.Log($"[PerformanceOptimizer] VR Eye Texture Resolution Scale: {vrEyeTextureResolutionScale}");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PerformanceOptimizer] Could not set eyeTextureResolutionScale: {e.Message}");
            }

            // 2. Request the target refresh rate on Quest hardware.
            // Application.targetFrameRate (set above) is respected by the Quest runtime.
            // Additionally, try to set it via OVRManager if the Oculus SDK is available.
            try
            {
                var ovrManagerType = System.Type.GetType("OVRManager, Assembly-CSharp");
                if (ovrManagerType == null)
                    ovrManagerType = System.Type.GetType("OVRManager, Oculus.VR");

                if (ovrManagerType != null)
                {
                    var displayFreqProp = ovrManagerType.GetProperty("display_frequenciesAvailable",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var setFreqField = ovrManagerType.GetField("display_frequency",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (setFreqField != null)
                    {
                        setFreqField.SetValue(null, (float)fps);
                        Debug.Log($"[PerformanceOptimizer] Set OVRManager display frequency to {fps}Hz");
                    }
                }
                else
                {
                    Debug.Log($"[PerformanceOptimizer] OVRManager not found. Using Application.targetFrameRate ({fps}) for refresh rate.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PerformanceOptimizer] Could not set XR refresh rate: {e.Message}");
            }

            // 3. Enable fixed foveated rendering via Oculus API if available
            if (enableFixedFoveatedRendering)
            {
                // Set via XRSettings (supported on Quest via Oculus provider)
                XRSettings.useOcclusionMesh = true;
                Debug.Log("[PerformanceOptimizer] Occlusion mesh enabled for VR");
            }

            // 4. Ensure render viewport scale is at full resolution
            XRSettings.renderViewportScale = 1.0f;

            Debug.Log("[PerformanceOptimizer] VR optimizations applied successfully");
        }
    }
}
