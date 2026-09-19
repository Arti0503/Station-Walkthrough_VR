using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;
using UnityEngine.InputSystem;

public class FixVRIssues : EditorWindow
{
    [MenuItem("VR Tools/Fix VR Issues")]
    public static void FixIssues()
    {
        int changes = 0;
        
        // 1. Fix Camera Clipping and Multiple Audio Listeners
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Camera mainCamera = Camera.main;
        
        foreach (Camera cam in cameras)
        {
            if (cam.nearClipPlane > 0.02f || cam.nearClipPlane < 0.005f)
            {
                cam.nearClipPlane = 0.01f;
                Debug.Log($"Fixed {cam.name} near clip plane to 0.01 to prevent VR clipping.");
                changes++;
            }
            if (cam.farClipPlane > 2000f)
            {
                cam.farClipPlane = 1000f; // Prevent Z-fighting
                Debug.Log($"Fixed {cam.name} far clip plane to 1000 to prevent Z-fighting/flickering.");
                changes++;
            }
        }
        
        // Remove duplicate audio listeners
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        if (listeners.Length > 1)
        {
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].gameObject != mainCamera?.gameObject)
                {
                    DestroyImmediate(listeners[i]);
                    Debug.Log($"Removed duplicate AudioListener on {listeners[i].gameObject.name}.");
                    changes++;
                }
            }
        }

        // 2. Fix Locomotion Controls (Move where you look)
        var moveProviders = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.ActionBasedContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var move in moveProviders)
        {
            if (mainCamera != null && move.forwardSource == null)
            {
                move.forwardSource = mainCamera.transform;
                Debug.Log($"Set {move.name} forwardSource to Main Camera (Move where you look).");
                changes++;
            }
        }

        // Check if there is an InputActionManager
        var inputManagers = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (inputManagers.Length == 0)
        {
            // Attempt to create one automatically
            GameObject managerObj = new GameObject("Input Action Manager");
            var manager = managerObj.AddComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
            
            // Try to find the XRI Default Input Actions asset
            string[] guids = AssetDatabase.FindAssets("XRI Default Input Actions");
            if (guids.Length > 0)
            {
                var inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (inputActions != null)
                {
                    manager.actionAssets = new System.Collections.Generic.List<InputActionAsset> { inputActions };
                    Debug.Log("Created Input Action Manager and assigned XRI Default Input Actions.");
                    changes++;
                }
            }
            else
            {
                Debug.LogWarning("Created Input Action Manager but could not find 'XRI Default Input Actions'. Please assign your Input Actions manually.");
                changes++;
            }
        }

        EditorUtility.DisplayDialog("VR Fixes Applied", 
            $"Successfully applied {changes} fixes to the scene.\n\n" +
            "1. Fixed Camera flickering and clipping.\n" +
            "2. Set Movement to follow your head direction (Move where you look).\n" +
            "3. Checked Input Action Manager (for Left/Right controls).\n\n" +
            "If your Left/Right controls still don't work, make sure the XR Interaction Setup component in your XR Origin is active and the Locomotion System has left/right actions assigned.", "OK");
    }
}
