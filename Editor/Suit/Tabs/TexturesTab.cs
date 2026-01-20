using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit.Tabs
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

            /// <summary>Tracked size from analysis (bytes). May be estimated depending on analysis mode.</summary>
            public long SizeBytes;

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
            _foldHeader = SuitStyles.DrawSectionHeader("Analysis Info", _foldHeader, "\u2139");
            if (_foldHeader)
            {
                SuitStyles.BeginCard();
                EditorGUILayout.LabelField("Analysis Mode", _buildInfo.DataMode.ToString());
                EditorGUILayout.LabelField("Tracked Assets", _buildInfo.TrackedAssetCount.ToString());

                string tb = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
                if (_buildInfo.DataMode == BuildDataMode.DependenciesFallback) tb += " (estimated)";
                EditorGUILayout.LabelField("Tracked Bytes", tb);

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Status Colors:", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField("\u2022 Green: optimized | Yellow: could improve | Red: needs attention", SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
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
            _foldBatch = SuitStyles.DrawSectionHeader("Batch Apply", _foldBatch, "\u2699");
            if (_foldBatch)
            {
                SuitStyles.BeginCard();
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(UI.Preset, GUILayout.Width(50));
                        var newPreset = (Preset)EditorGUILayout.EnumPopup(_preset, GUILayout.Width(210));
                        if (newPreset != _preset)
                        {
                            _preset = newPreset;
                            ApplyPreset(_preset);
                        }

                        GUILayout.FlexibleSpace();

                        GUI.enabled = GetSelectedCount() > 0;
                        if (GUILayout.Button(UI.ApplyToSelected, GUILayout.Height(26), GUILayout.Width(140)))
                            ApplyBatchToSelected();
                        GUI.enabled = true;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(UI.WebGLMaxSize, GUILayout.Width(110));
                        _batchMaxSize = EditorGUILayout.IntPopup(
                            _batchMaxSize,
                            new[] { "256", "512", "1024", "2048", "4096" },
                            new[] { 256, 512, 1024, 2048, 4096 },
                            GUILayout.Width(120));

                        GUILayout.Space(12);

                        GUILayout.Label(UI.Compression, GUILayout.Width(85));
                        _batchCompression = (TextureImporterCompression)EditorGUILayout.EnumPopup(_batchCompression, GUILayout.Width(140));

                        GUILayout.Space(12);

                        _batchCrunch = EditorGUILayout.ToggleLeft(UI.Crunch, _batchCrunch, GUILayout.Width(80));

                        GUILayout.Label(UI.CrunchQuality, GUILayout.Width(50));
                        _batchCrunchQuality = EditorGUILayout.IntSlider(_batchCrunchQuality, 0, 100, GUILayout.Width(220));

                        GUILayout.Space(12);

                        _batchDisableReadWrite = EditorGUILayout.ToggleLeft(UI.DisableReadWrite, _batchDisableReadWrite, GUILayout.Width(180));
                    }

                    GUILayout.Space(4);
                    EditorGUILayout.LabelField("Presets: Balanced (1024) | Aggressive (512) | High Quality (2048)", SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
            }
        }

        /// <summary>
        /// Tinify panel:
        /// - Shows whether API key is set
        /// - Allows optimizing selected PNG/JPG/JPEG source files
        /// </summary>
        private void DrawTinifyPanel()
        {
            _foldTinify = SuitStyles.DrawSectionHeader("Tinify (TinyPNG)", _foldTinify, "\u26A1");
            if (_foldTinify)
            {
                SuitStyles.BeginCard();
                    bool hasKey = TinifyUtility.HasKey();
                    EditorGUILayout.LabelField("Key", hasKey ? "Set" : "Not set");

                    EditorGUILayout.LabelField("Optimizes source PNG/JPG/JPEG files via Tinify API", SuitStyles.SubtitleStyle);

                    GUILayout.Space(4);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUI.enabled = !_tinifyRunning && hasKey && GetSelectedCount() > 0;

                        if (SuitStyles.DrawAccentButton(UI.TinifyButton, GUILayout.Height(28)))
                            TinifySelected();

                        GUI.enabled = true;

                        GUILayout.FlexibleSpace();

                        if (!hasKey)
                            GUILayout.Label("Set key in Settings tab.", EditorStyles.miniLabel);
                    }
                SuitStyles.EndCard();
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
                            UnityEngine.Debug.LogWarning("Suit Tinify: " + res.Errors[i]);
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
            _foldList = SuitStyles.DrawSectionHeader($"Texture List ({_rows.Count} items)", _foldList, "\u25A6");
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

            Rect cb = new Rect(rect.x + 4, rect.y + 2, 18, rect.height);
            r.Selected = EditorGUI.Toggle(cb, r.Selected);

            // Texture file name (extracted from path)
            string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(fileName)) fileName = "—";
            Rect nameR = new Rect(rect.x + 26, rect.y + 2, 150, rect.height);
            EditorGUI.LabelField(nameR, new GUIContent(fileName, r.Path), EditorStyles.miniLabel);

            Rect sizeR = new Rect(rect.x + 180, rect.y + 2, 95, rect.height);
            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";
            EditorGUI.LabelField(sizeR, size, EditorStyles.miniLabel);

            Rect maxR = new Rect(rect.x + 280, rect.y + 2, 70, rect.height);
            EditorGUI.LabelField(maxR, "Max " + r.WebGLMaxSize, EditorStyles.miniLabel);

            Rect rwR = new Rect(rect.x + 355, rect.y + 2, 70, rect.height);
            EditorGUI.LabelField(rwR, r.ReadWrite ? "R/W ON" : "R/W OFF", EditorStyles.miniLabel);

            Rect compR = new Rect(rect.x + 430, rect.y + 2, 95, rect.height);
            EditorGUI.LabelField(compR, r.Compression.ToString(), EditorStyles.miniLabel);

            Rect crR = new Rect(rect.x + 527, rect.y + 2, 90, rect.height);
            if (r.Crunch) EditorGUI.LabelField(crR, "Crunch " + r.CrunchQuality, EditorStyles.miniLabel);
            else EditorGUI.LabelField(crR, "Crunch OFF", EditorStyles.miniLabel);

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

                        var imp = AssetImporter.GetAtPath(a.Path) as TextureImporter;

                        var row = new Row
                        {
                            Path = a.Path,
                            SizeBytes = a.SizeBytes,
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
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                    _status = "Rebuild failed: " + ex.Message;
                }
                finally
                {
                    _isRebuilding = false;
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
                    Undo.SetCurrentGroupName("Suit TextureImporter Batch Apply");

                    AssetDatabase.StartAssetEditing();

                    int changed = 0;
                    int skipped = 0;

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (i % 10 == 0)
                            EditorUtility.DisplayProgressBar(
                                "Playgama Suit",
                                "Applying texture importer settings...",
                                i / Mathf.Max(1f, (float)paths.Count));

                        string path = paths[i];

                        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (imp == null) { skipped++; continue; }

                        var main = AssetDatabase.LoadMainAssetAtPath(path);
                        if (!(main is Texture2D)) { skipped++; continue; }

                        Undo.RecordObject(imp, "Suit TextureImporter Change");

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
                                "Playgama Suit",
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
                case StatusLevel.Red: return SuitStyles.StatusRed;
                case StatusLevel.Yellow: return SuitStyles.StatusYellow;
                case StatusLevel.Green: return SuitStyles.StatusGreen;
                default: return SuitStyles.StatusGray;
            }
        }

        /// <summary>Case-insensitive substring search against the asset path.</summary>
        private static bool PassSearch(string path, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
