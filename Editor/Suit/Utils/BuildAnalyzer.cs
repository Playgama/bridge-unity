using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Playgama.Suit
{
    /// <summary>
    /// Builds a WebGL player with a DetailedBuildReport and extracts build-size analytics into <see cref="BuildInfo"/>.
    ///
    /// Output:
    /// - Raises <see cref="OnBuildInfoChanged"/> multiple times during the workflow:
    ///   1) "Analyzing build..." (early status)
    ///   2) Final results (success/failure + asset list + summary)
    ///
    /// Analysis modes:
    /// - PackedAssets (preferred): uses <see cref="BuildReport"/> packedAssets mapping to estimate per-asset packed size.
    /// - DependenciesFallback (always available): uses AssetDatabase dependencies + file sizes as estimates.
    /// </summary>
    public static class BuildAnalyzer
    {
        /// <summary>
        /// Fired whenever new build/analysis state is available.
        /// Subscribers should treat this as a snapshot update (not incremental diffs).
        /// </summary>
        public static event Action<BuildInfo> OnBuildInfoChanged;

        /// <summary>EditorPrefs key used to persist the last successful build folder.</summary>
        private const string Pref_LastBuildFolder = "SUIT.Build.LastFolder";

        /// <summary>
        /// Returns the most recently used build folder, or a default folder if none was stored.
        /// </summary>
        public static string GetLastBuildFolder()
        {
            var p = EditorPrefs.GetString(Pref_LastBuildFolder, "");
            return string.IsNullOrEmpty(p) ? GetDefaultBuildFolder() : p;
        }

        /// <summary>
        /// Stores the last build folder in EditorPrefs.
        /// </summary>
        public static void SetLastBuildFolder(string folder)
        {
            if (!string.IsNullOrEmpty(folder))
                EditorPrefs.SetString(Pref_LastBuildFolder, folder);
        }

        /// <summary>
        /// Returns the default build output folder under the project working directory:
        /// {ProjectRoot}/Builds/WebGL
        /// </summary>
        public static string GetDefaultBuildFolder()
        {
            string root = Directory.GetCurrentDirectory();
            return Path.Combine(root, "Builds", "WebGL");
        }

        /// <summary>
        /// Schedules a WebGL build using the default/last-used folder.
        /// </summary>
        public static void BuildAndAnalyze()
        {
            BuildAndAnalyze(GetLastBuildFolder());
        }

        /// <summary>
        /// Schedules a WebGL build (DetailedBuildReport) and then analyzes the result.
        ///
        /// Notes:
        /// - The build is executed via <see cref="EditorApplication.delayCall"/> to avoid running long operations inside IMGUI.
        /// - Ensures there is at least one enabled scene in Build Settings.
        /// - Optionally switches the active build target to WebGL after a user confirmation dialog.
        /// </summary>
        public static void BuildAndAnalyze(string buildFolder)
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    if (string.IsNullOrEmpty(buildFolder))
                        buildFolder = GetDefaultBuildFolder();

                    if (!Directory.Exists(buildFolder))
                        Directory.CreateDirectory(buildFolder);

                    SetLastBuildFolder(buildFolder);

                    var scenes = GetEnabledBuildScenes();
                    if (scenes.Length == 0)
                    {
                        PublishError("No enabled scenes in Build Settings.");
                        return;
                    }

                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                    {
                        bool go = EditorUtility.DisplayDialog(
                            "Playgama Suit",
                            "Active Build Target is not WebGL.\nSwitch to WebGL and continue?",
                            "Switch & Continue",
                            "Cancel");

                        if (!go)
                        {
                            PublishError("Cancelled: build target is not WebGL.");
                            return;
                        }

                        EditorUtility.DisplayProgressBar("Playgama Suit", "Switching Build Target to WebGL...", 0.1f);
                        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                        EditorUtility.ClearProgressBar();

                        if (!switched)
                        {
                            PublishError("Failed to switch build target to WebGL.");
                            return;
                        }
                    }

                    var opts = new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = buildFolder,
                        target = BuildTarget.WebGL,
                        options = BuildOptions.DetailedBuildReport
                    };

                    if (EditorUserBuildSettings.development)
                        opts.options |= BuildOptions.Development;

                    var start = DateTime.UtcNow;

                    EditorUtility.DisplayProgressBar("Playgama Suit", "Building WebGL (DetailedBuildReport)...", 0.05f);
                    BuildReport report = null;
                    try
                    {
                        report = BuildPipeline.BuildPlayer(opts);
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }

                    var elapsed = DateTime.UtcNow - start;

                    AnalyzeReport(report, elapsed);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    PublishError("Build&Analyze exception: " + ex.Message);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            };
        }

        /// <summary>
        /// Builds a <see cref="BuildInfo"/> snapshot from a <see cref="BuildReport"/> and raises it to subscribers.
        /// Chooses the best available data mode:
        /// - PackedAssets (preferred) if usable per-asset mapping exists
        /// - DependenciesFallback otherwise (guaranteed to produce a list)
        /// </summary>
        private static void AnalyzeReport(BuildReport report, TimeSpan buildTime)
        {
            var info = new BuildInfo();
            info.StatusMessage = "Analyzing build...";
            Raise(info);

            try
            {
                info.BuildTargetName = "WebGL";
                info.BuildTime = buildTime;

                if (report == null)
                {
                    info.BuildSucceeded = false;
                    info.HasData = false;
                    info.StatusMessage = "BuildReport is null. Cannot analyze.";
                    Raise(info);
                    return;
                }

                info.UsedBuildReport = true;
                info.BuildSucceeded = report.summary.result == BuildResult.Succeeded;
                info.TotalBuildSizeBytes = SafeLong(report.summary.totalSize);

                bool packedOk = TryAnalyzePackedAssets(
                    report,
                    out var packedAssets,
                    out int packedGroups,
                    out int emptyPaths,
                    out string diag);

                if (packedOk)
                {
                    info.DataMode = BuildDataMode.PackedAssets;
                    info.Assets = packedAssets;
                    info.PackedGroupsCount = packedGroups;
                    info.EmptyPathsCount = emptyPaths;
                    info.ModeDiagnostics = diag;
                }
                else
                {
                    info.DataMode = BuildDataMode.DependenciesFallback;
                    info.Assets = AnalyzeDependenciesFallback();
                    info.PackedGroupsCount = 0;
                    info.EmptyPathsCount = 0;
                    info.ModeDiagnostics = string.IsNullOrEmpty(diag)
                        ? "PackedAssets unavailable/insufficient. Used DependenciesFallback."
                        : ("PackedAssets rejected: " + diag + " | Used DependenciesFallback.");
                }

                long tracked = 0;
                int count = 0;

                if (info.Assets != null)
                {
                    count = info.Assets.Count;
                    for (int i = 0; i < info.Assets.Count; i++)
                        tracked += info.Assets[i].SizeBytes;
                }

                info.TrackedBytes = tracked;
                info.TrackedAssetCount = count;
                info.HasData = count > 0;

                info.StatusMessage =
                    $"Build: {(info.BuildSucceeded ? "Succeeded" : report.summary.result.ToString())} | " +
                    $"Total: {SharedTypes.FormatBytes(info.TotalBuildSizeBytes)} | " +
                    $"Mode: {info.DataMode} | Tracked: {count}";

                Raise(info);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                info.BuildSucceeded = false;
                info.HasData = false;
                info.StatusMessage = "Analyze exception: " + ex.Message;
                Raise(info);
            }
        }

        /// <summary>
        /// Preferred analysis: uses BuildReport.packedAssets to map packed sizes to source assets.
        /// </summary>
        private static bool TryAnalyzePackedAssets(
            BuildReport report,
            out List<AssetInfo> assets,
            out int packedGroupsCount,
            out int emptyPathsCount,
            out string diagnostics)
        {
            assets = new List<AssetInfo>(2048);
            packedGroupsCount = 0;
            emptyPathsCount = 0;
            diagnostics = "";

            try
            {
                var repType = report.GetType();
                var packedProp = repType.GetProperty("packedAssets", BindingFlags.Instance | BindingFlags.Public);
                if (packedProp == null)
                {
                    diagnostics = "BuildReport.packedAssets property not found.";
                    return false;
                }

                var packedObj = packedProp.GetValue(report, null);
                if (packedObj == null)
                {
                    diagnostics = "BuildReport.packedAssets is null.";
                    return false;
                }

                if (!(packedObj is IEnumerable packedEnumerable))
                {
                    diagnostics = "BuildReport.packedAssets is not IEnumerable.";
                    return false;
                }

                var map = new Dictionary<string, long>(4096);
                int groups = 0;
                int empty = 0;

                foreach (var group in packedEnumerable)
                {
                    if (group == null) continue;
                    groups++;

                    var contentsProp = group.GetType().GetProperty("contents", BindingFlags.Instance | BindingFlags.Public);
                    if (contentsProp == null)
                    {
                        diagnostics = "PackedAssetInfo.contents not found -> cannot map assets.";
                        return false;
                    }

                    var contentsObj = contentsProp.GetValue(group, null);
                    if (contentsObj == null) continue;

                    if (!(contentsObj is IEnumerable contentsEnumerable))
                    {
                        diagnostics = "PackedAssetInfo.contents is not IEnumerable.";
                        return false;
                    }

                    foreach (var c in contentsEnumerable)
                    {
                        if (c == null) continue;

                        string path = ReadString(c, "sourceAssetPath");
                        if (string.IsNullOrEmpty(path))
                        {
                            string guid = ReadString(c, "sourceAssetGUID");
                            if (!string.IsNullOrEmpty(guid))
                                path = AssetDatabase.GUIDToAssetPath(guid);
                        }

                        if (string.IsNullOrEmpty(path))
                        {
                            empty++;
                            continue;
                        }

                        long sz = ReadUlongAsLong(c, "packedSize");
                        if (sz <= 0)
                        {
                            diagnostics = "Packed content has no packedSize -> cannot do per-asset sizing.";
                            return false;
                        }

                        if (map.TryGetValue(path, out long cur))
                            map[path] = cur + sz;
                        else
                            map[path] = sz;
                    }
                }

                if (groups == 0 || map.Count == 0)
                {
                    diagnostics = $"Packed assets empty. Groups={groups}, MappedAssets={map.Count}.";
                    return false;
                }

                packedGroupsCount = groups;
                emptyPathsCount = empty;

                foreach (var kv in map)
                {
                    var t = AssetDatabase.GetMainAssetTypeAtPath(kv.Key);

                    assets.Add(new AssetInfo
                    {
                        Path = kv.Key,
                        SizeBytes = kv.Value,
                        TypeName = (t != null) ? t.Name : "Unknown",
                        Category = CategorizeAsset(kv.Key, t),
                        IsSizeEstimated = false
                    });
                }

                assets.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));

                diagnostics = $"PackedAssets OK | Groups={groups} | EmptyPaths={empty} | Mapped={assets.Count}";
                return true;
            }
            catch (Exception ex)
            {
                diagnostics = "PackedAssets exception: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Fallback analysis: collects dependencies of enabled build scenes using AssetDatabase.GetDependencies.
        /// </summary>
        private static List<AssetInfo> AnalyzeDependenciesFallback()
        {
            var result = new List<AssetInfo>(4096);

            var scenes = GetEnabledBuildScenes();
            if (scenes.Length == 0) return result;

            string[] deps = null;
            try
            {
                EditorUtility.DisplayProgressBar("Playgama Suit", "Collecting dependencies...", 0.15f);
                deps = AssetDatabase.GetDependencies(scenes, true);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (deps == null || deps.Length == 0) return result;

            var set = new HashSet<string>(deps);

            int idx = 0;
            foreach (var p in set)
            {
                idx++;
                if (idx % 250 == 0)
                    EditorUtility.DisplayProgressBar("Playgama Suit", "Analyzing dependencies...", idx / Mathf.Max(1f, (float)set.Count));

                if (string.IsNullOrEmpty(p)) continue;

                if (!(p.StartsWith("Assets/", StringComparison.Ordinal) || p.StartsWith("Packages/", StringComparison.Ordinal)))
                    continue;

                long size = SharedTypes.GetFileSizeForAssetPath(p);
                var t = AssetDatabase.GetMainAssetTypeAtPath(p);

                result.Add(new AssetInfo
                {
                    Path = p,
                    SizeBytes = size,
                    TypeName = (t != null) ? t.Name : "Unknown",
                    Category = CategorizeAsset(p, t),
                    IsSizeEstimated = true
                });
            }

            EditorUtility.ClearProgressBar();

            result.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
            return result;
        }

        private static string[] GetEnabledBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null) return Array.Empty<string>();

            var list = new List<string>();
            for (int i = 0; i < scenes.Length; i++)
            {
                var s = scenes[i];
                if (s != null && s.enabled && !string.IsNullOrEmpty(s.path))
                    list.Add(s.path);
            }
            return list.ToArray();
        }

        private static void PublishError(string msg)
        {
            var info = new BuildInfo
            {
                BuildSucceeded = false,
                HasData = false,
                StatusMessage = msg
            };
            Raise(info);
        }

        private static void Raise(BuildInfo info)
        {
            try { OnBuildInfoChanged?.Invoke(info); }
            catch (Exception ex) { UnityEngine.Debug.LogException(ex); }
        }

        private static long SafeLong(ulong v)
        {
            if (v > long.MaxValue) return long.MaxValue;
            return (long)v;
        }

        private static long ReadUlongAsLong(object obj, string propName)
        {
            if (obj == null) return 0;
            try
            {
                var p = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
                if (p == null) return 0;

                var v = p.GetValue(obj, null);
                if (v == null) return 0;

                if (v is ulong uu) return SafeLong(uu);
                if (v is long ll) return ll;
                if (v is int ii) return ii;
                if (v is uint ui) return ui;

                return 0;
            }
            catch { return 0; }
        }

        private static string ReadString(object obj, string propName)
        {
            if (obj == null) return null;
            try
            {
                var p = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public);
                if (p == null) return null;
                return p.GetValue(obj, null) as string;
            }
            catch { return null; }
        }

        private static AssetCategory CategorizeAsset(string path, Type t)
        {
            if (t == typeof(Texture2D) || t == typeof(Sprite))
                return AssetCategory.Textures;

            if (t != null && t.Name == "AudioClip")
                return AssetCategory.Audio;

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".fbx" || ext == ".obj" || ext == ".dae" || ext == ".blend")
                return AssetCategory.Models;

            if (t != null && t.Name == "Mesh")
                return AssetCategory.Meshes;

            return AssetCategory.Other;
        }
    }
}
