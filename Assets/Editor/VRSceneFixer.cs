using UnityEngine;
using UnityEditor;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

public class VRSceneFixer : EditorWindow
{
    [MenuItem("VR Tools/Apply Complete VR Fixes")]
    public static void ApplyFixes()
    {
        int changes = 0;
        
        // 1. Fix Camera Clipping (Flickering) and Multiple Audio Listeners
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        Camera mainCamera = Camera.main;
        
        foreach (Camera cam in cameras)
        {
            if (cam.nearClipPlane > 0.02f || cam.nearClipPlane < 0.005f)
            {
                cam.nearClipPlane = 0.01f;
                Debug.Log($"[VR Fixer] Fixed {cam.name} near clip plane to 0.01 to prevent VR near clipping.");
                changes++;
            }
            if (cam.farClipPlane > 2000f)
            {
                cam.farClipPlane = 1000f; 
                Debug.Log($"[VR Fixer] Fixed {cam.name} far clip plane to 1000 to prevent Z-fighting/flickering on models.");
                changes++;
            }
            
            // Remove rigidbody on camera which causes jitter
            Rigidbody rb = cam.GetComponent<Rigidbody>();
            if (rb != null)
            {
                DestroyImmediate(rb);
                Debug.Log($"[VR Fixer] Removed Rigidbody from {cam.name} to prevent physics-induced VR jitter.");
                changes++;
            }
        }
        
        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        if (listeners.Length > 1)
        {
            for (int i = 0; i < listeners.Length; i++)
            {
                if (listeners[i].gameObject != mainCamera?.gameObject)
                {
                    DestroyImmediate(listeners[i]);
                    Debug.Log($"[VR Fixer] Removed duplicate AudioListener on {listeners[i].gameObject.name}.");
                    changes++;
                }
            }
        }

        // Find XR Origin
        XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>(FindObjectsInactive.Include);
        
        // 2. Fix Locomotion Controls (Decoupled Move/Look)
        var moveProviders = Object.FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.ActionBasedContinuousMoveProvider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var move in moveProviders)
        {
            // By setting forwardSource to XR Origin (or leaving it null and placing it on XR Origin), movement is based on the rig, not the head.
            if (xrOrigin != null && move.forwardSource != xrOrigin.transform)
            {
                move.forwardSource = xrOrigin.transform;
                Debug.Log($"[VR Fixer] Set {move.name} forwardSource to XR Origin (Rig). You will now walk straight regardless of head rotation.");
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
                    Debug.Log("[VR Fixer] Created Input Action Manager and assigned XRI Default Input Actions. This enables Left/Right controllers.");
                    changes++;
                }
            }
            else
            {
                Debug.LogWarning("[VR Fixer] Created Input Action Manager but could not find 'XRI Default Input Actions'. Please assign your Input Actions manually.");
                changes++;
            }
        }
        else
        {
            Debug.Log("[VR Fixer] InputActionManager already exists.");
        }

        EditorUtility.DisplayDialog("Complete VR Fixes Applied", 
            $"Successfully applied {changes} fixes to the scene.\n\n" +
            "1. Camera Jitter/Flicker: Adjusted clip planes and removed rigidbodies.\n" +
            "2. Decoupled Movement: Set Forward Source to XR Origin so head rotation doesn't steer you.\n" +
            "3. VR Controls: Configured Input Action Manager for Left/Right hand inputs.\n\n" +
            "Please test this in your headset!", "OK");
    }
}
