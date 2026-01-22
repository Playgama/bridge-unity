using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Playgama.Bridge
{
    public static class MeshOptimizationUtility
    {
        public struct ModelBatchSettings
        {
            public ModelImporterMeshCompression MeshCompression;
            public bool DisableReadWrite;
            public bool OptimizeMesh;
        }

        public static bool TryReadCurrent(string assetPath, out ModelBatchSettings current, out string message)
        {
            current = default;
            message = "";

            try
            {
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null)
                {
                    message = "Not a ModelImporter.";
                    return false;
                }

                current = new ModelBatchSettings
                {
                    MeshCompression = importer.meshCompression,
                    DisableReadWrite = !importer.isReadable,
                    OptimizeMesh = GetOptimizeMesh(importer)
                };

                return true;
            }
            catch (Exception ex)
            {
                message = "Read error: " + ex.Message;
                return false;
            }
        }

        public static bool Apply(string assetPath, ModelBatchSettings target, out bool changed, out string message)
        {
            changed = false;
            message = "";

            try
            {
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null)
                {
                    message = "Not a ModelImporter.";
                    return false;
                }

                Undo.RecordObject(importer, "Bridge Mesh Batch Apply");

                bool dirty = false;

                if (importer.meshCompression != target.MeshCompression)
                {
                    importer.meshCompression = target.MeshCompression;
                    dirty = true;
                }

                bool wantReadable = !target.DisableReadWrite;
                if (importer.isReadable != wantReadable)
                {
                    importer.isReadable = wantReadable;
                    dirty = true;
                }

                dirty |= SetOptimizeMesh(importer, target.OptimizeMesh);

                changed = dirty;

                if (dirty)
                {
                    importer.SaveAndReimport();
                    message = "Applied.";
                }
                else
                {
                    message = "No changes needed.";
                }

                return true;
            }
            catch (Exception ex)
            {
                message = "Apply error: " + ex.Message;
                return false;
            }
        }

        // Uses reflection to support different Unity versions
        private static bool GetOptimizeMesh(ModelImporter importer)
        {
            try
            {
                var p = typeof(ModelImporter).GetProperty("optimizeMesh", BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.PropertyType == typeof(bool))
                {
                    object v = p.GetValue(importer, null);
                    if (v is bool b) return b;
                }
            }
            catch { }
            return false;
        }

        // Uses reflection to support different Unity versions
        private static bool SetOptimizeMesh(ModelImporter importer, bool value)
        {
            try
            {
                var p = typeof(ModelImporter).GetProperty("optimizeMesh", BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.PropertyType == typeof(bool) && p.CanWrite)
                {
                    bool cur = false;
                    try
                    {
                        object v = p.GetValue(importer, null);
                        if (v is bool b) cur = b;
                    }
                    catch { }

                    if (cur == value) return false;

                    p.SetValue(importer, value, null);
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static void ApplyStaticFlagsInBuildScenes(
            string[] scenePaths,
            System.Collections.Generic.HashSet<string> modelAssetPaths,
            StaticEditorFlags flags,
            out int affectedObjects,
            out int affectedScenes,
            out string message)
        {
            affectedObjects = 0;
            affectedScenes = 0;
            message = "";

            if (scenePaths == null || scenePaths.Length == 0)
            {
                message = "No scenes.";
                return;
            }

            if (modelAssetPaths == null || modelAssetPaths.Count == 0)
            {
                message = "No selected models.";
                return;
            }

            if (!SaveIfUserWants())
            {
                message = "Cancelled by user (save scenes).";
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            string activePath = activeScene.path;

            try
            {
                for (int si = 0; si < scenePaths.Length; si++)
                {
                    string sp = scenePaths[si];
                    float prog = (si + 1) / Mathf.Max(1f, (float)scenePaths.Length);
                    EditorUtility.DisplayProgressBar("Playgama Bridge", "Scanning scene: " + sp, prog);

                    var scene = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);

                    int changedInScene = ApplyStaticInOpenScene(scene, modelAssetPaths, flags);
                    if (changedInScene > 0)
                    {
                        affectedScenes++;
                        affectedObjects += changedInScene;
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }

                message = $"Static applied. Scenes changed={affectedScenes}, Objects changed={affectedObjects}.";
            }
            catch (Exception ex)
            {
                message = "Static apply error: " + ex.Message;
                UnityEngine.Debug.LogException(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                if (!string.IsNullOrEmpty(activePath))
                {
                    try { EditorSceneManager.OpenScene(activePath, OpenSceneMode.Single); }
                    catch { }
                }
            }
        }

        private static int ApplyStaticInOpenScene(Scene scene, System.Collections.Generic.HashSet<string> modelAssetPaths, StaticEditorFlags flags)
        {
            int changed = 0;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                changed += ApplyStaticRecursive(roots[i], modelAssetPaths, flags);

            return changed;
        }

        private static int ApplyStaticRecursive(GameObject go, System.Collections.Generic.HashSet<string> modelAssetPaths, StaticEditorFlags flags)
        {
            int changed = 0;

            if (UsesSelectedModel(go, modelAssetPaths))
            {
                var cur = GameObjectUtility.GetStaticEditorFlags(go);

                if (cur != flags)
                {
                    Undo.RecordObject(go, "Bridge Set Static Flags");
                    GameObjectUtility.SetStaticEditorFlags(go, flags);
                    changed++;
                }
            }

            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                changed += ApplyStaticRecursive(t.GetChild(i).gameObject, modelAssetPaths, flags);

            return changed;
        }

        private static bool UsesSelectedModel(GameObject go, System.Collections.Generic.HashSet<string> modelAssetPaths)
        {
            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string meshPath = AssetDatabase.GetAssetPath(mf.sharedMesh);
                if (!string.IsNullOrEmpty(meshPath) && modelAssetPaths.Contains(meshPath))
                    return true;
            }

            var smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                string meshPath = AssetDatabase.GetAssetPath(smr.sharedMesh);
                if (!string.IsNullOrEmpty(meshPath) && modelAssetPaths.Contains(meshPath))
                    return true;
            }

            return false;
        }

        // Uses reflection to maximize compatibility across Unity versions
        private static bool SaveIfUserWants()
        {
            try
            {
                var m = typeof(EditorSceneManager).GetMethod(
                    "SaveCurrentModifiedScenesIfUserWantsTo",
                    BindingFlags.Public | BindingFlags.Static);

                if (m != null && m.ReturnType == typeof(bool) && m.GetParameters().Length == 0)
                {
                    object r = m.Invoke(null, null);
                    return (r is bool b) ? b : true;
                }
            }
            catch { }

            try
            {
                var m = typeof(EditorSceneManager).GetMethod(
                    "SaveModifiedScenesIfUserWantsTo",
                    BindingFlags.Public | BindingFlags.Static);

                if (m != null && m.ReturnType == typeof(bool))
                {
                    var ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(Scene[]))
                    {
                        Scene[] arr = new[] { SceneManager.GetActiveScene() };
                        object r = m.Invoke(null, new object[] { arr });
                        return (r is bool b) ? b : true;
                    }
                }
            }
            catch { }

            return true;
        }
    }
}
