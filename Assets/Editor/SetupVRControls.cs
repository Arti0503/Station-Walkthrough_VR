using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace StationWalkthrough.Editor
{
    public class SetupVRControls : EditorWindow
    {
        [MenuItem("VR Tools/Clean Duplicate Controllers & Setup VR Input")]
        public static void CleanAndConfigureVRControllersMenu()
        {
            CleanAndConfigureVRControllers(true);
        }

        public static void CleanAndConfigureVRControllers(bool showDialog = false)
        {
            int cleaned = 0;
            int configured = 0;
            Debug.Log("[VR Controller Setup] Cleaning duplicates and configuring VR Input System...");

            // 1. Process active scene
            Scene activeScene = SceneManager.GetActiveScene();
            GameObject[] rootObjects = activeScene.GetRootGameObjects();

            foreach (var root in rootObjects)
            {
                SimpleFPPController controller = root.GetComponentInChildren<SimpleFPPController>(true);
                if (controller != null)
                {
                    cleaned += CleanDuplicateControllers(controller.gameObject);
                    configured += ConfigureControllerObject(controller.gameObject, "Scene: " + controller.gameObject.name);
                }
            }

            // 2. Process Player.prefab and XR Origin (VR).prefab
            string[] prefabPaths = new string[]
            {
                "Assets/prefabs/Player.prefab",
                "Assets/prefabs/XR Origin (VR).prefab"
            };

            foreach (var path in prefabPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    GameObject contentsRoot = PrefabUtility.LoadPrefabContents(path);
                    if (contentsRoot != null)
                    {
                        SimpleFPPController fpp = contentsRoot.GetComponentInChildren<SimpleFPPController>(true);
                        if (fpp != null)
                        {
                            int cClean = CleanDuplicateControllers(fpp.gameObject);
                            int cConf = ConfigureControllerObject(fpp.gameObject, "Prefab: " + path);
                            if (cClean > 0 || cConf > 0)
                            {
                                PrefabUtility.SaveAsPrefabAsset(contentsRoot, path);
                                cleaned += cClean;
                                configured += cConf;
                                Debug.Log($"[VR Controller Setup] Saved updated prefab at {path}");
                            }
                        }
                        PrefabUtility.UnloadPrefabContents(contentsRoot);
                    }
                }
            }

            // 3. Mark Scene Dirty and Save if changes occurred
            if (cleaned > 0 || configured > 0)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
            }

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "VR Controller Setup Complete",
                    $"Successfully cleaned {cleaned} duplicate/placeholder objects and configured {configured} controller settings.\n\n" +
                    "• Duplicate Left & Right controller GameObjects removed\n" +
                    "• Placeholder capsule models removed\n" +
                    "• Clean 1:1 Left & Right Controller tracking anchors configured\n" +
                    "• Unity Input System TrackedPoseDrivers assigned for VR Headset and Hands\n" +
                    "• Left Stick = Headset-relative Locomotion (Move & Strafe)\n" +
                    "• Right Stick = Rotation (Snap / Smooth Turn)\n\n" +
                    "Hierarchy is now clean with single Left & Right controllers!",
                    "Great!"
                );
            }
        }

        private static int CleanDuplicateControllers(GameObject targetObj)
        {
            int removed = 0;
            SimpleFPPController fpp = targetObj.GetComponent<SimpleFPPController>();
            if (fpp == null) return 0;

            Camera cam = targetObj.GetComponent<Camera>() ?? targetObj.GetComponentInChildren<Camera>(true);
            if (cam == null && Camera.main != null) cam = Camera.main;
            Transform camTransform = (fpp.playerCamera != null) ? fpp.playerCamera : (cam != null ? cam.transform : targetObj.transform);
            Transform cameraOffset = camTransform.parent != null ? camTransform.parent : camTransform;

            List<Transform> leftControllers = new List<Transform>();
            List<Transform> rightControllers = new List<Transform>();
            List<GameObject> toDestroy = new List<GameObject>();

            for (int i = 0; i < cameraOffset.childCount; i++)
            {
                Transform child = cameraOffset.GetChild(i);
                string childName = child.name.ToLower();

                // Find placeholder models
                if (childName.Contains("leftcontrollermodel") || childName.Contains("rightcontrollermodel"))
                {
                    toDestroy.Add(child.gameObject);
                    continue;
                }

                if (childName.Contains("left") && childName.Contains("controller"))
                {
                    leftControllers.Add(child);
                }
                else if (childName.Contains("right") && childName.Contains("controller"))
                {
                    rightControllers.Add(child);
                }
            }

            // Remove child capsule visuals from inside controllers if present
            foreach (var ctrl in leftControllers)
            {
                for (int i = ctrl.childCount - 1; i >= 0; i--)
                {
                    Transform c = ctrl.GetChild(i);
                    if (c.name.ToLower().Contains("model") || c.name.ToLower().Contains("capsule"))
                    {
                        toDestroy.Add(c.gameObject);
                    }
                }
            }
            foreach (var ctrl in rightControllers)
            {
                for (int i = ctrl.childCount - 1; i >= 0; i--)
                {
                    Transform c = ctrl.GetChild(i);
                    if (c.name.ToLower().Contains("model") || c.name.ToLower().Contains("capsule"))
                    {
                        toDestroy.Add(c.gameObject);
                    }
                }
            }

            // Keep only the first Left Controller and first Right Controller; destroy duplicates
            if (leftControllers.Count > 1)
            {
                for (int i = 1; i < leftControllers.Count; i++)
                {
                    toDestroy.Add(leftControllers[i].gameObject);
                }
            }
            if (rightControllers.Count > 1)
            {
                for (int i = 1; i < rightControllers.Count; i++)
                {
                    toDestroy.Add(rightControllers[i].gameObject);
                }
            }

            foreach (var obj in toDestroy)
            {
                if (obj != null)
                {
                    removed++;
                    DestroyImmediate(obj);
                }
            }

            return removed;
        }

        private static int ConfigureControllerObject(GameObject targetObj, string context)
        {
            int changes = 0;
            SimpleFPPController fpp = targetObj.GetComponent<SimpleFPPController>();
            if (fpp == null) return 0;

            SerializedObject so = new SerializedObject(fpp);

            // Ensure playerCamera is assigned
            if (fpp.playerCamera == null)
            {
                Camera cam = targetObj.GetComponent<Camera>() ?? targetObj.GetComponentInChildren<Camera>(true);
                if (cam == null && Camera.main != null) cam = Camera.main;
                if (cam != null)
                {
                    so.FindProperty("playerCamera").objectReferenceValue = cam.transform;
                    changes++;
                }
            }

            Transform camTransform = fpp.playerCamera != null ? fpp.playerCamera : targetObj.transform;
            Transform cameraOffset = camTransform.parent != null ? camTransform.parent : camTransform;

            // Ensure Single Left Controller exists under cameraOffset
            Transform leftCtrl = cameraOffset.Find("Left Controller") ?? cameraOffset.Find("LeftController");
            if (leftCtrl == null)
            {
                GameObject leftObj = new GameObject("Left Controller");
                leftObj.transform.SetParent(cameraOffset, false);
                leftObj.transform.localPosition = new Vector3(-0.2f, -0.1f, 0.3f);
                leftObj.transform.localRotation = Quaternion.identity;
                SetupTrackedPoseDriver(leftObj, true);
                leftCtrl = leftObj.transform;
                Debug.Log($"[{context}] Created Left Controller GameObject under {cameraOffset.name}.");
                changes++;
            }
            else
            {
                var driver = leftCtrl.GetComponent<TrackedPoseDriver>();
                if (driver == null)
                {
                    SetupTrackedPoseDriver(leftCtrl.gameObject, true);
                    changes++;
                }
            }

            // Ensure Single Right Controller exists under cameraOffset
            Transform rightCtrl = cameraOffset.Find("Right Controller") ?? cameraOffset.Find("RightController");
            if (rightCtrl == null)
            {
                GameObject rightObj = new GameObject("Right Controller");
                rightObj.transform.SetParent(cameraOffset, false);
                rightObj.transform.localPosition = new Vector3(0.2f, -0.1f, 0.3f);
                rightObj.transform.localRotation = Quaternion.identity;
                SetupTrackedPoseDriver(rightObj, false);
                rightCtrl = rightObj.transform;
                Debug.Log($"[{context}] Created Right Controller GameObject under {cameraOffset.name}.");
                changes++;
            }
            else
            {
                var driver = rightCtrl.GetComponent<TrackedPoseDriver>();
                if (driver == null)
                {
                    SetupTrackedPoseDriver(rightCtrl.gameObject, false);
                    changes++;
                }
            }

            // Ensure Headset Camera has TrackedPoseDriver
            if (camTransform != null)
            {
                var camDriver = camTransform.GetComponent<TrackedPoseDriver>();
                if (camDriver == null)
                {
                    camDriver = camTransform.gameObject.AddComponent<TrackedPoseDriver>();
                    camDriver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
                    var headPosAction = new InputAction("Position", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
                    var headRotAction = new InputAction("Rotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
                    camDriver.positionInput = new InputActionProperty(headPosAction);
                    camDriver.rotationInput = new InputActionProperty(headRotAction);
                    changes++;
                }
            }

            // Assign anchors in SimpleFPPController
            so.FindProperty("leftControllerAnchor").objectReferenceValue = leftCtrl;
            so.FindProperty("rightControllerAnchor").objectReferenceValue = rightCtrl;
            so.FindProperty("useVRSnapTurn").boolValue = true;
            so.FindProperty("vrSnapTurnAngle").floatValue = 45f;
            so.FindProperty("vrHeadOrientedMovement").boolValue = true;
            so.FindProperty("autoCreateControllerAnchors").boolValue = false; // Prevent runtime duplicate creation

            so.ApplyModifiedProperties();

            // Ensure InputActionManager exists and has XRI Default Input Actions
            Transform root = targetObj.transform.root;
            InputActionManager inputManager = root.GetComponentInChildren<InputActionManager>(true);
            if (inputManager != null)
            {
                string[] guids = AssetDatabase.FindAssets("XRI Default Input Actions");
                if (guids.Length > 0)
                {
                    var xriAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    if (xriAsset != null && (inputManager.actionAssets == null || !inputManager.actionAssets.Contains(xriAsset)))
                    {
                        if (inputManager.actionAssets == null) inputManager.actionAssets = new System.Collections.Generic.List<InputActionAsset>();
                        inputManager.actionAssets.Add(xriAsset);
                        EditorUtility.SetDirty(inputManager);
                        changes++;
                    }
                }
            }

            return changes;
        }

        private static void SetupTrackedPoseDriver(GameObject obj, bool isLeft)
        {
            var driver = obj.GetComponent<TrackedPoseDriver>() ?? obj.AddComponent<TrackedPoseDriver>();
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;

            string handPath = isLeft ? "{LeftHand}" : "{RightHand}";
            string posPath = $"<XRController>{handPath}/devicePosition";
            string rotPath = $"<XRController>{handPath}/deviceRotation";

            var posAction = new InputAction("Position", InputActionType.Value, posPath, expectedControlType: "Vector3");
            var rotAction = new InputAction("Rotation", InputActionType.Value, rotPath, expectedControlType: "Quaternion");

            driver.positionInput = new InputActionProperty(posAction);
            driver.rotationInput = new InputActionProperty(rotAction);
        }
    }
}
