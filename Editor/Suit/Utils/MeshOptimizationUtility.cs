using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Playgama.Suit
{
    /// <summary>
    /// Helper methods for batch-optimizing model import settings and optionally applying Static flags
    /// to scene objects that use selected model assets.
    ///
    /// This utility focuses on safe, predictable importer changes:
    /// - Mesh compression
    /// - Read/Write toggle
    /// - Mesh optimization toggle (reflection-safe across Unity versions)
    ///
    /// Additionally, it can scan build scenes and set StaticEditorFlags on objects that reference
    /// the selected model files via MeshFilter or SkinnedMeshRenderer.
    /// </summary>
    public static class MeshOptimizationUtility
    {
        /// <summary>
        /// Settings applied to a ModelImporter during a batch operation.
        /// </summary>
        public struct ModelBatchSettings
        {
            /// <summary>
            /// Compression level for meshes inside the imported model.
            /// Higher compression can reduce build size but may affect precision.
            /// </summary>
            public ModelImporterMeshCompression MeshCompression;

            /// <summary>
            /// If true, disables Read/Write by setting ModelImporter.isReadable = false.
            /// Disabling Read/Write reduces memory usage and can reduce build size for some pipelines,
            /// but will break runtime mesh modification that requires read access.
            /// </summary>
            public bool DisableReadWrite;

            /// <summary>
            /// Enables Unity's mesh optimization pass (when available in the current Unity version).
            /// Applied via reflection to avoid compile issues across versions.
            /// </summary>
            public bool OptimizeMesh;
        }

        /// <summary>
        /// Reads the current import settings for the model at the given asset path.
        /// </summary>
        /// <param name="assetPath">AssetDatabase path to a model file (e.g. "Assets/Models/Ship.fbx").</param>
        /// <param name="current">Current importer settings (output).</param>
        /// <param name="message">Human-readable message describing failures (output).</param>
        /// <returns>True if the asset has a ModelImporter and settings were read; otherwise false.</returns>
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

        /// <summary>
        /// Applies the target settings to a model importer.
        /// </summary>
        /// <param name="assetPath">AssetDatabase path to a model file.</param>
        /// <param name="target">Desired settings.</param>
        /// <param name="changed">True if any setting was changed and reimport occurred.</param>
        /// <param name="message">Result message (applied / no changes / error).</param>
        /// <returns>True if the operation ran and the importer existed; false if not a ModelImporter or exception.</returns>
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

                Undo.RecordObject(importer, "Suit Mesh Batch Apply");

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

        /// <summary>
        /// Reads the mesh optimization toggle from a ModelImporter using reflection.
        ///
        /// Unity has changed importer APIs across versions; this method currently attempts to read:
        /// - ModelImporter.optimizeMesh (bool)
        ///
        /// If the property is missing or unreadable, returns false (safe default).
        /// </summary>
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

        /// <summary>
        /// Sets the mesh optimization toggle on a ModelImporter using reflection.
        ///
        /// Behavior:
        /// - If property exists and the value differs, sets it and returns true.
        /// - If the property doesn't exist, returns false and does not treat it as an error.
        /// </summary>
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

        /// <summary>
        /// Applies StaticEditorFlags to GameObjects in the provided scenes that reference any of the selected model assets.
        ///
        /// Matching logic:
        /// - MeshFilter.sharedMesh asset path matches one of the model asset paths (typically .fbx/.obj)
        /// - SkinnedMeshRenderer.sharedMesh asset path matches one of the model asset paths
        ///
        /// Notes:
        /// - Scene scanning opens scenes one-by-one (Single mode), applies changes, marks dirty, and saves.
        /// - The caller supplies the desired flags (the method sets EXACTLY that flags value).
        /// - The method prompts the user to save modified scenes before switching scenes (best-effort across Unity versions).
        /// </summary>
        /// <param name="scenePaths">AssetDatabase paths to scenes (e.g. "Assets/Scenes/Main.unity").</param>
        /// <param name="modelAssetPaths">Set of model file paths (e.g. "Assets/Models/Ship.fbx").</param>
        /// <param name="flags">Static flags to apply to matching GameObjects.</param>
        /// <param name="affectedObjects">Number of GameObjects whose flags were changed.</param>
        /// <param name="affectedScenes">Number of scenes saved with changes.</param>
        /// <param name="message">Result message summary.</param>
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
                    EditorUtility.DisplayProgressBar("Playgama Suit", "Scanning scene: " + sp, prog);

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

        /// <summary>
        /// Applies the provided static flags to all matching GameObjects in an already opened scene.
        /// </summary>
        private static int ApplyStaticInOpenScene(Scene scene, System.Collections.Generic.HashSet<string> modelAssetPaths, StaticEditorFlags flags)
        {
            int changed = 0;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                changed += ApplyStaticRecursive(roots[i], modelAssetPaths, flags);

            return changed;
        }

        /// <summary>
        /// Recursively traverses the hierarchy and sets flags on GameObjects that use selected models.
        ///
        /// Flags are set exactly to the provided value (no merging):
        /// - predictable behavior
        /// - the caller UI can present the full resulting flags state
        /// </summary>
        private static int ApplyStaticRecursive(GameObject go, System.Collections.Generic.HashSet<string> modelAssetPaths, StaticEditorFlags flags)
        {
            int changed = 0;

            if (UsesSelectedModel(go, modelAssetPaths))
            {
                var cur = GameObjectUtility.GetStaticEditorFlags(go);

                if (cur != flags)
                {
                    Undo.RecordObject(go, "Suit Set Static Flags");
                    GameObjectUtility.SetStaticEditorFlags(go, flags);
                    changed++;
                }
            }

            var t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                changed += ApplyStaticRecursive(t.GetChild(i).gameObject, modelAssetPaths, flags);

            return changed;
        }

        /// <summary>
        /// Checks whether a GameObject references any selected model asset via:
        /// - MeshFilter.sharedMesh
        /// - SkinnedMeshRenderer.sharedMesh
        ///
        /// AssetDatabase.GetAssetPath(mesh) typically returns the model file path (e.g. .fbx),
        /// even if the mesh is a sub-asset inside the model.
        /// </summary>
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

        /// <summary>
        /// Prompts the user to save modified scenes before the utility starts opening other scenes.
        ///
        /// Uses reflection to maximize compatibility with Unity versions that expose different APIs:
        /// 1) EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()
        /// 2) EditorSceneManager.SaveModifiedScenesIfUserWantsTo(Scene[] scenes)
        ///
        /// If no compatible method is found, it returns true so the operation is not blocked.
        /// </summary>
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
