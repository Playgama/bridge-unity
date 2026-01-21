using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
{
    /// <summary>
    /// Editor tab that displays tracked texture assets from the latest build analysis.
    /// Provides:
    /// - A sortable list (largest first) with quick status coloring (heuristics).
    /// - Selection tools (select all / invert / filter).
    /// - Batch application of common TextureImporter settings (WebGL focused).
    /// - Optional Tinify (TinyPNG) source optimization for PNG/JPG/JPEG assets.
    /// </summary>
    public sealed class TexturesTab : ITab
    {
        /// <summary>Displayed name of the tab in the parent UI.</summary>
        public string TabName => "Textures";

        private BuildInfo _buildInfo;

        /// <summary>Cached UI rows representing tracked textures.</summary>
        private readonly List<Row> _rows = new List<Row>(2048);

        /// <summary>
        /// When true, the next repaint will schedule a rebuild of the internal row cache.
        /// This prevents doing heavy work directly inside OnGUI.
        /// </summary>
        private bool _needsRebuild = true;

        /// <summary>True while a delayed rebuild is queued/executing.</summary>
        private bool _isRebuilding = false;

        /// <summary>Scroll state for the full page.</summary>
        private Vector2 _scrollPage;

        /// <summary>Scroll state for the list view.</summary>
        private Vector2 _scroll;

        /// <summary>General status messages for the tab (non-error, non-exception).</summary>
        private string _status = "";

        /// <summary>Case-insensitive filter applied to asset paths.</summary>
        private string _search = "";

        /// <summary>If true, the list view only renders rows that are currently selected.</summary>
        private bool _onlySelected = false;

        // Batch apply settings (applied to selected textures via TextureImporter).
        private int _batchMaxSize = 1024;
        private TextureImporterCompression _batchCompression = TextureImporterCompression.Compressed;
        private bool _batchCrunch = true;
        private int _batchCrunchQuality = 50;
        private bool _batchDisableReadWrite = true;

        // Tinify (TinyPNG) state and status text.
        private bool _tinifyRunning = false;
        private string _tinifyStatus = "";

        // Foldout states for collapsible sections.
        private bool _foldHeader = true;
        private bool _foldBatch = true;
        private bool _foldTinify = false;
        private bool _foldList = true;

        /// <summary>
        /// Quick presets for typical WebGL build targets.
        /// These are heuristics, not strict rules.
        /// </summary>
        private enum Preset
        {
            /// <summary>Balanced quality/size: Max 1024, Compressed, Crunch ON.</summary>
            WebGL_Balanced_1024,

            /// <summary>Aggressive size savings: Max 512, Compressed, Crunch ON.</summary>
            WebGL_Aggressive_512,

            /// <summary>Higher visual quality: Max 2048, Compressed, Crunch OFF by default.</summary>
            WebGL_HighQuality_2048
        }

        private Preset _preset = Preset.WebGL_Balanced_1024;

        /// <summary>
        /// Row model for a single tracked texture asset.
        /// Values are extracted from the analysis data (size/path) and from the importer (settings).
        /// </summary>
        private sealed class Row
        {
            /// <summary>AssetDatabase path (e.g., "Assets/Textures/foo.png").</summary>
            public string Path;

            /// <summary>Compressed/runtime size in bytes (calculated from texture format and dimensions).</summary>
            public long SizeBytes;

            /// <summary>Original source file size in bytes.</summary>
            public long SourceSizeBytes;

            /// <summary>True if the size value is an estimate rather than an exact measurement.</summary>
            public bool IsSizeEstimated;

            /// <summary>UI selection flag used by batch operations.</summary>
            public bool Selected;

            /// <summary>True if a TextureImporter was successfully resolved for this path.</summary>
            public bool ImporterFound;

            /// <summary>True if the main asset at the path is a Texture2D.</summary>
            public bool IsTexture2D;

            /// <summary>Max texture size for the WebGL platform override (or fallback maxTextureSize).</summary>
            public int WebGLMaxSize;

            /// <summary>Texture compression mode taken from TextureImporter.</summary>
            public TextureImporterCompression Compression;

            /// <summary>True if crunched compression is enabled on the importer.</summary>
            public bool Crunch;

            /// <summary>Crunch compression quality (0..100).</summary>
            public int CrunchQuality;

            /// <summary>True if Read/Write is enabled (imp.isReadable).</summary>
            public bool ReadWrite;

            /// <summary>Heuristic status used for background coloring in the list.</summary>
            public StatusLevel Status;
        }

        /// <summary>
        /// High-level severity signal for quick scanning the list.
        /// This is heuristic-based and may vary by Unity version/importer behavior.
        /// </summary>
        private enum StatusLevel
        {
            Green,
            Yellow,
            Red,
            Unknown
        }

        // Shared UI text/tooltips. Keeping these centralized reduces typo risk and keeps OnGUI readable.
        private static class UI
        {
            public static readonly GUIContent Refresh = new GUIContent(
                "Refresh",
                "Rebuild the internal texture list from the latest analysis data.\n" +
                "Use this if you changed import settings externally or after running Tinify.");

            public static readonly GUIContent SearchLabel = new GUIContent(
                "Search",
                "Filter textures by asset path (case-insensitive substring match).");

            public static readonly GUIContent OnlySelected = new GUIContent(
                "Only Selected",
                "Show only currently selected rows.\n" +
                "Useful when you're working on a small batch.");

            public static readonly GUIContent SelectAll = new GUIContent(
                "Select All",
                "Select every visible row (ignores the 'Only Selected' filter).");

            public static readonly GUIContent Deselect = new GUIContent(
                "Deselect",
                "Clear selection for every row.");

            public static readonly GUIContent Invert = new GUIContent(
                "Invert",
                "Invert selection state for every row.");

            public static readonly GUIContent Preset = new GUIContent(
                "Preset",
                "Quickly fills the batch settings below with common WebGL-oriented values.\n" +
                "You can still tweak individual options after choosing a preset.");

            public static readonly GUIContent ApplyToSelected = new GUIContent(
                "Apply to Selected",
                "Apply the batch TextureImporter settings to all selected textures.\n" +
                "Then forces reimport so settings take effect.");

            public static readonly GUIContent WebGLMaxSize = new GUIContent(
                "WebGL Max Size",
                "Sets the WebGL platform override max texture size.\n" +
                "Lower values reduce build size and memory usage, but can reduce sharpness.");

            public static readonly GUIContent Compression = new GUIContent(
                "Compression",
                "TextureImporter compression mode.\n" +
                "For WebGL builds, uncompressed textures often increase download size and memory usage.");

            public static readonly GUIContent Crunch = new GUIContent(
                "Crunch",
                "Enable crunched compression.\n" +
                "Can reduce build size; may increase import time and affect quality depending on content.");

            public static readonly GUIContent CrunchQuality = new GUIContent(
                "Quality",
                "Crunch compression quality (0..100).\n" +
                "Higher = better quality, usually bigger size.");

            public static readonly GUIContent DisableReadWrite = new GUIContent(
                "Disable Read/Write",
                "If enabled: turns off 'Read/Write' on selected textures.\n" +
                "This often saves a lot of memory on WebGL because textures don't need a CPU-readable copy.");

            public static readonly GUIContent TinifyButton = new GUIContent(
                "Tinify Selected (PNG/JPG)",
                "Optimize source PNG/JPG/JPEG files using the Tinify (TinyPNG) API.\n" +
                "WARNING: This overwrites files on disk and reimports assets. Undo is not available.");

            public static readonly GUIContent Ping = new GUIContent(
                "Ping",
                "Ping the asset in the Project window.");

            public static readonly GUIContent Select = new GUIContent(
                "Select",
                "Select the asset in the Project window (Selection.activeObject).");
        }

        /// <summary>
        /// Called by the hosting window/controller to provide analysis data.
        /// </summary>
        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
            RequestRebuild("Init");
        }

        /// <summary>
        /// Main Unity IMGUI entry point for the tab.
        /// Heavy work is scheduled via delayCall to keep UI responsive.
        /// </summary>
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
                    EditorGUILayout.HelpBox("Rebuilding texture list...", MessageType.Info);
                    return;
                }

                EnsureRebuilt();

                DrawBatchPanel();
                DrawTinifyPanel();
                DrawList();

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);

                if (!string.IsNullOrEmpty(_tinifyStatus))
                    EditorGUILayout.HelpBox(_tinifyStatus, MessageType.None);
            }
        }

        /// <summary>Top summary panel describing current analysis context and the meaning of status colors.</summary>
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

                GUILayout.Space(4);

                // Show size data source info
                if (_buildInfo.DataMode == BuildDataMode.PackedAssets)
                {
                    EditorGUILayout.LabelField("Sizes: From build report (actual packed sizes)", EditorStyles.miniBoldLabel);
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Sizes are estimated from source file sizes. Run 'Build & Analyze' to get actual compressed sizes from the build report.",
                        MessageType.Warning);
                }

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Status Colors:", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("\u2022 Green: optimized | Yellow: could improve | Red: needs attention", BridgeStyles.SubtitleStyle);
                BridgeStyles.EndCard();
            }
        }

        /// <summary>
        /// Toolbar with quick actions and filters:
        /// - Refresh
        /// - Search
        /// - Only Selected filter
        /// - Selection helpers
        /// </summary>
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(UI.Refresh, EditorStyles.toolbarButton, GUILayout.Width(70)))
                    RequestRebuild("User Refresh");

                GUILayout.Space(8);
                GUILayout.Label(UI.SearchLabel, GUILayout.Width(45));
                string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(120));
                if (newSearch != _search) _search = newSearch;

                GUILayout.Space(8);
                _onlySelected = GUILayout.Toggle(_onlySelected, UI.OnlySelected, EditorStyles.toolbarButton, GUILayout.Width(110));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(UI.SelectAll, EditorStyles.toolbarButton, GUILayout.Width(80))) SelectAll(true);
                if (GUILayout.Button(UI.Deselect, EditorStyles.toolbarButton, GUILayout.Width(70))) SelectAll(false);
                if (GUILayout.Button(UI.Invert, EditorStyles.toolbarButton, GUILayout.Width(60))) InvertSelection();
            }
        }

        /// <summary>
        /// Batch settings panel that modifies TextureImporter settings for selected textures.
        /// </summary>
        private void DrawBatchPanel()
        {
            _foldBatch = BridgeStyles.DrawSectionHeader("Batch Apply", _foldBatch, "\u2699");
            if (_foldBatch)
            {
                BridgeStyles.BeginCard();

                // First row: Preset and Apply button
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(UI.Preset, GUILayout.Width(50));
                    var newPreset = (Preset)EditorGUILayout.EnumPopup(_preset, GUILayout.MinWidth(150), GUILayout.MaxWidth(210));
                    if (newPreset != _preset)
                    {
                        _preset = newPreset;
                        ApplyPreset(_preset);
                    }

                    GUILayout.FlexibleSpace();

                    GUI.enabled = GetSelectedCount() > 0;
                    if (GUILayout.Button(UI.ApplyToSelected, GUILayout.Height(26), GUILayout.MinWidth(100), GUILayout.MaxWidth(140)))
                        ApplyBatchToSelected();
                    GUI.enabled = true;
                }

                GUILayout.Space(4);

                // Second row: Max Size and Compression
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(UI.WebGLMaxSize, GUILayout.Width(90));
                    _batchMaxSize = EditorGUILayout.IntPopup(
                        _batchMaxSize,
                        new[] { "256", "512", "1024", "2048", "4096" },
                        new[] { 256, 512, 1024, 2048, 4096 },
                        GUILayout.MinWidth(70), GUILayout.MaxWidth(100));

                    GUILayout.FlexibleSpace();

                    GUILayout.Label(UI.Compression, GUILayout.Width(80));
                    _batchCompression = (TextureImporterCompression)EditorGUILayout.EnumPopup(_batchCompression, GUILayout.MinWidth(100), GUILayout.MaxWidth(140));
                }

                GUILayout.Space(2);

                // Third row: Crunch and Quality
                using (new EditorGUILayout.HorizontalScope())
                {
                    _batchCrunch = EditorGUILayout.ToggleLeft(UI.Crunch, _batchCrunch, GUILayout.Width(70));

                    GUILayout.Label(UI.CrunchQuality, GUILayout.Width(50));
                    _batchCrunchQuality = EditorGUILayout.IntSlider(_batchCrunchQuality, 0, 100, GUILayout.MinWidth(100), GUILayout.MaxWidth(200));

                    GUILayout.FlexibleSpace();

                    _batchDisableReadWrite = EditorGUILayout.ToggleLeft(UI.DisableReadWrite, _batchDisableReadWrite, GUILayout.MinWidth(120), GUILayout.MaxWidth(160));
                }

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Presets: Balanced (1024) | Aggressive (512) | High Quality (2048)", BridgeStyles.SubtitleStyle);
                BridgeStyles.EndCard();
            }
        }

        /// <summary>
        /// Tinify panel:
        /// - Shows whether API key is set
        /// - Allows optimizing selected PNG/JPG/JPEG source files
        /// </summary>
        private void DrawTinifyPanel()
        {
            _foldTinify = BridgeStyles.DrawSectionHeader("Tinify (TinyPNG)", _foldTinify, "\u26A1");
            if (_foldTinify)
            {
                BridgeStyles.BeginCard();
                    bool hasKey = TinifyUtility.HasKey();
                    EditorGUILayout.LabelField("Key", hasKey ? "Set" : "Not set");

                    EditorGUILayout.LabelField("Optimizes source PNG/JPG/JPEG files via Tinify API", BridgeStyles.SubtitleStyle);

                    GUILayout.Space(4);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.enabled = !_tinifyRunning && hasKey && GetSelectedCount() > 0;

                        if (BridgeStyles.DrawAccentButton(UI.TinifyButton, GUILayout.Height(28)))
                            TinifySelected();

                        GUI.enabled = true;

                        GUILayout.FlexibleSpace();

                        if (!hasKey)
                            GUILayout.Label("Set key in Settings tab.", EditorStyles.miniLabel);
                    }
                BridgeStyles.EndCard();
            }
        }

        /// <summary>
        /// Runs Tinify optimization on selected textures (only PNG/JPG/JPEG sources).
        /// Progress is reported in the tab and the focused EditorWindow is repainted.
        /// </summary>
        private void TinifySelected()
        {
            if (_tinifyRunning) return;

            if (!TinifyUtility.CanUseTinify(out string reason))
            {
                _tinifyStatus = reason;
                return;
            }

            var paths = CollectSelectedTexturePaths();
            if (paths.Count == 0)
            {
                _tinifyStatus = "No selected textures.";
                return;
            }

            _tinifyRunning = true;
            _tinifyStatus = "Starting Tinify...";

            TinifyUtility.OptimizeAssetsAsync(
                paths,
                onProgress: (done, total, msg) =>
                {
                    _tinifyStatus = $"{msg} ({done}/{total})";
                    try { EditorWindow.focusedWindow?.Repaint(); } catch { }
                },
                onDone: (res) =>
                {
                    _tinifyRunning = false;

                    if (res == null)
                    {
                        _tinifyStatus = "Tinify finished (no result).";
                        return;
                    }

                    _tinifyStatus =
                        $"Tinify done. Optimized: {res.Optimized}, Skipped: {res.Skipped}, Failed: {res.Failed}. " +
                        $"Saved: {SharedTypes.FormatBytes(res.BytesSaved)}";

                    if (res.Errors != null && res.Errors.Count > 0)
                        _tinifyStatus += $"\nErrors: {Mathf.Min(3, res.Errors.Count)} shown in Console.";

                    if (res.Errors != null)
                    {
                        for (int i = 0; i < res.Errors.Count; i++)
                            UnityEngine.Debug.LogWarning("Bridge Tinify: " + res.Errors[i]);
                    }

                    RequestRebuild("After Tinify");
                    try { EditorWindow.focusedWindow?.Repaint(); } catch { }
                }
            );
        }

        /// <summary>
        /// List view of rows. Applies search filter and "Only Selected" filter at draw time.
        /// </summary>
        private void DrawList()
        {
            _foldList = BridgeStyles.DrawSectionHeader($"Texture List ({_rows.Count} items)", _foldList, "\u25A6");
            if (!_foldList) return;

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                if (_rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tracked textures found.", MessageType.Info);
                    return;
                }

                for (int i = 0; i < _rows.Count; i++)
                {
                    var r = _rows[i];

                    if (_onlySelected && !r.Selected) continue;
                    if (!PassSearch(r.Path, _search)) continue;

                    DrawRow(r);
                }
            }
        }

        /// <summary>
        /// Draws a single row with:
        /// - background color by heuristic status
        /// - selection toggle
        /// - file name
        /// - key importer flags/settings
        /// - Ping/Select shortcuts
        /// </summary>
        private void DrawRow(Row r)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 24);
            rect.x += 6;
            rect.y += 2;
            rect.height -= 4;

            Color bg = StatusToColor(r.Status);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), bg);

            // Calculate available width for content (excluding margins and buttons)
            float availableWidth = rect.width - 12; // margins
            float buttonWidth = 108; // Ping + Select buttons
            float checkboxWidth = 22;
            float contentWidth = availableWidth - buttonWidth - checkboxWidth;

            // Determine layout mode based on available width
            bool compactMode = contentWidth < 450;
            bool veryCompactMode = contentWidth < 300;

            float x = rect.x + 4;

            // Checkbox
            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 2, 18, rect.height), r.Selected);
            x += checkboxWidth;

            if (veryCompactMode)
            {
                // Very compact: only name and size
                float nameWidth = contentWidth * 0.65f;
                float sizeWidth = contentWidth * 0.35f;

                string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 20), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                string size = SharedTypes.FormatBytes(r.SizeBytes);
                if (r.IsSizeEstimated) size += " ~";
                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
            }
            else if (compactMode)
            {
                // Compact: name, size, max, compression
                float nameWidth = contentWidth * 0.35f;
                float sizeWidth = contentWidth * 0.2f;
                float maxWidth = contentWidth * 0.2f;
                float compWidth = contentWidth * 0.25f;

                string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 25), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                string size = SharedTypes.FormatBytes(r.SizeBytes);
                if (r.IsSizeEstimated) size += " ~";
                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, maxWidth, rect.height), "Max " + r.WebGLMaxSize, EditorStyles.miniLabel);
                x += maxWidth;

                string compText = r.Compression.ToString();
                if (r.Crunch) compText += " Cr";
                EditorGUI.LabelField(new Rect(x, rect.y + 2, compWidth, rect.height), compText, EditorStyles.miniLabel);
            }
            else
            {
                // Full layout: all columns
                float nameWidth = Mathf.Max(100, contentWidth * 0.22f);
                float sizeWidth = Mathf.Max(70, contentWidth * 0.13f);
                float maxWidth = Mathf.Max(55, contentWidth * 0.1f);
                float rwWidth = Mathf.Max(55, contentWidth * 0.1f);
                float compWidth = Mathf.Max(80, contentWidth * 0.18f);
                float crunchWidth = Mathf.Max(70, contentWidth * 0.15f);

                string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 30), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                string size = SharedTypes.FormatBytes(r.SizeBytes);
                if (r.IsSizeEstimated) size += " ~";
                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, maxWidth, rect.height), "Max " + r.WebGLMaxSize, EditorStyles.miniLabel);
                x += maxWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, rwWidth, rect.height), r.ReadWrite ? "R/W ON" : "R/W OFF", EditorStyles.miniLabel);
                x += rwWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, compWidth, rect.height), r.Compression.ToString(), EditorStyles.miniLabel);
                x += compWidth;

                string crunchText = r.Crunch ? "Crunch " + r.CrunchQuality : "Crunch OFF";
                EditorGUI.LabelField(new Rect(x, rect.y + 2, crunchWidth, rect.height), crunchText, EditorStyles.miniLabel);
            }

            // Buttons always at the right edge
            Rect pingR = new Rect(rect.x + rect.width - 112, rect.y + 2, 50, rect.height);
            if (GUI.Button(pingR, UI.Ping))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }

            Rect selR = new Rect(rect.x + rect.width - 58, rect.y + 2, 55, rect.height);
            if (GUI.Button(selR, UI.Select))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) Selection.activeObject = obj;
            }
        }

        /// <summary>
        /// Truncates a string and adds ellipsis if it exceeds the max length.
        /// </summary>
        private static string TruncateWithEllipsis(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen - 1) + "…";
        }

        /// <summary>
        /// Ensures the row cache is built.
        /// The actual rebuild work is scheduled via EditorApplication.delayCall to avoid heavy work inside OnGUI.
        /// </summary>
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

                    for (int i = 0; i < _buildInfo.Assets.Count; i++)
                    {
                        var a = _buildInfo.Assets[i];
                        if (a == null) continue;
                        if (a.Category != AssetCategory.Textures) continue;
                        if (string.IsNullOrEmpty(a.Path)) continue;

                        var main = AssetDatabase.LoadMainAssetAtPath(a.Path);
                        bool isTex2D = main is Texture2D;
                        Texture2D tex = main as Texture2D;

                        var imp = AssetImporter.GetAtPath(a.Path) as TextureImporter;

                        // Always use build report size (a.SizeBytes) as primary source
                        // In PackedAssets mode: this is the actual packed size from build report
                        // In DependenciesFallback mode: this is the file size (estimated)
                        var row = new Row
                        {
                            Path = a.Path,
                            SizeBytes = a.SizeBytes,
                            SourceSizeBytes = a.SizeBytes,
                            IsSizeEstimated = a.IsSizeEstimated,
                            Selected = false,

                            ImporterFound = (imp != null),
                            IsTexture2D = isTex2D,

                            WebGLMaxSize = GetWebGLMaxSize(imp),
                            Compression = imp != null ? imp.textureCompression : TextureImporterCompression.Uncompressed,
                            Crunch = imp != null && imp.crunchedCompression,
                            CrunchQuality = imp != null ? imp.compressionQuality : 0,
                            ReadWrite = imp != null && imp.isReadable
                        };

                        row.Status = EvaluateStatus(row);
                        _rows.Add(row);
                    }

                    _rows.Sort((x, y) => y.SizeBytes.CompareTo(x.SizeBytes));
                    _status = $"Tracked textures: {_rows.Count}";

                    // Force repaint to show updated values
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
                    // Force another repaint after rebuild completes
                    try { EditorWindow.focusedWindow?.Repaint(); } catch { }
                }
            };
        }

        /// <summary>
        /// Marks the row cache as stale and sets a user-facing status message.
        /// </summary>
        private void RequestRebuild(string reason)
        {
            _needsRebuild = true;
            _status = "Rebuild requested: " + reason;
        }

        /// <summary>Select or deselect every row.</summary>
        private void SelectAll(bool v)
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = v;
        }

        /// <summary>Invert selection for every row.</summary>
        private void InvertSelection()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = !_rows[i].Selected;
        }

        /// <summary>Returns the number of selected rows.</summary>
        private int GetSelectedCount()
        {
            int c = 0;
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Selected) c++;
            return c;
        }

        /// <summary>
        /// Collects asset paths for selected rows.
        /// The batch and Tinify operations both use this list.
        /// </summary>
        private List<string> CollectSelectedTexturePaths()
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

        /// <summary>
        /// Fills the batch UI fields based on the selected preset.
        /// This does not modify any assets until "Apply to Selected" is pressed.
        /// </summary>
        private void ApplyPreset(Preset p)
        {
            switch (p)
            {
                case Preset.WebGL_Aggressive_512:
                    _batchMaxSize = 512;
                    _batchCompression = TextureImporterCompression.Compressed;
                    _batchCrunch = true;
                    _batchCrunchQuality = 45;
                    _batchDisableReadWrite = true;
                    break;

                case Preset.WebGL_HighQuality_2048:
                    _batchMaxSize = 2048;
                    _batchCompression = TextureImporterCompression.Compressed;
                    _batchCrunch = false;
                    _batchCrunchQuality = 50;
                    _batchDisableReadWrite = true;
                    break;

                default:
                    _batchMaxSize = 1024;
                    _batchCompression = TextureImporterCompression.Compressed;
                    _batchCrunch = true;
                    _batchCrunchQuality = 50;
                    _batchDisableReadWrite = true;
                    break;
            }
        }

        /// <summary>
        /// Applies the configured batch settings to all selected Texture2D assets using their TextureImporter:
        /// - WebGL platform max texture size override
        /// - compression mode
        /// - Crunch enable + quality
        /// - optionally disable Read/Write
        /// After changes, forces reimport to apply settings.
        /// </summary>
        private void ApplyBatchToSelected()
        {
            var paths = CollectSelectedTexturePaths();
            if (paths.Count == 0)
            {
                _status = "No selected textures.";
                return;
            }

            int desiredMax = _batchMaxSize;
            var desiredCompression = _batchCompression;
            bool desiredCrunch = _batchCrunch;
            int desiredCrunchQ = Mathf.Clamp(_batchCrunchQuality, 0, 100);
            bool disableRW = _batchDisableReadWrite;

            EditorApplication.delayCall += () =>
            {
                try
                {
                    Undo.SetCurrentGroupName("Bridge TextureImporter Batch Apply");

                    AssetDatabase.StartAssetEditing();

                    int changed = 0;
                    int skipped = 0;

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (i % 10 == 0)
                            EditorUtility.DisplayProgressBar(
                                "Playgama Bridge",
                                "Applying texture importer settings...",
                                i / Mathf.Max(1f, (float)paths.Count));

                        string path = paths[i];

                        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (imp == null) { skipped++; continue; }

                        var main = AssetDatabase.LoadMainAssetAtPath(path);
                        if (!(main is Texture2D)) { skipped++; continue; }

                        Undo.RecordObject(imp, "Bridge TextureImporter Change");

                        bool any = false;

                        any |= SetWebGLMaxSize(imp, desiredMax);

                        if (imp.textureCompression != desiredCompression)
                        {
                            imp.textureCompression = desiredCompression;
                            any = true;
                        }

                        if (imp.crunchedCompression != desiredCrunch)
                        {
                            imp.crunchedCompression = desiredCrunch;
                            any = true;
                        }

                        if (imp.compressionQuality != desiredCrunchQ)
                        {
                            imp.compressionQuality = desiredCrunchQ;
                            any = true;
                        }

                        if (disableRW && imp.isReadable)
                        {
                            imp.isReadable = false;
                            any = true;
                        }

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
                        if (i % 10 == 0)
                            EditorUtility.DisplayProgressBar(
                                "Playgama Bridge",
                                "Reimporting textures...",
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

        /// <summary>
        /// Attempts to read the WebGL platform override max texture size.
        /// Falls back to importer.maxTextureSize if platform settings are unavailable.
        /// Returns 0 if importer is null or settings cannot be read.
        /// </summary>
        private static int GetWebGLMaxSize(TextureImporter imp)
        {
            if (imp == null) return 0;
            try
            {
                var s = imp.GetPlatformTextureSettings("WebGL");
                if (s != null && s.maxTextureSize > 0) return s.maxTextureSize;
            }
            catch { }

            try { return imp.maxTextureSize; } catch { return 0; }
        }

        /// <summary>
        /// Writes the WebGL platform override max texture size when possible.
        /// Falls back to importer.maxTextureSize if platform settings are unavailable.
        /// Returns true if any value/override state was changed.
        /// </summary>
        private static bool SetWebGLMaxSize(TextureImporter imp, int maxSize)
        {
            if (imp == null) return false;

            bool changed = false;

            try
            {
                var s = imp.GetPlatformTextureSettings("WebGL");
                bool was = s.overridden;

                s.overridden = true;
                if (s.maxTextureSize != maxSize)
                {
                    s.maxTextureSize = maxSize;
                    changed = true;
                }

                if (!was) changed = true;

                imp.SetPlatformTextureSettings(s);
                return changed;
            }
            catch
            {
                try
                {
                    if (imp.maxTextureSize != maxSize)
                    {
                        imp.maxTextureSize = maxSize;
                        return true;
                    }
                }
                catch { }
            }

            return changed;
        }

        /// <summary>
        /// Heuristic evaluation for quick UX coloring:
        /// - Red: Read/Write on, huge size (>=4096), or uncompressed
        /// - Yellow: big (>=2048) or compressed but Crunch off
        /// - Green: looks fine under these heuristics
        /// - Unknown: not a Texture2D or importer missing
        /// </summary>
        private static StatusLevel EvaluateStatus(Row r)
        {
            if (r == null) return StatusLevel.Unknown;
            if (!r.ImporterFound || !r.IsTexture2D) return StatusLevel.Unknown;

            bool uncompressed = r.Compression == TextureImporterCompression.Uncompressed;
            bool huge = r.WebGLMaxSize >= 4096;
            bool big = r.WebGLMaxSize >= 2048;

            if (r.ReadWrite) return StatusLevel.Red;
            if (huge) return StatusLevel.Red;
            if (uncompressed) return StatusLevel.Red;

            if (big) return StatusLevel.Yellow;
            if (!r.Crunch && r.Compression == TextureImporterCompression.Compressed) return StatusLevel.Yellow;

            return StatusLevel.Green;
        }

        /// <summary>Maps heuristic status to a subtle background color.</summary>
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

        /// <summary>Case-insensitive substring search against the asset path.</summary>
        private static bool PassSearch(string path, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Calculates the actual compressed/runtime size of a texture.
        /// Uses Unity's Profiler to get the accurate runtime memory size.
        /// </summary>
        private static long CalculateCompressedTextureSize(Texture2D tex, TextureImporter imp)
        {
            if (tex == null) return 0;

            try
            {
                // Use Unity's Profiler to get the actual runtime memory size
                // This gives us the size after compression is applied
                long runtimeSize = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);
                if (runtimeSize > 0)
                {
                    return runtimeSize;
                }
            }
            catch { }

            // Fallback: estimate based on texture format and dimensions
            try
            {
                int width = tex.width;
                int height = tex.height;
                TextureFormat format = tex.format;

                // Get bits per pixel for the format
                int bitsPerPixel = GetBitsPerPixel(format);
                if (bitsPerPixel <= 0) bitsPerPixel = 32; // Default to RGBA32

                // Calculate base size
                long baseSize = (long)width * height * bitsPerPixel / 8;

                // Account for mipmaps (adds ~33% for full mip chain)
                if (imp != null && imp.mipmapEnabled)
                {
                    baseSize = (long)(baseSize * 1.33f);
                }

                return baseSize;
            }
            catch { }

            return 0;
        }

        /// <summary>
        /// Returns the bits per pixel for common texture formats.
        /// </summary>
        private static int GetBitsPerPixel(TextureFormat format)
        {
            switch (format)
            {
                // Compressed formats
                case TextureFormat.DXT1:
                case TextureFormat.DXT1Crunched:
                case TextureFormat.ETC_RGB4:
                case TextureFormat.ETC2_RGB:
                case TextureFormat.PVRTC_RGB4:
                case TextureFormat.PVRTC_RGBA4:
                    return 4;

                case TextureFormat.DXT5:
                case TextureFormat.DXT5Crunched:
                case TextureFormat.ETC2_RGBA8:
                case TextureFormat.BC7:
                case TextureFormat.ASTC_4x4:
                case TextureFormat.ASTC_5x5:
                case TextureFormat.ASTC_6x6:
                case TextureFormat.ASTC_8x8:
                case TextureFormat.ASTC_10x10:
                case TextureFormat.ASTC_12x12:
                    return 8;

                // Uncompressed formats
                case TextureFormat.Alpha8:
                case TextureFormat.R8:
                    return 8;

                case TextureFormat.R16:
                case TextureFormat.RG16:
                case TextureFormat.RGB565:
                case TextureFormat.RGBA4444:
                case TextureFormat.ARGB4444:
                    return 16;

                case TextureFormat.RGB24:
                    return 24;

                case TextureFormat.RGBA32:
                case TextureFormat.ARGB32:
                case TextureFormat.BGRA32:
                case TextureFormat.RGBAHalf:
                    return 32;

                case TextureFormat.RGBAFloat:
                    return 128;

                default:
                    return 32; // Default assumption
            }
        }
    }
}
