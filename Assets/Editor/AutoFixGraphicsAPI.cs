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

            // Ensure at least Vulkan or OpenGLES3 is configured
            if (!hasVulkan && !hasGLES3)
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
                changed = true;
                Debug.Log("[AutoFixGraphicsAPI] Configured Android Graphics APIs to Vulkan with OpenGLES3 fallback.");
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
