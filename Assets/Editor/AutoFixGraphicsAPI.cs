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

            // Ensure Vulkan is the primary API (required by Meta Quest OpenXR)
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (apis.Length == 0 || apis[0] != GraphicsDeviceType.Vulkan)
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
                changed = true;
                Debug.Log("[AutoFixGraphicsAPI] Configured Android Graphics APIs to Vulkan (primary) with OpenGLES3 fallback.");
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
        }
    }
}
