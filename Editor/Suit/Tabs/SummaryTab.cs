using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit.Tabs
{
    public sealed class SummaryTab : ITab
    {
        public string TabName { get { return "Summary"; } }

        private BuildInfo _buildInfo;
        private Vector2 _scroll;

        // Cached aggregates to keep OnGUI fast:
        // - No re-sorting / re-aggregation every repaint
        // - Cache invalidates when BuildInfo snapshot changes (mode, counts, tracked bytes, total build size)
        private string _cacheKey = "";
        private readonly Dictionary<AssetCategory, long> _bytesByCat = new Dictionary<AssetCategory, long>();
        private readonly Dictionary<AssetCategory, int> _countByCat = new Dictionary<AssetCategory, int>();
        private readonly List<AssetInfo> _top10 = new List<AssetInfo>(10);

        private bool _anyEstimated;
        private int _nullAssetCount;

        // Foldout states for collapsible sections.
        private bool _foldStatus = true;
        private bool _foldTotals = true;
        private bool _foldBreakdown = true;
        private bool _foldTop10 = true;
        private bool _foldDiagnostics = false;
        private bool _foldSavedReports = false;

        // Saved reports cache
        private List<ReportFileInfo> _savedReports = new List<ReportFileInfo>();
        private bool _savedReportsNeedRefresh = true;

        // --- UI content (labels + tooltips) ---
        private static readonly GUIContent GC_Title = new GUIContent(
            "Summary",
            "High-level overview of the last Build & Analyze run: real build size, analysis mode, tracked assets, category breakdown, and top offenders.");

        private static readonly GUIContent GC_Header = new GUIContent(
            "Build Status",
            "Build result and target information from the latest snapshot. This block is shown even when analysis data is missing.");

        private static readonly GUIContent GC_Results = new GUIContent(
            "Totals",
            "Key numbers for the latest snapshot. Total build size is real. Tracked bytes may be estimated depending on analysis mode.");

        private static readonly GUIContent GC_Breakdown = new GUIContent(
            "Category Breakdown",
            "Tracked assets grouped by category. In DependenciesFallback mode, per-asset sizes are estimated using file size on disk.");

        private static readonly GUIContent GC_Top10 = new GUIContent(
            "Top 10 Largest Tracked Assets",
            "Largest tracked assets by tracked size. In DependenciesFallback mode the sizes are estimates (~).");

        private static readonly GUIContent GC_Diagnostics = new GUIContent(
            "Diagnostics",
            "Data quality and mode information. Use this to understand why PackedAssets mode was not used and how many entries were skipped.");

        private static readonly GUIContent GC_Ping = new GUIContent(
            "Ping",
            "Highlights the asset in the Project window (Editor ping).");

        private static readonly GUIContent GC_NoData = new GUIContent(
            "No analysis data.",
            "Run Build & Analyze from the Build Settings tab to populate the report.");

        private static readonly GUIContent GC_EstimatedNote = new GUIContent(
            "Some asset sizes are estimated.",
            "When PackedAssets mapping is unavailable, Playgama Suit falls back to dependencies and estimates per-asset sizes using file sizes on disk.\n" +
            "Total build size remains real.");

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
            InvalidateCache();
        }

        public void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                SuitStyles.DrawSectionTitle("Summary", "\u2211");

                if (_buildInfo == null)
                {
                    EditorGUILayout.HelpBox("BuildInfo is null.", MessageType.Error);
                    return;
                }

                DrawHeaderStatus();

                if (!_buildInfo.HasData || _buildInfo.Assets == null || _buildInfo.Assets.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No analysis data. Click 'Build & Analyze' or load a saved report below.",
                        MessageType.Warning);

                    // Show saved reports so user can load a previous one
                    DrawSavedReportsBlock();

                    // Diagnostics are still shown to explain what is missing.
                    DrawDiagnosticsBlock();
                    return;
                }

                EnsureCache();

                _foldTotals = SuitStyles.DrawSectionHeader("Totals", _foldTotals, "\u2139");
                if (_foldTotals)
                {
                    SuitStyles.BeginCard();
                    DrawKeyValue(
                        "Total Build Size (real)",
                        "Actual build size reported by Unity BuildReport (summary.totalSize). This is not an estimate.",
                        SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes),
                        "Real value from Unity build report.");

                    DrawKeyValue(
                        "Analysis Mode",
                        "PackedAssets is preferred if usable mappings exist. DependenciesFallback always works but per-asset sizes are estimated.",
                        _buildInfo.DataMode.ToString(),
                        "Current analysis mode used for the tracked list.");

                    DrawKeyValue(
                        "Tracked Assets",
                        "Count of assets tracked by the selected analysis mode.",
                        _buildInfo.TrackedAssetCount.ToString(),
                        "Number of tracked assets.");

                    string trackedLabel = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
                    bool isEstimated = _anyEstimated || _buildInfo.DataMode == BuildDataMode.DependenciesFallback;
                    if (isEstimated) trackedLabel += " (estimated)";

                    DrawKeyValue(
                        "Tracked Bytes",
                        "Sum of tracked asset sizes. In DependenciesFallback mode this is an estimate based on file sizes on disk.",
                        trackedLabel,
                        isEstimated ? "Estimated total (disk file sizes)." : "Tracked total from packed asset mapping (when available).");

                    if (_buildInfo.BuildTime.TotalSeconds > 0)
                    {
                        DrawKeyValue(
                            "Build Time",
                            "Build duration (if available in the snapshot).",
                            FormatTimeSpan(_buildInfo.BuildTime),
                            "Time spent by Unity building the player.");
                    }
                    SuitStyles.EndCard();
                }

                DrawBreakdownBlock();
                DrawTop10Block();
                DrawSavedReportsBlock();
                DrawDiagnosticsBlock();
            }
        }

        private void DrawHeaderStatus()
        {
            _foldStatus = SuitStyles.DrawSectionHeader("Last Build Snapshot", _foldStatus, "\u2713");
            if (_foldStatus)
            {
                SuitStyles.BeginCard();

                // Check if we have actual build data (not just a status message from "Analyzing...")
                bool hasBuildData = _buildInfo.TotalBuildSizeBytes > 0 || _buildInfo.HasData;

                if (!hasBuildData)
                {
                    // Check if build is in progress
                    if (!string.IsNullOrEmpty(_buildInfo.StatusMessage) &&
                        _buildInfo.StatusMessage.Contains("Analyzing"))
                    {
                        EditorGUILayout.LabelField("Build in progress...", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(_buildInfo.StatusMessage, SuitStyles.SubtitleStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No build has been run yet.", SuitStyles.SubtitleStyle);
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Click 'Build & Analyze' to create a snapshot.", EditorStyles.miniLabel);
                    }
                }
                else
                {
                    // Show build result with color
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(new GUIContent("Build Result", "Whether the last build step reported success."), GUILayout.Width(170));
                        GUILayout.FlexibleSpace();

                        Color prevColor = GUI.color;
                        GUI.color = _buildInfo.BuildSucceeded ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
                        GUILayout.Label(_buildInfo.BuildSucceeded ? "Success" : "Failed", EditorStyles.boldLabel);
                        GUI.color = prevColor;
                    }

                    DrawKeyValue(
                        "Target",
                        "Build target name captured in the snapshot.",
                        string.IsNullOrEmpty(_buildInfo.BuildTargetName) ? "—" : _buildInfo.BuildTargetName);

                    DrawKeyValue(
                        "Total Build Size",
                        "Total size of the build output.",
                        SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes));

                    if (_buildInfo.BuildTime.TotalSeconds > 0)
                    {
                        DrawKeyValue(
                            "Build Time",
                            "How long the build took.",
                            FormatTimeSpan(_buildInfo.BuildTime));
                    }

                    if (!string.IsNullOrEmpty(_buildInfo.StatusMessage))
                        EditorGUILayout.LabelField(_buildInfo.StatusMessage, SuitStyles.SubtitleStyle);
                }
                SuitStyles.EndCard();
            }
        }

        private void DrawBreakdownBlock()
        {
            _foldBreakdown = SuitStyles.DrawSectionHeader("Category Breakdown", _foldBreakdown, "\u2630");
            if (_foldBreakdown)
            {
                SuitStyles.BeginCard();
                DrawBreakdownRow(AssetCategory.Textures, new GUIContent(
                    "Textures", "Texture assets tracked by the analysis mode (Texture2D and related)."));

                DrawBreakdownRow(AssetCategory.Audio, new GUIContent(
                    "Audio", "Audio assets tracked by the analysis mode (AudioClip and related)."));

                DrawBreakdownRow(AssetCategory.Meshes, new GUIContent(
                    "Meshes", "Mesh data tracked by the analysis mode. Often part of model assets or mesh containers."));

                DrawBreakdownRow(AssetCategory.Models, new GUIContent(
                    "Models", "Model file assets (e.g., FBX) tracked by the analysis mode."));

                DrawBreakdownRow(AssetCategory.Shaders, new GUIContent(
                    "Shaders", "Shader assets tracked by the analysis mode."));

                DrawBreakdownRow(AssetCategory.Fonts, new GUIContent(
                    "Fonts", "Font assets including TrueType, OpenType, and TextMeshPro fonts."));

                DrawBreakdownRow(AssetCategory.Other, new GUIContent(
                    "Other", "Everything that does not fit into the main categories above."));

                if (_nullAssetCount > 0)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField($"Warning: {_nullAssetCount} AssetInfo entries were null.", SuitStyles.SubtitleStyle);
                }

                if (_anyEstimated || _buildInfo.DataMode == BuildDataMode.DependenciesFallback)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("Some asset sizes are estimated.", SuitStyles.SubtitleStyle);
                }
                SuitStyles.EndCard();
            }
        }

        private void DrawTop10Block()
        {
            _foldTop10 = SuitStyles.DrawSectionHeader($"Top 10 Largest Assets ({_top10.Count} items)", _foldTop10, "\u2B06");
            if (!_foldTop10) return;

            SuitStyles.BeginCard();
            if (_top10.Count == 0)
            {
                EditorGUILayout.LabelField("Top list is empty.", SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
                return;
            }

            for (int i = 0; i < _top10.Count; i++)
            {
                var a = _top10[i];
                if (a == null) continue;

                Rect rect = EditorGUILayout.GetControlRect(false, 24);
                rect.x += 4;
                rect.y += 2;
                rect.height -= 4;

                SuitStyles.DrawListRowBackground(rect, i, SuitStyles.CardBackground);

                float x = rect.x + 4;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, 24, rect.height),
                    new GUIContent((i + 1).ToString(), "Rank in the Top 10 list."), EditorStyles.miniBoldLabel);
                x += 28;

                // File name (extracted from path)
                string fileName = string.IsNullOrEmpty(a.Path) ? "—" : System.IO.Path.GetFileName(a.Path);
                EditorGUI.LabelField(new Rect(x, rect.y + 2, 150, rect.height),
                    new GUIContent(fileName, a.Path), EditorStyles.miniLabel);
                x += 155;

                string size = SharedTypes.FormatBytes(a.SizeBytes);
                if (a.IsSizeEstimated) size += " ~";

                EditorGUI.LabelField(new Rect(x, rect.y + 2, 90, rect.height),
                    new GUIContent(size, a.IsSizeEstimated
                        ? "Estimated asset size (disk file size)."
                        : "Tracked asset size from packed mapping (when available)."), EditorStyles.miniLabel);
                x += 95;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, 90, rect.height),
                    new GUIContent(a.Category.ToString(), "Asset category."), EditorStyles.miniLabel);

                Rect pingR = new Rect(rect.x + rect.width - 65, rect.y + 2, 60, rect.height);
                if (GUI.Button(pingR, GC_Ping))
                {
                    var obj = AssetDatabase.LoadMainAssetAtPath(a.Path);
                    if (obj != null) EditorGUIUtility.PingObject(obj);
                }
            }
            SuitStyles.EndCard();
        }

        private void DrawSavedReportsBlock()
        {
            _foldSavedReports = SuitStyles.DrawSectionHeader("Saved Reports", _foldSavedReports, "\u2630");
            if (!_foldSavedReports) return;

            SuitStyles.BeginCard();

            // Refresh button
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh List", GUILayout.Width(100)))
                {
                    _savedReportsNeedRefresh = true;
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Open Folder", GUILayout.Width(100)))
                {
                    string folderPath = BuildReportStorage.GetReportsFolderPath();
                    EditorUtility.RevealInFinder(folderPath);
                }
            }

            GUILayout.Space(8);

            // Load saved reports list
            if (_savedReportsNeedRefresh)
            {
                _savedReports = BuildReportStorage.GetSavedReports();
                _savedReportsNeedRefresh = false;
            }

            if (_savedReports.Count == 0)
            {
                EditorGUILayout.LabelField("No saved reports found.", SuitStyles.SubtitleStyle);
                EditorGUILayout.LabelField("Reports are auto-saved after each Build & Analyze.", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"{_savedReports.Count} saved report(s)", EditorStyles.miniBoldLabel);
                GUILayout.Space(4);

                // Show up to 10 most recent reports
                int maxToShow = Mathf.Min(_savedReports.Count, 10);
                for (int i = 0; i < maxToShow; i++)
                {
                    var report = _savedReports[i];
                    DrawSavedReportRow(report, i);
                }

                if (_savedReports.Count > 10)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField($"... and {_savedReports.Count - 10} more", SuitStyles.SubtitleStyle);
                }
            }

            SuitStyles.EndCard();
        }

        private void DrawSavedReportRow(ReportFileInfo report, int index)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, 28);
            SuitStyles.DrawListRowBackground(rowRect, index, SuitStyles.CardBackground);

            float x = rowRect.x + 4;

            // Date/time
            EditorGUI.LabelField(
                new Rect(x, rowRect.y + 5, 140, 18),
                new GUIContent(report.GetDisplayName(), report.FilePath),
                EditorStyles.miniLabel);
            x += 145;

            // File size
            string sizeStr = SharedTypes.FormatBytes(report.FileSize);
            EditorGUI.LabelField(
                new Rect(x, rowRect.y + 5, 70, 18),
                new GUIContent(sizeStr, "Report file size"),
                EditorStyles.miniLabel);
            x += 75;

            // Load button
            Rect loadRect = new Rect(rowRect.x + rowRect.width - 120, rowRect.y + 4, 55, 20);
            if (GUI.Button(loadRect, new GUIContent("Load", "Load this report")))
            {
                var loadedInfo = BuildReportStorage.LoadReport(report.FilePath);
                if (loadedInfo != null)
                {
                    var window = EditorWindow.GetWindow<SuitWindow>();
                    if (window != null)
                    {
                        window.LoadSavedReport(loadedInfo);
                    }
                }
            }

            // Delete button
            Rect deleteRect = new Rect(rowRect.x + rowRect.width - 60, rowRect.y + 4, 55, 20);
            if (GUI.Button(deleteRect, new GUIContent("Delete", "Delete this report")))
            {
                if (EditorUtility.DisplayDialog("Delete Report",
                    $"Delete report from {report.GetDisplayName()}?", "Delete", "Cancel"))
                {
                    BuildReportStorage.DeleteReport(report.FilePath);
                    _savedReportsNeedRefresh = true;
                }
            }
        }

        private void DrawDiagnosticsBlock()
        {
            _foldDiagnostics = SuitStyles.DrawSectionHeader("Diagnostics", _foldDiagnostics, "\u2699");
            if (_foldDiagnostics)
            {
                SuitStyles.BeginCard();
                DrawKeyValue(
                    "Mode",
                    "Active analysis mode used to build the tracked asset list.",
                    _buildInfo.DataMode.ToString());

                DrawKeyValue(
                    "Packed Groups",
                    "How many packed groups were detected in BuildReport data (if available).",
                    _buildInfo.PackedGroupsCount.ToString());

                DrawKeyValue(
                    "Empty Paths",
                    "How many packed entries lacked usable asset paths (path resolution failures).",
                    _buildInfo.EmptyPathsCount.ToString());

                DrawKeyValue(
                    "Tracked Assets",
                    "Number of tracked assets currently in the snapshot.",
                    _buildInfo.TrackedAssetCount.ToString());

                if (!string.IsNullOrEmpty(_buildInfo.ModeDiagnostics))
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField(_buildInfo.ModeDiagnostics, SuitStyles.SubtitleStyle);
                }
                SuitStyles.EndCard();
            }
        }

        private void DrawBreakdownRow(AssetCategory cat, GUIContent title)
        {
            long bytes = 0;
            int count = 0;

            _bytesByCat.TryGetValue(cat, out bytes);
            _countByCat.TryGetValue(cat, out count);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(title, GUILayout.Width(120));

                GUILayout.Label(
                    new GUIContent($"{count} items", "Number of tracked assets in this category."),
                    GUILayout.Width(90));

                GUILayout.FlexibleSpace();

                GUILayout.Label(
                    new GUIContent(SharedTypes.FormatBytes(bytes), "Sum of tracked sizes for this category."),
                    GUILayout.Width(110));
            }
        }

        private void EnsureCache()
        {
            // Cache key is derived from snapshot state that affects summary presentation.
            // If the key matches, cached aggregates remain valid.
            string key =
                (_buildInfo.DataMode.ToString()) + "|" +
                (_buildInfo.Assets != null ? _buildInfo.Assets.Count : 0) + "|" +
                _buildInfo.TrackedBytes + "|" +
                _buildInfo.TotalBuildSizeBytes;

            if (_cacheKey == key) return;

            _cacheKey = key;
            _bytesByCat.Clear();
            _countByCat.Clear();
            _top10.Clear();
            _anyEstimated = false;
            _nullAssetCount = 0;

            if (_buildInfo.Assets == null) return;

            InitBucket(AssetCategory.Textures);
            InitBucket(AssetCategory.Audio);
            InitBucket(AssetCategory.Meshes);
            InitBucket(AssetCategory.Models);
            InitBucket(AssetCategory.Shaders);
            InitBucket(AssetCategory.Fonts);
            InitBucket(AssetCategory.Other);

            // Single-pass aggregation + incremental Top-10 insertion.
            // Avoids allocations and LINQ overhead in IMGUI repaint loops.
            for (int i = 0; i < _buildInfo.Assets.Count; i++)
            {
                var a = _buildInfo.Assets[i];
                if (a == null)
                {
                    _nullAssetCount++;
                    continue;
                }

                if (a.IsSizeEstimated) _anyEstimated = true;

                var cat = a.Category;
                _bytesByCat[cat] += a.SizeBytes;
                _countByCat[cat] += 1;

                InsertTop10(a);
            }
        }

        private void InitBucket(AssetCategory cat)
        {
            _bytesByCat[cat] = 0;
            _countByCat[cat] = 0;
        }

        private void InsertTop10(AssetInfo a)
        {
            // Insert into a descending list by SizeBytes.
            // Maintains up to 10 items without sorting the full asset list.
            int insert = -1;
            for (int i = 0; i < _top10.Count; i++)
            {
                if (a.SizeBytes > _top10[i].SizeBytes)
                {
                    insert = i;
                    break;
                }
            }

            if (insert < 0)
            {
                if (_top10.Count < 10) _top10.Add(a);
                return;
            }

            _top10.Insert(insert, a);
            if (_top10.Count > 10) _top10.RemoveAt(10);
        }

        private void InvalidateCache()
        {
            _cacheKey = "";
            _bytesByCat.Clear();
            _countByCat.Clear();
            _top10.Clear();
            _anyEstimated = false;
            _nullAssetCount = 0;
        }

        private static void DrawKeyValue(string key, string keyTooltip, string value, string valueTooltip = null)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(key, keyTooltip), GUILayout.Width(170));
                GUILayout.FlexibleSpace();

                string v = string.IsNullOrEmpty(value) ? "—" : value;
                string tip = string.IsNullOrEmpty(valueTooltip) ? v : valueTooltip;
                GUILayout.Label(new GUIContent(v, tip));
            }
        }

        private static string Truncate(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            if (s.Length <= maxLen) return s;
            return "…" + s.Substring(s.Length - (maxLen - 1));
        }

        private static string FormatTimeSpan(TimeSpan t)
        {
            if (t.TotalSeconds < 1) return $"{Mathf.RoundToInt((float)t.TotalMilliseconds)} ms";
            if (t.TotalMinutes < 1) return $"{t.TotalSeconds:0.0} s";
            return $"{(int)t.TotalMinutes}m {t.Seconds}s";
        }
    }
}
