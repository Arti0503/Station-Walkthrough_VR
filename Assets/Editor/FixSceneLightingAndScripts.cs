using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace StationWalkthrough.Editor
{
    [InitializeOnLoad]
    public static class FixSceneLightingAndScripts
    {
        static FixSceneLightingAndScripts()
        {
            EditorApplication.delayCall += FixAll;
        }

        [MenuItem("Tools/Fix Scene Lighting & Missing Colliders")]
        public static void FixAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer) return;
            
            FixPrefabs();
            FixLoadedScenes();
        }

        private static void FixPrefabs()
        {
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (path.StartsWith("Packages/", System.StringComparison.OrdinalIgnoreCase)) continue;

                GameObject contentsRoot = null;
                try
                {
                    contentsRoot = PrefabUtility.LoadPrefabContents(path);
                }
                catch
                {
                    continue;
                }

                if (contentsRoot == null) continue;

                bool isDirty = false;

                // Remove missing scripts recursively
                Transform[] allTransforms = contentsRoot.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allTransforms)
                {
                    int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                    if (removedCount > 0)
                    {
                        Debug.Log($"[FixSceneLighting] Removed {removedCount} missing script(s) from prefab child: {t.name} in {path}");
                        isDirty = true;
                    }
                }

                // Ensure Area Lights in URP are set to Baked mode
                Light[] lights = contentsRoot.GetComponentsInChildren<Light>(true);
                foreach (Light l in lights)
                {
                    if (l.type == LightType.Rectangle || (int)l.type == 3)
                    {
                        SerializedObject so = new SerializedObject(l);
                        SerializedProperty lightmappingProp = so.FindProperty("m_Lightmapping");
                        if (lightmappingProp != null && lightmappingProp.intValue != 2) // 2 = Baked
                        {
                            lightmappingProp.intValue = 2;
                            so.ApplyModifiedProperties();
                            Debug.Log($"[FixSceneLighting] Set Area Light '{l.gameObject.name}' in prefab '{path}' to Baked mode.");
                            isDirty = true;
                        }
                    }
                }

                // Ensure 3D station meshes have MeshColliders so player can walk on floors/platforms
                MeshFilter[] meshFilters = contentsRoot.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter mf in meshFilters)
                {
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    {
                        if (PrefabUtility.IsPartOfImmutablePrefab(mf.gameObject)) continue;

                        try
                        {
                            MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                            if (mc != null)
                            {
                                mc.sharedMesh = mf.sharedMesh;
                                Debug.Log($"[FixSceneLighting] Added MeshCollider to prefab mesh '{mf.gameObject.name}' in '{path}'");
                                isDirty = true;
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogWarning($"[FixSceneLighting] Could not add MeshCollider to '{mf.gameObject.name}' in '{path}': {e.Message}");
                        }
                    }
                }

                if (isDirty)
                {
                    try
                    {
                        PrefabUtility.SaveAsPrefabAsset(contentsRoot, path);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[FixSceneLighting] Error saving prefab '{path}': {e.Message}");
                    }
                }
                
                PrefabUtility.UnloadPrefabContents(contentsRoot);
            }
        }

        private static void FixLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                bool isDirty = false;

                GameObject[] rootObjects = scene.GetRootGameObjects();
                foreach (GameObject root in rootObjects)
                {
                    Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (Transform t in allTransforms)
                    {
                        int removedCount = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                        if (removedCount > 0)
                        {
                            Debug.Log($"[FixSceneLighting] Removed {removedCount} missing script(s) from scene object: {t.name}");
                            isDirty = true;
                        }
                    }

                    Light[] lights = root.GetComponentsInChildren<Light>(true);
                    foreach (Light l in lights)
                    {
                        if (l.type == LightType.Rectangle || (int)l.type == 3)
                        {
                            SerializedObject so = new SerializedObject(l);
                            SerializedProperty lightmappingProp = so.FindProperty("m_Lightmapping");
                            if (lightmappingProp != null && lightmappingProp.intValue != 2) // 2 = Baked
                            {
                                lightmappingProp.intValue = 2;
                                so.ApplyModifiedProperties();
                                Debug.Log($"[FixSceneLighting] Set Area Light '{l.gameObject.name}' in scene to Baked mode.");
                                isDirty = true;
                            }
                        }
                    }

                    // Add MeshCollider to 3D environment meshes that lack colliders
                    MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
                    foreach (MeshFilter mf in meshFilters)
                    {
                        if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                        {
                            if (PrefabUtility.IsPartOfImmutablePrefab(mf.gameObject)) continue;

                            try
                            {
                                MeshCollider mc = mf.gameObject.AddComponent<MeshCollider>();
                                if (mc != null)
                                {
                                    mc.sharedMesh = mf.sharedMesh;
                                    Debug.Log($"[FixSceneLighting] Added MeshCollider to scene mesh '{mf.gameObject.name}'");
                                    isDirty = true;
                                }
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogWarning($"[FixSceneLighting] Could not add MeshCollider to '{mf.gameObject.name}' in scene: {e.Message}");
                            }
                        }
                    }
                }

                if (isDirty)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
    }
}
