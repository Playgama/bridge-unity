using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Playgama.Bridge
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
        /// Uses "Shorter Build Time" optimization for faster analysis.
        /// </summary>
        public static void BuildAndAnalyze()
        {
            BuildAndAnalyze(GetLastBuildFolder(), useReleaseOptimization: false);
        }

        /// <summary>
        /// Schedules a WebGL build optimized for release (smallest size).
        /// Uses "Disk Size with LTO" optimization - takes longer but produces smallest build.
        /// </summary>
        public static void BuildForRelease()
        {
            BuildAndAnalyze(GetLastBuildFolder(), useReleaseOptimization: true);
        }

        /// <summary>
        /// Schedules a WebGL build (DetailedBuildReport) and then analyzes the result.
        ///
        /// Notes:
        /// - The build is executed via <see cref="EditorApplication.delayCall"/> to avoid running long operations inside IMGUI.
        /// - Ensures there is at least one enabled scene in Build Settings.
        /// - Optionally switches the active build target to WebGL after a user confirmation dialog.
        /// </summary>
        /// <param name="buildFolder">Output folder for the build.</param>
        /// <param name="useReleaseOptimization">If true, uses "Disk Size with LTO" for smallest build. If false, uses "Shorter Build Time" for faster builds.</param>
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

                    // Set code optimization based on build type
                    if (useReleaseOptimization)
                    {
                        // Use Disk Size with LTO for smallest possible build
                        Tabs.BuildSettingsTab.TrySetCodeOptimization(Tabs.BuildSettingsTab.CodeOptimizationState.DiskSizeLTO);
                        Debug.Log("[Bridge] Using 'Disk Size with LTO' optimization for release build (smallest size, longer build time)");
                    }
                    else
                    {
                        // Use Shorter Build Time for faster analysis builds
                        Tabs.BuildSettingsTab.TrySetCodeOptimization(Tabs.BuildSettingsTab.CodeOptimizationState.ShorterBuildTime);
                        Debug.Log("[Bridge] Using 'Shorter Build Time' optimization for faster analysis");
                    }

                    var opts = new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = buildFolder,
                        target = BuildTarget.WebGL,
                        // CleanBuildCache forces a non-incremental build, which is required for packedAssets data
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

                    // Create ZIP archive if build succeeded
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

        /// <summary>
        /// Creates a ZIP archive of the build folder for easy uploading.
        /// The archive is saved next to the build folder with a timestamp.
        /// </summary>
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

                // Generate archive name with timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string folderName = Path.GetFileName(buildFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string parentFolder = Path.GetDirectoryName(buildFolder);
                string archiveName = $"{folderName}_{timestamp}.zip";
                string archivePath = Path.Combine(parentFolder, archiveName);

                // Delete existing archive if present
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }

                // Create the ZIP archive
                ZipFile.CreateFromDirectory(buildFolder, archivePath, System.IO.Compression.CompressionLevel.Optimal, false);

                // Get archive size
                var archiveInfo = new FileInfo(archivePath);
                string sizeStr = SharedTypes.FormatBytes(archiveInfo.Length);

                Debug.Log($"[Bridge] Build archive created: {archivePath} ({sizeStr})");

                // Show in explorer/finder
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

                // Auto-save the report
                if (info.HasData)
                {
                    BuildReportStorage.SaveReport(info);
                }

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
                // Log build report info for diagnostics
                Debug.Log($"[Bridge] BuildReport summary: result={report.summary.result}, totalSize={report.summary.totalSize}, totalTime={report.summary.totalTime}");

                // Try packedAssets first (preferred, gives per-asset sizes)
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
                                Path = kv.Key,
                                SizeBytes = kv.Value,
                                TypeName = (t != null) ? t.Name : "Unknown",
                                Category = CategorizeAsset(kv.Key, t),
                                IsSizeEstimated = false
                            });
                        }

                        assets.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                        diagnostics = $"PackedAssets OK | Groups={groups} | Contents={totalContents} | Mapped={assets.Count}";
                        Debug.Log("[Bridge] " + diagnostics);
                        return true;
                    }
                }

                // Fallback: Try to use GetFiles() to get build output file information
                Debug.Log("[Bridge] packedAssets empty, trying GetFiles() fallback...");

#if UNITY_2020_1_OR_NEWER
                var files = report.GetFiles();
                if (files != null && files.Length > 0)
                {
                    Debug.Log($"[Bridge] Found {files.Length} files in build report");

                    // Log first few files for diagnostics
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

            // Shaders
            if (t != null && (t.Name == "Shader" || t.Name == "ComputeShader"))
                return AssetCategory.Shaders;
            if (ext == ".shader" || ext == ".cginc" || ext == ".hlsl" || ext == ".compute")
                return AssetCategory.Shaders;

            // Fonts
            if (t != null && (t.Name == "Font" || t.Name == "TMP_FontAsset" || t.Name == "FontAsset"))
                return AssetCategory.Fonts;
            if (ext == ".ttf" || ext == ".otf" || ext == ".fnt" || ext == ".fontsettings")
                return AssetCategory.Fonts;

            return AssetCategory.Other;
        }
    }
}
