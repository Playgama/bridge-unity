using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor.Tabs
{
    public sealed class SummaryTab : ITab
    {
        public string TabName => "Summary";

        private BuildInfo _buildInfo;
        private Vector2 _scroll;

        private string _cacheKey = "";
        private readonly Dictionary<AssetCategory, long> _bytesByCat = new Dictionary<AssetCategory, long>();
        private readonly Dictionary<AssetCategory, int> _countByCat = new Dictionary<AssetCategory, int>();
        private readonly List<AssetInfo> _top10 = new List<AssetInfo>(10);

        private bool _anyEstimated;
        private int _nullAssetCount;

        private bool _foldStatus = true;
        private bool _foldTotals = true;
        private bool _foldBreakdown = true;
        private bool _foldTop10 = true;
        private bool _foldSuggestions = true;
        private bool _foldDiagnostics = false;
        private bool _foldSavedReports = false;

        private List<ReportFileInfo> _savedReports = new List<ReportFileInfo>();
        private bool _savedReportsNeedRefresh = true;

        // Sorting state
        private enum SortMode { Size, Name, Category }
        private SortMode _sortMode = SortMode.Size;
        private bool _sortAscending = false;

        // Suggestions cache
        private List<string> _suggestions = new List<string>();

        private static readonly GUIContent GC_Ping = new GUIContent(
            "Ping",
            "Highlights the asset in the Project window so you can locate and select it quickly.");

        private static readonly GUIContent GC_Select = new GUIContent(
            "Select",
            "Selects the asset in the Project window (Selection.activeObject).");

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

                BridgeStyles.DrawSectionTitle("Summary", "\u2211");

                if (_buildInfo == null)
                {
                    EditorGUILayout.HelpBox("BuildInfo is null.", MessageType.Error);
                    return;
                }

                DrawHeaderStatus();

                if (!_buildInfo.hasData || _buildInfo.assets == null || _buildInfo.assets.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No analysis data. Click 'Build & Analyze' or load a saved report below.",
                        MessageType.Warning);

                    DrawSavedReportsBlock();
                    DrawDiagnosticsBlock();
                    return;
                }

                EnsureCache();
                GenerateSuggestions();

                DrawTotalsBlock();
                DrawSuggestionsBlock();
                DrawBreakdownBlock();
                DrawTop10Block();
                DrawSavedReportsBlock();
                DrawDiagnosticsBlock();
            }
        }

        private void DrawHeaderStatus()
        {
            // Build inline summary for header
            string headerSummary = "";
            if (_buildInfo.totalBuildSizeBytes > 0)
            {
                headerSummary = $" - {SharedTypes.FormatBytes(_buildInfo.totalBuildSizeBytes)}";
                if (_buildInfo.buildSucceeded)
                    headerSummary += " \u2714";
            }

            _foldStatus = BridgeStyles.DrawSectionHeader($"Last Build Snapshot{headerSummary}", _foldStatus, "\u2713");
            if (_foldStatus)
            {
                BridgeStyles.BeginCard();

                bool hasBuildData = _buildInfo.totalBuildSizeBytes > 0 || _buildInfo.hasData;

                if (!hasBuildData)
                {
                    if (!string.IsNullOrEmpty(_buildInfo.statusMessage) &&
                        _buildInfo.statusMessage.Contains("Analyzing"))
                    {
                        EditorGUILayout.LabelField("Build in progress...", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(_buildInfo.statusMessage, BridgeStyles.subtitleStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No build has been run yet.", BridgeStyles.subtitleStyle);
                        EditorGUILayout.Space(4);
                        EditorGUILayout.LabelField("Click 'Build & Analyze' to create a snapshot.", EditorStyles.miniLabel);
                    }
                }
                else
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(new GUIContent("Build Result", "Whether the last build step reported success."), GUILayout.Width(170));
                        GUILayout.FlexibleSpace();

                        Color prevColor = GUI.color;
                        GUI.color = _buildInfo.buildSucceeded ? BridgeStyles.statusGreen : BridgeStyles.statusRed;
                        GUILayout.Label(_buildInfo.buildSucceeded ? "\u2714 Success" : "\u2718 Failed", EditorStyles.boldLabel);
                        GUI.color = prevColor;
                    }

                    DrawKeyValue(
                        "Target",
                        "Build target name captured in the snapshot.",
                        string.IsNullOrEmpty(_buildInfo.buildTargetName) ? "—" : _buildInfo.buildTargetName);

                    DrawKeyValue(
                        "Total Build Size",
                        "Total size of the build output.",
                        SharedTypes.FormatBytes(_buildInfo.totalBuildSizeBytes));

                    if (_buildInfo.buildTime.TotalSeconds > 0)
                    {
                        DrawKeyValue(
                            "Build Time",
                            "How long the build took.",
                            FormatTimeSpan(_buildInfo.buildTime));
                    }

                    if (!string.IsNullOrEmpty(_buildInfo.statusMessage))
                        EditorGUILayout.LabelField(_buildInfo.statusMessage, BridgeStyles.subtitleStyle);
                }
                BridgeStyles.EndCard();
            }
        }

        private void DrawTotalsBlock()
        {
            // Inline summary for header
            string totalsSummary = $" - {SharedTypes.FormatBytes(_buildInfo.totalBuildSizeBytes)} across {_buildInfo.trackedAssetCount} assets";

            _foldTotals = BridgeStyles.DrawSectionHeader($"Totals{totalsSummary}", _foldTotals, "\u2139");
            if (_foldTotals)
            {
                BridgeStyles.BeginCard();
                DrawKeyValue(
                    "Total Build Size (real)",
                    "Actual build size reported by Unity BuildReport (summary.totalSize). This is not an estimate.",
                    SharedTypes.FormatBytes(_buildInfo.totalBuildSizeBytes),
                    "Real value from Unity build report.");

                DrawKeyValue(
                    "Analysis Mode",
                    "PackedAssets is preferred if usable mappings exist. DependenciesFallback always works but per-asset sizes are estimated.",
                    _buildInfo.dataMode.ToString(),
                    "Current analysis mode used for the tracked list.");

                DrawKeyValue(
                    "Tracked Assets",
                    "Count of assets tracked by the selected analysis mode.",
                    _buildInfo.trackedAssetCount.ToString(),
                    "Number of tracked assets.");

                string trackedLabel = SharedTypes.FormatBytes(_buildInfo.trackedBytes);
                bool isEstimated = _anyEstimated || _buildInfo.dataMode == BuildDataMode.DependenciesFallback;
                if (isEstimated) trackedLabel += " (estimated)";

                DrawKeyValue(
                    "Tracked Bytes",
                    "Sum of tracked asset sizes. In DependenciesFallback mode this is an estimate based on file sizes on disk.",
                    trackedLabel,
                    isEstimated ? "Estimated total (disk file sizes)." : "Tracked total from packed asset mapping (when available).");

                if (_buildInfo.buildTime.TotalSeconds > 0)
                {
                    DrawKeyValue(
                        "Build Time",
                        "Build duration (if available in the snapshot).",
                        FormatTimeSpan(_buildInfo.buildTime),
                        "Time spent by Unity building the player.");
                }
                BridgeStyles.EndCard();
            }
        }

        private void GenerateSuggestions()
        {
            _suggestions.Clear();

            if (_buildInfo == null || !_buildInfo.hasData)
                return;

            long totalBytes = _buildInfo.totalBuildSizeBytes;
            if (totalBytes <= 0) totalBytes = 1; // Prevent division by zero

            // Check font percentage
            long fontBytes = 0;
            _bytesByCat.TryGetValue(AssetCategory.Fonts, out fontBytes);
            float fontPercent = (fontBytes * 100f) / totalBytes;
            if (fontPercent > 20)
            {
                _suggestions.Add($"\u26A0 Fonts account for {fontPercent:F0}% of your build. Consider using TMP Font Asset Creator to strip unused characters.");
            }

            // Check texture percentage
            long textureBytes = 0;
            _bytesByCat.TryGetValue(AssetCategory.Textures, out textureBytes);
            float texturePercent = (textureBytes * 100f) / totalBytes;
            if (texturePercent > 50)
            {
                _suggestions.Add($"\u26A0 Textures account for {texturePercent:F0}% of your build. Consider reducing max sizes or enabling crunch compression.");
            }

            // Check audio percentage
            long audioBytes = 0;
            _bytesByCat.TryGetValue(AssetCategory.Audio, out audioBytes);
            float audioPercent = (audioBytes * 100f) / totalBytes;
            if (audioPercent > 30)
            {
                _suggestions.Add($"\u26A0 Audio accounts for {audioPercent:F0}% of your build. Consider using compressed audio and lower quality settings.");
            }

            // Check for large individual assets
            if (_top10.Count > 0 && _top10[0].sizeBytes > 2 * 1024 * 1024)
            {
                var largest = _top10[0];
                float largestPercent = (largest.sizeBytes * 100f) / totalBytes;
                _suggestions.Add($"\u26A0 '{System.IO.Path.GetFileName(largest.path)}' alone is {SharedTypes.FormatBytes(largest.sizeBytes)} ({largestPercent:F0}% of build). Consider optimizing it.");
            }

            // Check build size against common limits
            if (totalBytes > 50 * 1024 * 1024)
            {
                _suggestions.Add($"\u26A0 Build size exceeds 50MB. Many web platforms have stricter limits. Consider optimization.");
            }
            else if (totalBytes > 30 * 1024 * 1024)
            {
                _suggestions.Add($"\u2139 Build size is above 30MB. Consider optimization for better load times on slower connections.");
            }

            // Development build check
            if (EditorUserBuildSettings.development)
            {
                _suggestions.Add($"\u2139 Development Build is enabled. Release builds are typically smaller.");
            }

            // If no suggestions
            if (_suggestions.Count == 0)
            {
                _suggestions.Add("\u2714 No major issues detected. Your build looks well-optimized!");
            }
        }

        private void DrawSuggestionsBlock()
        {
            string suggestionCount = _suggestions.Count > 0 && !_suggestions[0].StartsWith("\u2714")
                ? $" - {_suggestions.Count} suggestion(s)"
                : " - All good!";

            _foldSuggestions = BridgeStyles.DrawSectionHeader($"Suggestions{suggestionCount}", _foldSuggestions, "\u2728");
            if (!_foldSuggestions) return;

            BridgeStyles.BeginCard();

            foreach (var suggestion in _suggestions)
            {
                Rect rowRect = EditorGUILayout.GetControlRect(false, 24);

                // Determine color based on icon
                Color bgColor;
                if (suggestion.StartsWith("\u26A0"))
                    bgColor = new Color(0.8f, 0.6f, 0.2f, 0.15f);
                else if (suggestion.StartsWith("\u2714"))
                    bgColor = new Color(0.2f, 0.7f, 0.3f, 0.15f);
                else
                    bgColor = new Color(0.4f, 0.5f, 0.8f, 0.15f);

                EditorGUI.DrawRect(rowRect, bgColor);

                GUIStyle suggestionStyle = new GUIStyle(EditorStyles.label);
                suggestionStyle.wordWrap = true;
                suggestionStyle.richText = false;

                EditorGUI.LabelField(new Rect(rowRect.x + 8, rowRect.y + 3, rowRect.width - 16, rowRect.height - 6),
                    suggestion, suggestionStyle);
            }

            BridgeStyles.EndCard();
        }

        private void DrawBreakdownBlock()
        {
            // Calculate summary for header
            int totalCategories = 0;
            foreach (var kvp in _countByCat)
                if (kvp.Value > 0) totalCategories++;

            string breakdownSummary = $" - {totalCategories} categories";

            _foldBreakdown = BridgeStyles.DrawSectionHeader($"Category Breakdown{breakdownSummary}", _foldBreakdown, "\u2630");
            if (_foldBreakdown)
            {
                BridgeStyles.BeginCard();

                // Calculate max bytes for bar scaling
                long maxBytes = 1;
                foreach (var kvp in _bytesByCat)
                    if (kvp.Value > maxBytes) maxBytes = kvp.Value;

                DrawBreakdownRowWithBar(AssetCategory.Textures, "Textures", "Texture assets (Texture2D, Sprites)", maxBytes);
                DrawBreakdownRowWithBar(AssetCategory.Audio, "Audio", "Audio clips and audio-related assets", maxBytes);
                DrawBreakdownRowWithBar(AssetCategory.Meshes, "Meshes", "Mesh data from models or standalone meshes", maxBytes);
                DrawBreakdownRowWithBar(AssetCategory.Models, "Models", "Model files (FBX, OBJ, etc.)", maxBytes);
                DrawBreakdownRowWithBar(AssetCategory.Shaders, "Shaders", "Shader programs", maxBytes);
                DrawBreakdownRowWithBar(AssetCategory.Fonts, "Fonts", "Font assets including TextMeshPro", maxBytes);
                DrawBreakdownRowWithBar(AssetCategory.Other, "Other", "Other asset types", maxBytes);

                if (_nullAssetCount > 0)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField($"Warning: {_nullAssetCount} AssetInfo entries were null.", BridgeStyles.subtitleStyle);
                }

                if (_anyEstimated || _buildInfo.dataMode == BuildDataMode.DependenciesFallback)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("Note: Some asset sizes are estimated (~).", BridgeStyles.subtitleStyle);
                }
                BridgeStyles.EndCard();
            }
        }

        private void DrawBreakdownRowWithBar(AssetCategory cat, string title, string tooltip, long maxBytes)
        {
            long bytes = 0;
            int count = 0;

            _bytesByCat.TryGetValue(cat, out bytes);
            _countByCat.TryGetValue(cat, out count);

            Rect rowRect = EditorGUILayout.GetControlRect(false, 26);

            // Title
            Rect titleRect = new Rect(rowRect.x + 4, rowRect.y + 4, 80, 18);
            EditorGUI.LabelField(titleRect, new GUIContent(title, tooltip), EditorStyles.label);

            // Bar background
            float barStartX = rowRect.x + 90;
            float barWidth = rowRect.width - 220;
            Rect barBgRect = new Rect(barStartX, rowRect.y + 6, barWidth, 14);
            EditorGUI.DrawRect(barBgRect, new Color(0.2f, 0.2f, 0.22f));

            // Bar fill
            float fillPercent = maxBytes > 0 ? (float)bytes / maxBytes : 0;
            if (fillPercent > 0)
            {
                Rect barFillRect = new Rect(barStartX, rowRect.y + 6, barWidth * fillPercent, 14);
                EditorGUI.DrawRect(barFillRect, BridgeStyles.brandPurple);
            }

            // Count
            Rect countRect = new Rect(rowRect.x + rowRect.width - 125, rowRect.y + 4, 55, 18);
            EditorGUI.LabelField(countRect, $"{count} items", EditorStyles.miniLabel);

            // Size
            Rect sizeRect = new Rect(rowRect.x + rowRect.width - 70, rowRect.y + 4, 65, 18);
            EditorGUI.LabelField(sizeRect, SharedTypes.FormatBytes(bytes), EditorStyles.miniLabel);
        }

        private void DrawTop10Block()
        {
            _foldTop10 = BridgeStyles.DrawSectionHeader($"Top 10 Largest Assets", _foldTop10, "\u2B06");
            if (!_foldTop10) return;

            BridgeStyles.BeginCard();

            // Sorting controls
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Sort by:", GUILayout.Width(50));

                if (GUILayout.Toggle(_sortMode == SortMode.Size, "Size", EditorStyles.miniButton, GUILayout.Width(50)))
                    _sortMode = SortMode.Size;
                if (GUILayout.Toggle(_sortMode == SortMode.Name, "Name", EditorStyles.miniButton, GUILayout.Width(50)))
                    _sortMode = SortMode.Name;
                if (GUILayout.Toggle(_sortMode == SortMode.Category, "Type", EditorStyles.miniButton, GUILayout.Width(50)))
                    _sortMode = SortMode.Category;

                GUILayout.Space(10);

                if (GUILayout.Button(_sortAscending ? "\u25B2 Asc" : "\u25BC Desc", EditorStyles.miniButton, GUILayout.Width(55)))
                    _sortAscending = !_sortAscending;

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(8);

            if (_top10.Count == 0)
            {
                EditorGUILayout.LabelField("Top list is empty.", BridgeStyles.subtitleStyle);
                BridgeStyles.EndCard();
                return;
            }

            // Sort the list
            var sortedList = SortTop10();

            // Calculate max size for percentage bar
            long maxSize = sortedList.Max(a => a.sizeBytes);
            long totalSize = _buildInfo.totalBuildSizeBytes > 0 ? _buildInfo.totalBuildSizeBytes : sortedList.Sum(a => a.sizeBytes);

            for (int i = 0; i < sortedList.Count; i++)
            {
                var a = sortedList[i];
                if (a == null) continue;

                DrawTop10Row(a, i, maxSize, totalSize);
            }

            BridgeStyles.EndCard();
        }

        private List<AssetInfo> SortTop10()
        {
            var sorted = new List<AssetInfo>(_top10);

            switch (_sortMode)
            {
                case SortMode.Size:
                    sorted.Sort((x, y) => _sortAscending ? x.sizeBytes.CompareTo(y.sizeBytes) : y.sizeBytes.CompareTo(x.sizeBytes));
                    break;
                case SortMode.Name:
                    sorted.Sort((x, y) =>
                    {
                        string nameX = System.IO.Path.GetFileName(x.path ?? "");
                        string nameY = System.IO.Path.GetFileName(y.path ?? "");
                        return _sortAscending ? nameX.CompareTo(nameY) : nameY.CompareTo(nameX);
                    });
                    break;
                case SortMode.Category:
                    sorted.Sort((x, y) => _sortAscending ? x.category.CompareTo(y.category) : y.category.CompareTo(x.category));
                    break;
            }

            return sorted;
        }

        private void DrawTop10Row(AssetInfo a, int index, long maxSize, long totalSize)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 36);
            rect.x += 4;
            rect.y += 2;
            rect.height -= 4;

            BridgeStyles.DrawListRowBackground(rect, index, BridgeStyles.cardBackground);

            float x = rect.x + 4;

            // Rank
            EditorGUI.LabelField(new Rect(x, rect.y + 2, 24, 16),
                new GUIContent((index + 1).ToString(), "Rank in the list."), EditorStyles.miniBoldLabel);
            x += 28;

            // File name
            string fileName = string.IsNullOrEmpty(a.path) ? "—" : System.IO.Path.GetFileName(a.path);
            float nameWidth = 180;
            EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, 16),
                new GUIContent(Truncate(fileName, 24), a.path), EditorStyles.miniLabel);

            // Size and percentage
            string size = SharedTypes.FormatBytes(a.sizeBytes);
            if (a.isSizeEstimated) size += " ~";

            float percent = totalSize > 0 ? (a.sizeBytes * 100f / totalSize) : 0;
            string percentStr = $"({percent:F1}%)";

            EditorGUI.LabelField(new Rect(x, rect.y + 18, nameWidth, 14),
                new GUIContent($"{size} {percentStr}", a.isSizeEstimated ? "Estimated size" : "Actual packed size"),
                BridgeStyles.subtitleStyle);
            x += nameWidth + 10;

            // Percentage bar
            float barWidth = rect.width - nameWidth - 180;
            if (barWidth > 50)
            {
                Rect barBgRect = new Rect(x, rect.y + 10, barWidth, 12);
                EditorGUI.DrawRect(barBgRect, new Color(0.2f, 0.2f, 0.22f));

                float fillPercent = maxSize > 0 ? (float)a.sizeBytes / maxSize : 0;
                if (fillPercent > 0)
                {
                    Color barColor = fillPercent > 0.7f ? BridgeStyles.statusYellow :
                                     fillPercent > 0.3f ? BridgeStyles.brandPurple : BridgeStyles.statusGreen;
                    Rect barFillRect = new Rect(x, rect.y + 10, barWidth * fillPercent, 12);
                    EditorGUI.DrawRect(barFillRect, barColor);
                }
                x += barWidth + 10;
            }

            // Category badge
            Rect catRect = new Rect(rect.x + rect.width - 130, rect.y + 8, 55, 16);
            EditorGUI.DrawRect(catRect, new Color(0.3f, 0.3f, 0.35f));
            GUIStyle catStyle = new GUIStyle(EditorStyles.miniLabel);
            catStyle.alignment = TextAnchor.MiddleCenter;
            EditorGUI.LabelField(catRect, a.category.ToString(), catStyle);

            // Buttons
            Rect pingR = new Rect(rect.x + rect.width - 70, rect.y + 8, 32, 18);
            if (GUI.Button(pingR, GC_Ping, EditorStyles.miniButton))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(a.path);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }

            Rect selectR = new Rect(rect.x + rect.width - 35, rect.y + 8, 32, 18);
            if (GUI.Button(selectR, GC_Select, EditorStyles.miniButton))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(a.path);
                if (obj != null) Selection.activeObject = obj;
            }
        }

        private void DrawSavedReportsBlock()
        {
            if (_savedReportsNeedRefresh)
            {
                _savedReports = BuildReportStorage.GetSavedReports();
                _savedReportsNeedRefresh = false;
            }

            string reportsSummary = _savedReports.Count > 0 ? $" - {_savedReports.Count} saved" : " - None";

            _foldSavedReports = BridgeStyles.DrawSectionHeader($"Saved Reports{reportsSummary}", _foldSavedReports, "\u2630");
            if (!_foldSavedReports) return;

            BridgeStyles.BeginCard();

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

            if (_savedReports.Count == 0)
            {
                EditorGUILayout.LabelField("No saved reports found.", BridgeStyles.subtitleStyle);
                EditorGUILayout.LabelField("Reports are auto-saved after each Build & Analyze.", EditorStyles.miniLabel);
            }
            else
            {
                int maxToShow = Mathf.Min(_savedReports.Count, 10);
                for (int i = 0; i < maxToShow; i++)
                {
                    var report = _savedReports[i];
                    DrawSavedReportRow(report, i);
                }

                if (_savedReports.Count > 10)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField($"... and {_savedReports.Count - 10} more", BridgeStyles.subtitleStyle);
                }
            }

            BridgeStyles.EndCard();
        }

        private void DrawSavedReportRow(ReportFileInfo report, int index)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, 28);
            BridgeStyles.DrawListRowBackground(rowRect, index, BridgeStyles.cardBackground);

            float x = rowRect.x + 4;

            EditorGUI.LabelField(
                new Rect(x, rowRect.y + 5, 140, 18),
                new GUIContent(report.GetDisplayName(), report.FilePath),
                EditorStyles.miniLabel);
            x += 145;

            string sizeStr = SharedTypes.FormatBytes(report.FileSize);
            EditorGUI.LabelField(
                new Rect(x, rowRect.y + 5, 70, 18),
                new GUIContent(sizeStr, "Report file size"),
                EditorStyles.miniLabel);
            x += 75;

            Rect loadRect = new Rect(rowRect.x + rowRect.width - 120, rowRect.y + 4, 55, 20);
            if (GUI.Button(loadRect, new GUIContent("Load", "Load this report into the current view")))
            {
                var loadedInfo = BuildReportStorage.LoadReport(report.FilePath);
                if (loadedInfo != null)
                {
                    var window = EditorWindow.GetWindow<BridgeWindow>();
                    if (window != null)
                    {
                        window.LoadSavedReport(loadedInfo);
                    }
                }
            }

            Rect deleteRect = new Rect(rowRect.x + rowRect.width - 60, rowRect.y + 4, 55, 20);
            if (GUI.Button(deleteRect, new GUIContent("Delete", "Permanently delete this report file")))
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
            string diagSummary = $" - {_buildInfo.dataMode}";

            _foldDiagnostics = BridgeStyles.DrawSectionHeader($"Diagnostics{diagSummary}", _foldDiagnostics, "\u2699");
            if (_foldDiagnostics)
            {
                BridgeStyles.BeginCard();
                DrawKeyValue(
                    "Mode",
                    "Active analysis mode used to build the tracked asset list.",
                    _buildInfo.dataMode.ToString());

                DrawKeyValue(
                    "Packed Groups",
                    "How many packed groups were detected in BuildReport data (if available).",
                    _buildInfo.packedGroupsCount.ToString());

                DrawKeyValue(
                    "Empty Paths",
                    "How many packed entries lacked usable asset paths (path resolution failures).",
                    _buildInfo.emptyPathsCount.ToString());

                DrawKeyValue(
                    "Tracked Assets",
                    "Number of tracked assets currently in the snapshot.",
                    _buildInfo.trackedAssetCount.ToString());

                if (!string.IsNullOrEmpty(_buildInfo.modeDiagnostics))
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField(_buildInfo.modeDiagnostics, BridgeStyles.subtitleStyle);
                }
                BridgeStyles.EndCard();
            }
        }

        private void EnsureCache()
        {
            string key =
                (_buildInfo.dataMode.ToString()) + "|" +
                (_buildInfo.assets != null ? _buildInfo.assets.Count : 0) + "|" +
                _buildInfo.trackedBytes + "|" +
                _buildInfo.totalBuildSizeBytes;

            if (_cacheKey == key) return;

            _cacheKey = key;
            _bytesByCat.Clear();
            _countByCat.Clear();
            _top10.Clear();
            _anyEstimated = false;
            _nullAssetCount = 0;

            if (_buildInfo.assets == null) return;

            InitBucket(AssetCategory.Textures);
            InitBucket(AssetCategory.Audio);
            InitBucket(AssetCategory.Meshes);
            InitBucket(AssetCategory.Models);
            InitBucket(AssetCategory.Shaders);
            InitBucket(AssetCategory.Fonts);
            InitBucket(AssetCategory.Other);

            for (int i = 0; i < _buildInfo.assets.Count; i++)
            {
                var a = _buildInfo.assets[i];
                if (a == null)
                {
                    _nullAssetCount++;
                    continue;
                }

                if (a.isSizeEstimated) _anyEstimated = true;

                var cat = a.category;
                _bytesByCat[cat] += a.sizeBytes;
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
            int insert = -1;
            for (int i = 0; i < _top10.Count; i++)
            {
                if (a.sizeBytes > _top10[i].sizeBytes)
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
            _suggestions.Clear();
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
