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
        
        private static readonly string BUILD_SETTINGS_FILE = Path.Combine(Application.dataPath, "../Library/PlaygamaBridge.buildsettings");
        
        public static bool IsUnity6OrNewer
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return true;
#else
                return false;
#endif
            }
        }

        public static string GetLastBuildFolder()
        {
            // Try to read from project-specific file
            try
            {
                string fullPath = Path.GetFullPath(BUILD_SETTINGS_FILE);
                if (File.Exists(fullPath))
                {
                    string savedPath = File.ReadAllText(fullPath).Trim();
                    if (!string.IsNullOrEmpty(savedPath))
                        return savedPath;
                }
            }
            catch
            {
                // Ignore errors, use default
            }

            return GetDefaultBuildFolder();
        }

        public static void SetLastBuildFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder))
                return;

            try
            {
                string fullPath = Path.GetFullPath(BUILD_SETTINGS_FILE);
                File.WriteAllText(fullPath, folder);
            }
            catch
            {
                // Ignore errors
            }
        }

        public static string GetDefaultBuildFolder()
        {
            string root = Directory.GetCurrentDirectory();
            return Path.Combine(root, "Builds", "WebGL");
        }
        
        public static void AnalyzeOnly()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var scenes = GetEnabledBuildScenes();
                    if (scenes.Length == 0)
                    {
                        PublishError("No enabled scenes in Build Settings.");
                        return;
                    }


                    var start = DateTime.UtcNow;
                    var assets = AnalyzeDependenciesFallback();
                    var elapsed = DateTime.UtcNow - start;

                    long tracked = 0;
                    if (assets != null)
                    {
                        foreach (var asset in assets)
                        {
                            if (asset != null)
                                tracked += asset.sizeBytes;
                        }
                    }

                    var info = new BuildInfo
                    {
                        buildTargetName = "WebGL",
                        buildTime = elapsed,
                        buildSucceeded = true,
                        hasData = assets != null && assets.Count > 0,
                        usedBuildReport = false,
                        dataMode = BuildDataMode.DependenciesFallback,
                        assets = assets,
                        trackedBytes = tracked,
                        trackedAssetCount = assets?.Count ?? 0,
                        totalBuildSizeBytes = tracked,
                        modeDiagnostics = "Analysis only (no build). Showing source file sizes.",
                        statusMessage = $"Analysis complete | Assets: {assets?.Count ?? 0} | Estimated size: {SharedTypes.FormatBytes(tracked)}"
                    };

                    if (info.hasData)
                    {
                        BuildReportStorage.SaveReport(info);
                    }

                    Raise(info);
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    PublishError("Analysis exception: " + ex.Message);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            };
        }

        /// <summary>
        /// Build and analyze. On Unity 6+, uses DetailedBuildReport for accurate sizes.
        /// On older Unity, falls back to analysis-only (no build) to avoid crashes.
        /// </summary>
        public static void BuildAndAnalyze()
        {
            if (IsUnity6OrNewer)
            {
                // Unity 6+: Safe to use DetailedBuildReport
                BuildWithReport(GetLastBuildFolder(), useReleaseOptimization: false);
            }
            else
            {
                // Unity < 6: DetailedBuildReport can crash, use analysis-only
                AnalyzeOnly();
            }
        }

        /// <summary>
        /// Build for release. On Unity 6+, uses DetailedBuildReport.
        /// On older Unity, builds without DetailedBuildReport (no per-asset tracking but stable).
        /// </summary>
        public static void BuildForRelease()
        {
            BuildWithReport(GetLastBuildFolder(), useReleaseOptimization: true);
        }

        /// <summary>
        /// Internal: Performs actual build with optional DetailedBuildReport.
        /// </summary>
        private static void BuildWithReport(string buildFolder, bool useReleaseOptimization)
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
                        Tabs.BuildSettingsTab.TrySetCodeOptimization(Tabs.BuildSettingsTab.CodeOptimizationState.DiskSizeLTO);
                    else
                        Tabs.BuildSettingsTab.TrySetCodeOptimization(Tabs.BuildSettingsTab.CodeOptimizationState.ShorterBuildTime);

                    // Build options: Only use DetailedBuildReport on Unity 6+ where it's stable
                    BuildOptions buildOptions = BuildOptions.None;

                    if (IsUnity6OrNewer)
                        buildOptions |= BuildOptions.DetailedBuildReport;

                    if (EditorUserBuildSettings.development)
                        buildOptions |= BuildOptions.Development;

                    var opts = new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = buildFolder,
                        target = BuildTarget.WebGL,
                        options = buildOptions
                    };

                    var start = DateTime.UtcNow;

                    string progressMessage = useReleaseOptimization
                        ? "Building WebGL for Release (this may take a while)..."
                        : "Building WebGL...";
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

                    AnalyzeReport(report, elapsed, useDetailedReport: IsUnity6OrNewer);

                    if (report != null && report.summary.result == BuildResult.Succeeded)
                    {
                        CreateBuildArchive(buildFolder);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    PublishError("Build exception: " + ex.Message);
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            };
        }

        /// <summary>
        /// Legacy method for backwards compatibility.
        /// </summary>
        public static void BuildAndAnalyze(string buildFolder, bool useReleaseOptimization = false)
        {
            if (useReleaseOptimization)
            {
                SetLastBuildFolder(buildFolder);
                BuildForRelease();
            }
            else
            {
                SetLastBuildFolder(buildFolder);
                BuildAndAnalyze();
            }
        }

        private static void CreateBuildArchive(string buildFolder)
        {
            try
            {
                if (!Directory.Exists(buildFolder))
                    return;

                EditorUtility.DisplayProgressBar("Playgama Bridge", "Creating ZIP archive...", 0.5f);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string folderName = Path.GetFileName(buildFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string parentFolder = Path.GetDirectoryName(buildFolder);
                string archiveName = $"{folderName}_{timestamp}.zip";
                string archivePath = Path.Combine(parentFolder, archiveName);

                if (File.Exists(archivePath))
                    File.Delete(archivePath);

                ZipFile.CreateFromDirectory(buildFolder, archivePath, System.IO.Compression.CompressionLevel.Optimal, false);
                EditorUtility.RevealInFinder(archivePath);
            }
            catch
            {
                // Ignore errors
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void AnalyzeReport(BuildReport report, TimeSpan buildTime, bool useDetailedReport = true)
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

                // Only try PackedAssets analysis if DetailedBuildReport was used (Unity 6+)
                // On older Unity, DetailedBuildReport causes crashes, so we skip directly to fallback
                bool packedOk = false;
                List<AssetInfo> packedAssets = null;
                int packedGroups = 0;
                int emptyPaths = 0;
                string diag = "";

                if (useDetailedReport)
                {
                    packedOk = TryAnalyzePackedAssets(
                        report,
                        out packedAssets,
                        out packedGroups,
                        out emptyPaths,
                        out diag);
                }
                else
                {
                    diag = "DetailedBuildReport skipped (Unity < 6 compatibility mode).";
                }

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
                var packedAssetsArray = report.packedAssets;

                if (packedAssetsArray != null && packedAssetsArray.Length > 0)
                {
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
                        return true;
                    }
                }

                diagnostics = $"BuildReport.packedAssets is null or empty (Length={packedAssetsArray?.Length ?? 0}). WebGL builds may not provide per-asset packed sizes.";
                return false;
            }
            catch (Exception ex)
            {
                diagnostics = "PackedAssets exception: " + ex.Message;
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
