using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace StationWalkthrough.Editor
{
    [InitializeOnLoad]
    public class AutoFixGraphicsAPI
    {
        static AutoFixGraphicsAPI()
        {
            EditorApplication.delayCall += FixGraphicsAPI;
        }

        static void FixGraphicsAPI()
        {
            bool changed = false;

            // Ensure Auto Graphics API is disabled for Android
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android))
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                changed = true;
            }

            // Get current APIs
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            bool hasGLES3 = false;
            bool hasVulkan = false;

            foreach (var api in apis)
            {
                if (api == GraphicsDeviceType.OpenGLES3) hasGLES3 = true;
                if (api == GraphicsDeviceType.Vulkan) hasVulkan = true;
            }

            // We only want OpenGLES3, or at least OpenGLES3 first and NO Vulkan.
            // Vulkan is the #1 cause of black screens on URP mobile builds.
            if (hasVulkan || !hasGLES3 || (apis.Length > 0 && apis[0] != GraphicsDeviceType.OpenGLES3))
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
                changed = true;
                Debug.Log("[AutoFixGraphicsAPI] Safely removed Vulkan and switched Android Graphics API to OpenGLES3 to prevent black screens.");
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
