using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor.Tabs
{
    public sealed class AudioTab : ITab
    {
        public string TabName => "Audio";

        private BuildInfo _buildInfo;
        private readonly List<Row> _rows = new List<Row>(1024);
        private bool _needsRebuild = true;
        private bool _isRebuilding = false;
        private Vector2 _scrollPage;
        private Vector2 _scroll;
        private string _status = "";
        private string _search = "";
        private bool _onlySelected = false;

        // Batch settings
        private AudioClipLoadType _loadType = AudioClipLoadType.CompressedInMemory;
        private bool _forceToMono = true;
        private AudioCompressionFormat _format = AudioCompressionFormat.Vorbis;
        private float _quality = 0.6f;
        private bool _preload = true;

        // Foldout states
        private bool _foldHeader = true;
        private bool _foldRecommendations = true;
        private bool _foldBatch = true;
        private bool _foldList = true;

        // Issue tracking
        private int _lowQualityCount = 0;
        private int _stereoCount = 0;
        private int _optimalCount = 0;

        private sealed class Row
        {
            public string Path;
            public long SizeBytes;
            public bool IsSizeEstimated;
            public bool Selected;
            public bool ImporterFound;
            public AudioClipLoadType LoadType;
            public bool ForceToMono;
            public AudioCompressionFormat Format;
            public float Quality;
            public StatusLevel Status;
        }

        private enum StatusLevel { Green, Yellow, Red, Unknown }

        private static class UI
        {
            public static readonly GUIContent Refresh = new GUIContent(
                "Refresh",
                "Rebuild the internal audio list from the latest analysis data.\n" +
                "Use this after you changed importer settings externally or after batch apply.");

            public static readonly GUIContent SearchLabel = new GUIContent(
                "Search",
                "Filter audio assets by path (case-insensitive substring match).");

            public static readonly GUIContent OnlySelected = new GUIContent(
                "Only Selected",
                "Show only currently selected rows.\n" +
                "Useful when you want to focus on a small batch.");

            public static readonly GUIContent SelectAll = new GUIContent(
                "All",
                "Select every visible row (ignores the 'Only Selected' filter).");

            public static readonly GUIContent Deselect = new GUIContent(
                "None",
                "Clear selection for every row.");

            public static readonly GUIContent Invert = new GUIContent(
                "Invert",
                "Invert selection state for every row.");

            public static readonly GUIContent LoadType = new GUIContent(
                "Load Type",
                "How Unity loads the audio clip at runtime:\n" +
                "- Decompress On Load: larger memory, faster playback start\n" +
                "- Compressed In Memory: smaller memory, some CPU decode cost\n" +
                "- Streaming: smallest memory, continuous decoding during playback");

            public static readonly GUIContent Format = new GUIContent(
                "Format",
                "Compression codec used for the build.\n" +
                "Vorbis is usually a solid default for WebGL size/quality balance.");

            public static readonly GUIContent Quality = new GUIContent(
                "Quality",
                "Compression quality (0..1).\n" +
                "Lower = smaller file, lower quality.\n" +
                "Recommended: 0.5-0.7 for most audio.");

            public static readonly GUIContent ForceToMono = new GUIContent(
                "Force To Mono",
                "If enabled: converts stereo clips to mono on import.\n" +
                "Can reduce size by ~50%, but may reduce spatial feeling if the clip relies on stereo.");

            public static readonly GUIContent Preload = new GUIContent(
                "Preload Audio Data",
                "If enabled: loads clip data during scene load/startup.\n" +
                "Trade-off: faster playback start vs. longer loading and more upfront memory.");

            public static readonly GUIContent ApplyToSelected = new GUIContent(
                "Apply to Selected",
                "Apply the batch AudioImporter settings to all selected audio assets.\n" +
                "Then forces reimport so settings take effect.");

            public static readonly GUIContent Ping = new GUIContent(
                "Ping",
                "Highlights the asset in the Project window so you can locate it quickly.");

            public static readonly GUIContent Select = new GUIContent(
                "Sel",
                "Select the asset in the Project window (Selection.activeObject).");
        }

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
            RequestRebuild("Init");
        }

        public void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(_scrollPage))
            {
                _scrollPage = sv.scrollPosition;

                if (_buildInfo == null)
                {
                    EditorGUILayout.HelpBox("BuildInfo == null.", MessageType.Error);
                    return;
                }

                DrawHeader();

                if (!_buildInfo.HasData || _buildInfo.Assets == null || _buildInfo.Assets.Count == 0)
                {
                    EditorGUILayout.HelpBox("No analysis data yet. Run Build & Analyze first.", MessageType.Warning);
                    return;
                }

                DrawToolbar();

                if (_isRebuilding)
                {
                    EditorGUILayout.HelpBox("Rebuilding audio list...", MessageType.Info);
                    return;
                }

                EnsureRebuilt();

                DrawRecommendationsPanel();
                DrawBatchPanel();
                DrawList();

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private void DrawHeader()
        {
            _foldHeader = BridgeStyles.DrawSectionHeader("Analysis Info", _foldHeader, "\u2139");
            if (_foldHeader)
            {
                BridgeStyles.BeginCard();
                EditorGUILayout.LabelField("Analysis Mode", _buildInfo.DataMode.ToString());
                EditorGUILayout.LabelField("Tracked Assets", _buildInfo.TrackedAssetCount.ToString());

                string tb = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
                if (_buildInfo.DataMode == BuildDataMode.DependenciesFallback) tb += " (estimated)";
                EditorGUILayout.LabelField("Tracked Bytes", tb);

                GUILayout.Space(8);

                // Status summary
                EditorGUILayout.LabelField("Audio Status Summary:", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStatusBadge(BridgeStyles.StatusRed, $"{_lowQualityCount} very low quality", "Audio with quality < 0.3");
                    GUILayout.Space(10);
                    DrawStatusBadge(BridgeStyles.StatusYellow, $"{_stereoCount} stereo (could be mono)", "Consider mono for SFX");
                    GUILayout.Space(10);
                    DrawStatusBadge(BridgeStyles.StatusGreen, $"{_optimalCount} optimal", "Well-configured audio");
                    GUILayout.FlexibleSpace();
                }

                BridgeStyles.EndCard();
            }
        }

        private void DrawStatusBadge(Color color, string text, string tooltip)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(Mathf.Max(100, text.Length * 7 + 16)));
            EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, 0.25f));
            EditorGUI.LabelField(rect, new GUIContent("  " + text, tooltip), EditorStyles.miniLabel);
        }

        private void DrawRecommendationsPanel()
        {
            string status = _lowQualityCount > 0 ? $" - {_lowQualityCount} issue(s)" : " - All good!";
            _foldRecommendations = BridgeStyles.DrawSectionHeader($"WebGL Recommendations{status}", _foldRecommendations, "\u2714");
            if (!_foldRecommendations) return;

            BridgeStyles.BeginCard();

            EditorGUILayout.LabelField("Optimal WebGL Audio Settings:", EditorStyles.boldLabel);
            GUILayout.Space(4);

            DrawRecommendationRow("\u2714", "Load Type", "Compressed In Memory", "Balances memory and load time");
            DrawRecommendationRow("\u2714", "Compression", "Vorbis", "Best quality/size ratio for WebGL");
            DrawRecommendationRow("\u2714", "Quality", "0.5 - 0.7", "Good balance for most audio");
            DrawRecommendationRow("\u2714", "Force To Mono", "Yes (for SFX)", "Halves file size for non-stereo needs");

            GUILayout.Space(6);

            // Show warnings if there are issues
            if (_lowQualityCount > 0)
            {
                Rect warningRect = EditorGUILayout.GetControlRect(false, 26);
                EditorGUI.DrawRect(warningRect, new Color(0.9f, 0.5f, 0.2f, 0.2f));
                EditorGUI.LabelField(new Rect(warningRect.x + 8, warningRect.y + 4, warningRect.width - 16, 18),
                    $"\u26A0 {_lowQualityCount} audio clip(s) use very low quality (< 0.3). Consider increasing to 0.5+ for better audio.", EditorStyles.miniLabel);
            }

            BridgeStyles.EndCard();
        }

        private void DrawRecommendationRow(string icon, string setting, string value, string reason)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                Color oldColor = GUI.color;
                GUI.color = BridgeStyles.StatusGreen;
                GUILayout.Label(icon, GUILayout.Width(18));
                GUI.color = oldColor;

                GUILayout.Label(setting, EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.Label(value, GUILayout.Width(140));
                GUILayout.Label(reason, BridgeStyles.SubtitleStyle);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(UI.Refresh, EditorStyles.toolbarButton, GUILayout.Width(60)))
                    RequestRebuild("User Refresh");

                GUILayout.Space(6);
                GUILayout.Label(UI.SearchLabel, GUILayout.Width(42));
                string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(100));
                if (newSearch != _search) _search = newSearch;

                GUILayout.Space(6);
                _onlySelected = GUILayout.Toggle(_onlySelected, UI.OnlySelected, EditorStyles.toolbarButton, GUILayout.Width(90));

                GUILayout.FlexibleSpace();

                // Quick filter buttons
                if (GUILayout.Button(new GUIContent("Low Q", "Select all audio with quality < 0.3"), EditorStyles.toolbarButton, GUILayout.Width(45)))
                    SelectByStatus(StatusLevel.Red);
                if (GUILayout.Button(new GUIContent("Stereo", "Select all stereo audio that could be mono"), EditorStyles.toolbarButton, GUILayout.Width(50)))
                    SelectStereo();

                GUILayout.Space(6);

                if (GUILayout.Button(UI.SelectAll, EditorStyles.toolbarButton, GUILayout.Width(30))) SelectAll(true);
                if (GUILayout.Button(UI.Deselect, EditorStyles.toolbarButton, GUILayout.Width(40))) SelectAll(false);
                if (GUILayout.Button(UI.Invert, EditorStyles.toolbarButton, GUILayout.Width(45))) InvertSelection();
            }
        }

        private void DrawBatchPanel()
        {
            int selectedCount = GetSelectedCount();
            string headerText = selectedCount > 0
                ? $"Batch Apply - {selectedCount} audio clip(s) selected"
                : "Batch Apply - Select audio clips to apply";

            _foldBatch = BridgeStyles.DrawSectionHeader(headerText, _foldBatch, "\u2699");
            if (!_foldBatch) return;

            BridgeStyles.BeginCard();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(UI.LoadType, GUILayout.Width(65));
                _loadType = (AudioClipLoadType)EditorGUILayout.EnumPopup(_loadType, GUILayout.Width(160));

                GUILayout.Space(10);

                // Load type explanation
                string loadTypeInfo = _loadType == AudioClipLoadType.DecompressOnLoad ? "(More memory, faster start)" :
                                     _loadType == AudioClipLoadType.CompressedInMemory ? "(Recommended for WebGL)" :
                                     "(Best for large files)";
                GUILayout.Label(loadTypeInfo, BridgeStyles.SubtitleStyle);
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(UI.Format, GUILayout.Width(50));
                _format = (AudioCompressionFormat)EditorGUILayout.EnumPopup(_format, GUILayout.Width(100));

                GUILayout.Space(20);

                GUILayout.Label(UI.Quality, GUILayout.Width(50));
                _quality = EditorGUILayout.Slider(_quality, 0.0f, 1.0f, GUILayout.Width(150));

                // Quality context label with color
                string qualityLabel = _quality < 0.3f ? "(Very Low - not recommended)" :
                                     _quality < 0.5f ? "(Low quality)" :
                                     _quality <= 0.7f ? "(Balanced - recommended)" :
                                     _quality < 0.9f ? "(High quality)" : "(Maximum)";

                Color oldColor = GUI.color;
                if (_quality < 0.3f) GUI.color = new Color(1f, 0.5f, 0.3f);
                else if (_quality >= 0.5f && _quality <= 0.7f) GUI.color = new Color(0.5f, 0.9f, 0.5f);
                GUILayout.Label(qualityLabel, EditorStyles.miniLabel, GUILayout.Width(150));
                GUI.color = oldColor;

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(4);

            using (new EditorGUILayout.HorizontalScope())
            {
                _forceToMono = EditorGUILayout.ToggleLeft(UI.ForceToMono, _forceToMono, GUILayout.Width(110));

                GUILayout.Space(10);

                _preload = EditorGUILayout.ToggleLeft(UI.Preload, _preload, GUILayout.Width(140));

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                GUI.enabled = selectedCount > 0;

                Color oldBg = GUI.backgroundColor;
                if (selectedCount > 0)
                    GUI.backgroundColor = BridgeStyles.BrandPurple;

                string applyText = selectedCount > 0
                    ? $"Apply to {selectedCount} Selected Audio Clip(s)"
                    : "Select Audio Clips First";

                if (GUILayout.Button(applyText, GUILayout.Height(28), GUILayout.MinWidth(220)))
                    ApplyBatchToSelected();

                GUI.backgroundColor = oldBg;
                GUI.enabled = true;
            }

            BridgeStyles.EndCard();
        }

        private void DrawList()
        {
            int visibleCount = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (_onlySelected && !r.Selected) continue;
                if (!PassSearch(r.Path, _search)) continue;
                visibleCount++;
            }

            string headerText = $"Audio List - Showing {visibleCount} of {_rows.Count}";
            _foldList = BridgeStyles.DrawSectionHeader(headerText, _foldList, "\u266A");
            if (!_foldList) return;

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Height(350)))
            {
                _scroll = sv.scrollPosition;

                if (_rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tracked audio assets found.", MessageType.Info);
                    return;
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox("No audio matches current filter.", MessageType.Info);
                    return;
                }

                for (int i = 0; i < _rows.Count; i++)
                {
                    var r = _rows[i];
                    if (_onlySelected && !r.Selected) continue;
                    if (!PassSearch(r.Path, _search)) continue;

                    DrawRow(r, i);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Selected: {GetSelectedCount()} | Total: {_rows.Count}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRow(Row r, int index)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 26);
            rect.x += 6;
            rect.y += 2;
            rect.height -= 4;

            Color bg = StatusToColor(r.Status);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), bg);

            // Selection highlight
            if (r.Selected)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), BridgeStyles.BrandPurple);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), new Color(0.55f, 0.36f, 0.96f, 0.4f));
            }

            float availableWidth = rect.width - 12;
            float buttonWidth = 85;
            float checkboxWidth = 22;
            float contentWidth = availableWidth - buttonWidth - checkboxWidth;

            bool compactMode = contentWidth < 450;
            bool veryCompactMode = contentWidth < 300;

            float x = rect.x + 6;

            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 4, 18, rect.height - 4), r.Selected);
            x += checkboxWidth;

            string fileName = string.IsNullOrEmpty(r.Path) ? "-" : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(fileName)) fileName = "-";

            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";

            if (veryCompactMode)
            {
                float nameWidth = contentWidth * 0.65f;
                float sizeWidth = contentWidth * 0.35f;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 20), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
            }
            else if (compactMode)
            {
                float nameWidth = contentWidth * 0.28f;
                float sizeWidth = contentWidth * 0.18f;
                float ltWidth = contentWidth * 0.3f;
                float qWidth = contentWidth * 0.24f;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 22), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, ltWidth, rect.height), TruncateLoadType(r.LoadType), EditorStyles.miniLabel);
                x += ltWidth;

                // Quality with warning indicator
                string qText = $"Q {r.Quality:F2}";
                GUIStyle qStyle = new GUIStyle(EditorStyles.miniLabel);
                if (r.Quality < 0.3f)
                {
                    qStyle.normal.textColor = new Color(1f, 0.5f, 0.3f);
                    qText += " \u26A0";
                }
                EditorGUI.LabelField(new Rect(x, rect.y + 4, qWidth, rect.height), qText, qStyle);
            }
            else
            {
                float nameWidth = Mathf.Max(100, contentWidth * 0.2f);
                float sizeWidth = Mathf.Max(70, contentWidth * 0.12f);
                float ltWidth = Mathf.Max(100, contentWidth * 0.22f);
                float fmtWidth = Mathf.Max(70, contentWidth * 0.14f);
                float qWidth = Mathf.Max(60, contentWidth * 0.12f);
                float monoWidth = Mathf.Max(50, contentWidth * 0.1f);

                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 25), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, ltWidth, rect.height), r.LoadType.ToString(), EditorStyles.miniLabel);
                x += ltWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, fmtWidth, rect.height), r.Format.ToString(), EditorStyles.miniLabel);
                x += fmtWidth;

                // Quality with color coding
                string qText = $"Q {r.Quality:F2}";
                GUIStyle qStyle = new GUIStyle(EditorStyles.miniLabel);
                if (r.Quality < 0.3f)
                {
                    qStyle.normal.textColor = new Color(1f, 0.5f, 0.3f);
                    qText += " \u26A0";
                }
                EditorGUI.LabelField(new Rect(x, rect.y + 4, qWidth, rect.height), qText, qStyle);
                x += qWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, monoWidth, rect.height), r.ForceToMono ? "Mono" : "Stereo", EditorStyles.miniLabel);
            }

            Rect pingR = new Rect(rect.x + rect.width - 85, rect.y + 3, 38, rect.height - 2);
            if (GUI.Button(pingR, UI.Ping, EditorStyles.miniButton))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }

            Rect selR = new Rect(rect.x + rect.width - 43, rect.y + 3, 38, rect.height - 2);
            if (GUI.Button(selR, UI.Select, EditorStyles.miniButton))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) Selection.activeObject = obj;
            }
        }

        private static string TruncateWithEllipsis(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "-";
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen - 1) + "...";
        }

        private static string TruncateLoadType(AudioClipLoadType lt)
        {
            switch (lt)
            {
                case AudioClipLoadType.DecompressOnLoad: return "Decompress";
                case AudioClipLoadType.CompressedInMemory: return "Compressed";
                case AudioClipLoadType.Streaming: return "Stream";
                default: return lt.ToString();
            }
        }

        private StatusLevel EvaluateStatus(Row r)
        {
            if (!r.ImporterFound) return StatusLevel.Unknown;

            // Very low quality is a problem
            if (r.Quality < 0.3f) return StatusLevel.Red;

            // Stereo that could be mono (for larger files)
            if (!r.ForceToMono && r.SizeBytes > 100 * 1024) return StatusLevel.Yellow;

            // Decompress on load for large files
            if (r.LoadType == AudioClipLoadType.DecompressOnLoad && r.SizeBytes > 500 * 1024) return StatusLevel.Yellow;

            return StatusLevel.Green;
        }

        private static Color StatusToColor(StatusLevel s)
        {
            switch (s)
            {
                case StatusLevel.Red: return BridgeStyles.StatusRed;
                case StatusLevel.Yellow: return BridgeStyles.StatusYellow;
                case StatusLevel.Green: return BridgeStyles.StatusGreen;
                default: return BridgeStyles.StatusGray;
            }
        }

        private void EnsureRebuilt()
        {
            if (!_needsRebuild || _isRebuilding) return;

            _isRebuilding = true;
            _needsRebuild = false;

            EditorApplication.delayCall += () =>
            {
                try
                {
                    _rows.Clear();
                    _lowQualityCount = 0;
                    _stereoCount = 0;
                    _optimalCount = 0;

                    for (int i = 0; i < _buildInfo.Assets.Count; i++)
                    {
                        var a = _buildInfo.Assets[i];
                        if (a == null) continue;
                        if (a.Category != AssetCategory.Audio) continue;
                        if (string.IsNullOrEmpty(a.Path)) continue;

                        var imp = AssetImporter.GetAtPath(a.Path) as AudioImporter;

                        var row = new Row
                        {
                            Path = a.Path,
                            SizeBytes = a.SizeBytes,
                            IsSizeEstimated = a.IsSizeEstimated,
                            Selected = false,
                            ImporterFound = (imp != null),
                            LoadType = AudioClipLoadType.DecompressOnLoad,
                            ForceToMono = false,
                            Format = AudioCompressionFormat.PCM,
                            Quality = 1.0f
                        };

                        if (imp != null)
                        {
                            TryReadSnapshot(imp, ref row);
                        }

                        row.Status = EvaluateStatus(row);

                        // Count issues
                        if (row.Quality < 0.3f) _lowQualityCount++;
                        if (!row.ForceToMono && row.SizeBytes > 100 * 1024) _stereoCount++;
                        if (row.Status == StatusLevel.Green) _optimalCount++;

                        _rows.Add(row);
                    }

                    _rows.Sort((x, y) => y.SizeBytes.CompareTo(x.SizeBytes));
                    _status = $"Tracked audio assets: {_rows.Count}";

                    try { EditorWindow.focusedWindow?.Repaint(); } catch { }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    _status = "Rebuild failed: " + ex.Message;
                }
                finally
                {
                    _isRebuilding = false;
                    try { EditorWindow.focusedWindow?.Repaint(); } catch { }
                }
            };
        }

        private void TryReadSnapshot(AudioImporter imp, ref Row row)
        {
            try
            {
                object ss = imp.GetType().GetProperty("defaultSampleSettings")?.GetValue(imp, null);
                if (ss != null)
                {
                    Type t = ss.GetType();

                    object lt = t.GetField("loadType")?.GetValue(ss) ?? t.GetProperty("loadType")?.GetValue(ss, null);
                    object fmt = t.GetField("compressionFormat")?.GetValue(ss) ?? t.GetProperty("compressionFormat")?.GetValue(ss, null);
                    object q = t.GetField("quality")?.GetValue(ss) ?? t.GetProperty("quality")?.GetValue(ss, null);

                    if (lt is AudioClipLoadType) row.LoadType = (AudioClipLoadType)lt;
                    if (fmt is AudioCompressionFormat) row.Format = (AudioCompressionFormat)fmt;
                    if (q is float) row.Quality = (float)q;
                }

                row.ForceToMono = imp.forceToMono;
            }
            catch
            {
                // Snapshot read failed, defaults will be used
            }
        }

        private void ApplyBatchToSelected()
        {
            var paths = CollectSelectedPaths();
            if (paths.Count == 0)
            {
                _status = "No selected audio assets.";
                return;
            }

            var opt = new AudioOptimizationUtility.ApplyOptions
            {
                LoadType = _loadType,
                ForceToMono = _forceToMono,
                CompressionFormat = _format,
                Quality = Mathf.Clamp01(_quality),
                PreloadAudioData = _preload,
                KeepSampleRate = true
            };

            EditorApplication.delayCall += () =>
            {
                try
                {
                    Undo.SetCurrentGroupName("Bridge AudioImporter Batch Apply");

                    AssetDatabase.StartAssetEditing();

                    int changed = 0;
                    int skipped = 0;

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (i % 8 == 0)
                            EditorUtility.DisplayProgressBar(
                                "Playgama Bridge",
                                "Applying audio importer settings...",
                                i / Mathf.Max(1f, (float)paths.Count));

                        string path = paths[i];

                        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
                        if (imp == null) { skipped++; continue; }

                        Undo.RecordObject(imp, "Bridge AudioImporter Change");

                        bool any = AudioOptimizationUtility.ApplyToImporter(imp, opt, out _);

                        if (any)
                        {
                            EditorUtility.SetDirty(imp);
                            changed++;
                        }
                    }

                    AssetDatabase.StopAssetEditing();
                    EditorUtility.ClearProgressBar();

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (i % 8 == 0)
                            EditorUtility.DisplayProgressBar(
                                "Playgama Bridge",
                                "Reimporting audio assets...",
                                i / Mathf.Max(1f, (float)paths.Count));

                        AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
                    }

                    EditorUtility.ClearProgressBar();

                    _status = $"Batch applied. Changed: {changed}, Skipped: {skipped}.";
                    RequestRebuild("After Batch Apply");
                }
                catch (Exception ex)
                {
                    try { AssetDatabase.StopAssetEditing(); } catch { }
                    EditorUtility.ClearProgressBar();
                    UnityEngine.Debug.LogException(ex);
                    _status = "Apply failed: " + ex.Message;
                }
            };
        }

        private void RequestRebuild(string reason)
        {
            _needsRebuild = true;
            _status = "Rebuild requested: " + reason;
        }

        private void SelectAll(bool v)
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = v;
        }

        private void SelectByStatus(StatusLevel status)
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = (_rows[i].Status == status);
        }

        private void SelectStereo()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = (!_rows[i].ForceToMono && _rows[i].SizeBytes > 100 * 1024);
        }

        private void InvertSelection()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = !_rows[i].Selected;
        }

        private int GetSelectedCount()
        {
            int c = 0;
            for (int i = 0; i < _rows.Count; i++) if (_rows[i].Selected) c++;
            return c;
        }

        private List<string> CollectSelectedPaths()
        {
            var list = new List<string>(256);
            for (int i = 0; i < _rows.Count; i++)
            {
                if (!_rows[i].Selected) continue;
                if (string.IsNullOrEmpty(_rows[i].Path)) continue;
                list.Add(_rows[i].Path);
            }
            return list;
        }

        private static bool PassSearch(string path, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
