using UnityEngine;
using UnityEditor;

public class ForceMaterialUpgrade : EditorWindow
{
    [MenuItem("Tools/Force Upgrade Selected Materials to URP")]
    public static void UpgradeSelectedMaterials()
    {
        Object[] selectedObjects = Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets);
        
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("No materials selected. Please select materials in the Project window first.");
            return;
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("Could not find URP Lit shader. Is URP installed?");
            return;
        }

        int count = 0;
        foreach (Object obj in selectedObjects)
        {
            Material mat = obj as Material;
            if (mat != null)
            {
                // Try to save original properties
                Texture mainTex = null;
                Color color = Color.white;
                
                if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

                if (mat.HasProperty("_Color")) color = mat.GetColor("_Color");
                else if (mat.HasProperty("_BaseColor")) color = mat.GetColor("_BaseColor");

                // Change shader
                mat.shader = urpLit;

                // Reassign properties to URP slots
                if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);
                mat.SetColor("_BaseColor", color);

                EditorUtility.SetDirty(mat);
                count++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Successfully forced upgrade of {count} materials to URP Lit.");
    }
}
