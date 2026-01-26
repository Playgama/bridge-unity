using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Playgama.Editor
{
    public static class BuildAnalyzer
    {
        public static event Action<BuildInfo> OnBuildInfoChanged;

        private const string Pref_LastBuildFolder = "SUIT.Build.LastFolder";

        public static string GetLastBuildFolder()
        {
            var p = EditorPrefs.GetString(Pref_LastBuildFolder, "");
            return string.IsNullOrEmpty(p) ? GetDefaultBuildFolder() : p;
        }

        public static void SetLastBuildFolder(string folder)
        {
            if (!string.IsNullOrEmpty(folder))
                EditorPrefs.SetString(Pref_LastBuildFolder, folder);
        }

        public static string GetDefaultBuildFolder()
        {
            string root = Directory.GetCurrentDirectory();
            return Path.Combine(root, "Builds", "WebGL");
        }

        public static void BuildAndAnalyze()
        {
            BuildAndAnalyze(GetLastBuildFolder(), useReleaseOptimization: false);
        }

        public static void BuildForRelease()
        {
            BuildAndAnalyze(GetLastBuildFolder(), useReleaseOptimization: true);
        }

        public static void BuildAndAnalyze(string buildFolder, bool useReleaseOptimization = false)
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
                            "Playgama Bridge",
                            "Active Build Target is not WebGL.\nSwitch to WebGL and continue?",
                            "Switch & Continue",
                            "Cancel");

                        if (!go)
                        {
                            PublishError("Cancelled: build target is not WebGL.");
                            return;
                        }

                        EditorUtility.DisplayProgressBar("Playgama Bridge", "Switching Build Target to WebGL...", 0.1f);
                        bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
                        EditorUtility.ClearProgressBar();

                        if (!switched)
                        {
                            PublishError("Failed to switch build target to WebGL.");
                            return;
                        }
                    }

                    if (useReleaseOptimization)
                    {
                        Tabs.BuildSettingsTab.TrySetCodeOptimization(Tabs.BuildSettingsTab.CodeOptimizationState.DiskSizeLTO);
                        Debug.Log("[Bridge] Using 'Disk Size with LTO' optimization for release build (smallest size, longer build time)");
                    }
                    else
                    {
                        Tabs.BuildSettingsTab.TrySetCodeOptimization(Tabs.BuildSettingsTab.CodeOptimizationState.ShorterBuildTime);
                        Debug.Log("[Bridge] Using 'Shorter Build Time' optimization for faster analysis");
                    }

                    var opts = new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = buildFolder,
                        target = BuildTarget.WebGL,
                        options = BuildOptions.DetailedBuildReport | BuildOptions.CleanBuildCache
                    };

                    if (EditorUserBuildSettings.development)
                        opts.options |= BuildOptions.Development;

                    string buildType = useReleaseOptimization ? "Release (Disk Size with LTO)" : "Analysis (Shorter Build Time)";
                    Debug.Log($"[Bridge] Starting clean WebGL build - {buildType} with options: {opts.options}");

                    var start = DateTime.UtcNow;

                    string progressMessage = useReleaseOptimization
                        ? "Building WebGL for Release (this may take a while)..."
                        : "Building WebGL for Analysis...";
                    EditorUtility.DisplayProgressBar("Playgama Bridge", progressMessage, 0.05f);
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

                    if (report != null && report.summary.result == BuildResult.Succeeded)
                    {
                        CreateBuildArchive(buildFolder);
                    }
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

        private static void CreateBuildArchive(string buildFolder)
        {
            try
            {
                if (!Directory.Exists(buildFolder))
                {
                    Debug.LogWarning("[Bridge] Cannot create archive: build folder does not exist.");
                    return;
                }

                EditorUtility.DisplayProgressBar("Playgama Bridge", "Creating ZIP archive...", 0.5f);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string folderName = Path.GetFileName(buildFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string parentFolder = Path.GetDirectoryName(buildFolder);
                string archiveName = $"{folderName}_{timestamp}.zip";
                string archivePath = Path.Combine(parentFolder, archiveName);

                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }

                ZipFile.CreateFromDirectory(buildFolder, archivePath, System.IO.Compression.CompressionLevel.Optimal, false);

                var archiveInfo = new FileInfo(archivePath);
                string sizeStr = SharedTypes.FormatBytes(archiveInfo.Length);

                Debug.Log($"[Bridge] Build archive created: {archivePath} ({sizeStr})");

                EditorUtility.RevealInFinder(archivePath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Bridge] Failed to create build archive: {ex.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void AnalyzeReport(BuildReport report, TimeSpan buildTime)
        {
            var info = new BuildInfo();
            info.statusMessage = "Analyzing build...";
            Raise(info);

            try
            {
                info.buildTargetName = "WebGL";
                info.buildTime = buildTime;

                if (report == null)
                {
                    info.buildSucceeded = false;
                    info.hasData = false;
                    info.statusMessage = "BuildReport is null. Cannot analyze.";
                    Raise(info);
                    return;
                }

                info.usedBuildReport = true;
                info.buildSucceeded = report.summary.result == BuildResult.Succeeded;
                info.totalBuildSizeBytes = SafeLong(report.summary.totalSize);

                bool packedOk = TryAnalyzePackedAssets(
                    report,
                    out var packedAssets,
                    out int packedGroups,
                    out int emptyPaths,
                    out string diag);

                if (packedOk)
                {
                    info.dataMode = BuildDataMode.PackedAssets;
                    info.assets = packedAssets;
                    info.packedGroupsCount = packedGroups;
                    info.emptyPathsCount = emptyPaths;
                    info.modeDiagnostics = diag;
                }
                else
                {
                    info.dataMode = BuildDataMode.DependenciesFallback;
                    info.assets = AnalyzeDependenciesFallback();
                    info.packedGroupsCount = 0;
                    info.emptyPathsCount = 0;
                    info.modeDiagnostics = string.IsNullOrEmpty(diag)
                        ? "PackedAssets unavailable/insufficient. Used DependenciesFallback."
                        : ("PackedAssets rejected: " + diag + " | Used DependenciesFallback.");
                }

                long tracked = 0;
                int count = 0;

                if (info.assets != null)
                {
                    count = info.assets.Count;
                    for (int i = 0; i < info.assets.Count; i++)
                    {
                        var asset = info.assets[i];
                        if (asset != null)
                            tracked += asset.sizeBytes;
                    }
                }

                info.trackedBytes = tracked;
                info.trackedAssetCount = count;
                info.hasData = count > 0;

                info.statusMessage =
                    $"Build: {(info.buildSucceeded ? "Succeeded" : report.summary.result.ToString())} | " +
                    $"Total: {SharedTypes.FormatBytes(info.totalBuildSizeBytes)} | " +
                    $"Mode: {info.dataMode} | Tracked: {count}";

                if (info.hasData)
                {
                    BuildReportStorage.SaveReport(info);
                }

                Raise(info);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                info.buildSucceeded = false;
                info.hasData = false;
                info.statusMessage = "Analyze exception: " + ex.Message;
                Raise(info);
            }
        }

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
                Debug.Log($"[Bridge] BuildReport summary: result={report.summary.result}, totalSize={report.summary.totalSize}, totalTime={report.summary.totalTime}");

                var packedAssetsArray = report.packedAssets;

                if (packedAssetsArray != null && packedAssetsArray.Length > 0)
                {
                    Debug.Log($"[Bridge] Found {packedAssetsArray.Length} packed asset groups");

                    var map = new Dictionary<string, long>(4096);
                    int groups = 0;
                    int empty = 0;
                    int zeroSize = 0;
                    int totalContents = 0;

                    foreach (var group in packedAssetsArray)
                    {
                        if (group.contents == null) continue;
                        groups++;

                        foreach (var c in group.contents)
                        {
                            totalContents++;

                            string path = c.sourceAssetPath;
                            if (string.IsNullOrEmpty(path))
                            {
                                var guid = c.sourceAssetGUID;
                                if (guid != default)
                                    path = AssetDatabase.GUIDToAssetPath(guid.ToString());
                            }

                            if (string.IsNullOrEmpty(path))
                            {
                                empty++;
                                continue;
                            }

                            if (path.StartsWith("Resources/") || path.StartsWith("Library/"))
                                continue;

                            long sz = (long)c.packedSize;
                            if (sz <= 0)
                            {
                                zeroSize++;
                                continue;
                            }

                            if (map.TryGetValue(path, out long cur))
                                map[path] = cur + sz;
                            else
                                map[path] = sz;
                        }
                    }

                    Debug.Log($"[Bridge] PackedAssets analysis: Groups={groups}, TotalContents={totalContents}, EmptyPaths={empty}, ZeroSize={zeroSize}, Mapped={map.Count}");

                    if (map.Count > 0)
                    {
                        packedGroupsCount = groups;
                        emptyPathsCount = empty;

                        foreach (var kv in map)
                        {
                            var t = AssetDatabase.GetMainAssetTypeAtPath(kv.Key);
                            assets.Add(new AssetInfo
                            {
                                path = kv.Key,
                                sizeBytes = kv.Value,
                                typeName = (t != null) ? t.Name : "Unknown",
                                category = CategorizeAsset(kv.Key, t),
                                isSizeEstimated = false
                            });
                        }

                        assets.Sort((a, b) => b.sizeBytes.CompareTo(a.sizeBytes));
                        diagnostics = $"PackedAssets OK | Groups={groups} | Contents={totalContents} | Mapped={assets.Count}";
                        Debug.Log("[Bridge] " + diagnostics);
                        return true;
                    }
                }

                Debug.Log("[Bridge] packedAssets empty, trying GetFiles() fallback...");

#if UNITY_2020_1_OR_NEWER
                var files = report.GetFiles();
                if (files != null && files.Length > 0)
                {
                    Debug.Log($"[Bridge] Found {files.Length} files in build report");

                    for (int i = 0; i < Mathf.Min(5, files.Length); i++)
                    {
                        Debug.Log($"[Bridge] File[{i}]: path={files[i].path}, role={files[i].role}, size={files[i].size}");
                    }
                }
                else
                {
                    Debug.Log("[Bridge] GetFiles() returned null or empty");
                }
#endif

                diagnostics = $"BuildReport.packedAssets is null or empty (Length={packedAssetsArray?.Length ?? 0}). WebGL builds may not provide per-asset packed sizes.";
                Debug.Log("[Bridge] " + diagnostics);
                return false;
            }
            catch (Exception ex)
            {
                diagnostics = "PackedAssets exception: " + ex.Message;
                Debug.LogError("[Bridge] " + diagnostics + "\n" + ex.StackTrace);
                return false;
            }
        }

        private static List<AssetInfo> AnalyzeDependenciesFallback()
        {
            var result = new List<AssetInfo>(4096);

            var scenes = GetEnabledBuildScenes();
            if (scenes.Length == 0) return result;

            string[] deps = null;
            try
            {
                EditorUtility.DisplayProgressBar("Playgama Bridge", "Collecting dependencies...", 0.15f);
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
                    EditorUtility.DisplayProgressBar("Playgama Bridge", "Analyzing dependencies...", idx / Mathf.Max(1f, (float)set.Count));

                if (string.IsNullOrEmpty(p)) continue;

                if (!(p.StartsWith("Assets/", StringComparison.Ordinal) || p.StartsWith("Packages/", StringComparison.Ordinal)))
                    continue;

                long size = SharedTypes.GetFileSizeForAssetPath(p);
                var t = AssetDatabase.GetMainAssetTypeAtPath(p);

                result.Add(new AssetInfo
                {
                    path = p,
                    sizeBytes = size,
                    typeName = (t != null) ? t.Name : "Unknown",
                    category = CategorizeAsset(p, t),
                    isSizeEstimated = true
                });
            }

            EditorUtility.ClearProgressBar();

            result.Sort((a, b) => b.sizeBytes.CompareTo(a.sizeBytes));
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
                buildSucceeded = false,
                hasData = false,
                statusMessage = msg
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

            if (t != null && (t.Name == "Shader" || t.Name == "ComputeShader"))
                return AssetCategory.Shaders;
            if (ext == ".shader" || ext == ".cginc" || ext == ".hlsl" || ext == ".compute")
                return AssetCategory.Shaders;

            if (t != null && (t.Name == "Font" || t.Name == "TMP_FontAsset" || t.Name == "FontAsset"))
                return AssetCategory.Fonts;
            if (ext == ".ttf" || ext == ".otf" || ext == ".fnt" || ext == ".fontsettings")
                return AssetCategory.Fonts;

            return AssetCategory.Other;
        }
    }
}
