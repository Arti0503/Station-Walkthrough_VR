using UnityEngine;
using UnityEditor;
using System.IO;

public class QuestTextureOptimizer : EditorWindow
{
    [MenuItem("Tools/Optimize Textures for Quest")]
    public static void ShowWindow()
    {
        GetWindow<QuestTextureOptimizer>("Quest Texture Optimizer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Optimize 3D_Assets Textures for Quest VR", EditorStyles.boldLabel);
        
        GUILayout.Space(10);
        GUILayout.Label("This tool will automatically find all textures in the\n'Assets/3D_Assets' folder and apply the following:", EditorStyles.wordWrappedLabel);
        GUILayout.Label("- Generate Mip Maps: ON");
        GUILayout.Label("- Filter Mode: Trilinear");
        GUILayout.Label("- Aniso Level: 4");
        GUILayout.Label("- Android Format: ASTC 6x6");

        GUILayout.Space(20);
        if (GUILayout.Button("Optimize Textures Now", GUILayout.Height(40)))
        {
            OptimizeTextures();
        }
    }

    private void OptimizeTextures()
    {
        string folderPath = "Assets/3D_Assets";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("Error", $"Folder {folderPath} not found!", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { folderPath });
        int count = 0;

        EditorUtility.DisplayProgressBar("Optimizing Textures", "Starting...", 0);

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                bool changed = false;

                // MipMaps and Filtering
                if (!importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 4)
                {
                    importer.mipmapEnabled = true;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 4;
                    changed = true;
                }

                // Android ASTC Compression
                TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
                if (androidSettings.format != TextureImporterFormat.ASTC_6x6 || !androidSettings.overridden)
                {
                    androidSettings.overridden = true;
                    androidSettings.format = TextureImporterFormat.ASTC_6x6;
                    importer.SetPlatformTextureSettings(androidSettings);
                    changed = true;
                }

                if (changed)
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }

            EditorUtility.DisplayProgressBar("Optimizing Textures", path, (float)count / guids.Length);
        }

        EditorUtility.ClearProgressBar();
        EditorUtility.DisplayDialog("Complete", $"Successfully optimized {count} textures for Quest VR.", "OK");
        Debug.Log($"[QuestTextureOptimizer] Optimized {count} textures.");
    }
}
