using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor.Tabs
{
    public sealed class TexturesTab : ITab
    {
        public string TabName => "Textures";

        private BuildInfo _buildInfo;
        private readonly List<Row> _rows = new List<Row>(2048);
        private bool _needsRebuild = true;
        private bool _isRebuilding = false;
        private Vector2 _scrollPage;
        private Vector2 _scroll;
        private string _status = "";
        private string _search = "";
        private bool _onlySelected = false;

        // Batch apply settings
        private int _batchMaxSize = 1024;
        private TextureImporterCompression _batchCompression = TextureImporterCompression.Compressed;
        private bool _batchCrunch = true;
        private int _batchCrunchQuality = 50;
        private bool _batchDisableReadWrite = true;

        // Foldout states
        private bool _foldHeader = true;
        private bool _foldBatch = true;
        private bool _foldList = true;

        // Status counts for quick reference
        private int _redCount = 0;
        private int _yellowCount = 0;
        private int _greenCount = 0;

        private enum Preset
        {
            WebGL_Balanced_1024,
            WebGL_Aggressive_512,
            WebGL_HighQuality_2048
        }

        private Preset _preset = Preset.WebGL_Balanced_1024;

        private sealed class Row
        {
            public string Path;
            public long SizeBytes;
            public long SourceSizeBytes;
            public bool IsSizeEstimated;
            public bool Selected;
            public bool ImporterFound;
            public bool IsTexture2D;
            public int WebGLMaxSize;
            public TextureImporterCompression Compression;
            public bool Crunch;
            public int CrunchQuality;
            public bool ReadWrite;
            public StatusLevel Status;
        }

        private enum StatusLevel
        {
            Green,
            Yellow,
            Red,
            Unknown
        }

        private static class UI
        {
            public static readonly GUIContent Refresh = new GUIContent(
                "Refresh",
                "Rebuild the internal texture list from the latest analysis data.\n" +
                "Use this if you changed import settings externally.");

            public static readonly GUIContent SearchLabel = new GUIContent(
                "Search",
                "Filter textures by asset path (case-insensitive substring match).");

            public static readonly GUIContent OnlySelected = new GUIContent(
                "Only Selected",
                "Show only currently selected rows.\n" +
                "Useful when you're working on a small batch.");

            public static readonly GUIContent SelectAll = new GUIContent(
                "All",
                "Select every visible row.");

            public static readonly GUIContent Deselect = new GUIContent(
                "None",
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

            public static readonly GUIContent Ping = new GUIContent(
                "Ping",
                "Highlights the asset in the Project window so you can locate it quickly.");

            public static readonly GUIContent Select = new GUIContent(
                "Sel",
                "Selects the asset in the Project window (Selection.activeObject).");
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

                if (!_buildInfo.hasData || _buildInfo.assets == null || _buildInfo.assets.Count == 0)
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
                EditorGUILayout.LabelField("Analysis Mode", _buildInfo.dataMode.ToString());
                EditorGUILayout.LabelField("Tracked Assets", _buildInfo.trackedAssetCount.ToString());

                string tb = SharedTypes.FormatBytes(_buildInfo.trackedBytes);
                if (_buildInfo.dataMode == BuildDataMode.DependenciesFallback) tb += " (estimated)";
                EditorGUILayout.LabelField("Tracked Bytes", tb);

                GUILayout.Space(8);

                // Status color legend - more prominent
                EditorGUILayout.LabelField("Status Color Legend:", EditorStyles.boldLabel);
                GUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStatusLegendItem(BridgeStyles.statusGreen, "Green", "Optimized", _greenCount);
                    GUILayout.Space(10);
                    DrawStatusLegendItem(BridgeStyles.statusYellow, "Yellow", "Could improve", _yellowCount);
                    GUILayout.Space(10);
                    DrawStatusLegendItem(BridgeStyles.statusRed, "Red", "Needs attention", _redCount);
                    GUILayout.FlexibleSpace();
                }

                GUILayout.Space(4);

                if (_buildInfo.dataMode != BuildDataMode.PackedAssets)
                {
                    EditorGUILayout.HelpBox(
                        "Sizes are estimated from source file sizes. Run 'Build & Analyze' to get actual compressed sizes from the build report.",
                        MessageType.Warning);
                }

                BridgeStyles.EndCard();
            }
        }

        private void DrawStatusLegendItem(Color color, string label, string desc, int count)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 20, GUILayout.Width(130));
            Rect colorRect = new Rect(rect.x, rect.y + 4, 12, 12);
            EditorGUI.DrawRect(colorRect, color);

            Rect labelRect = new Rect(rect.x + 18, rect.y, 110, 20);
            EditorGUI.LabelField(labelRect, new GUIContent($"{label}: {count}", desc), EditorStyles.miniLabel);
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
                if (GUILayout.Button(new GUIContent("Red", "Select all textures with Red status (needs attention)"), EditorStyles.toolbarButton, GUILayout.Width(35)))
                    SelectByStatus(StatusLevel.Red);
                if (GUILayout.Button(new GUIContent("Yellow", "Select all textures with Yellow status (could improve)"), EditorStyles.toolbarButton, GUILayout.Width(45)))
                    SelectByStatus(StatusLevel.Yellow);

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
                ? $"Batch Apply - {selectedCount} texture(s) selected"
                : "Batch Apply - Select textures to apply";

            _foldBatch = BridgeStyles.DrawSectionHeader(headerText, _foldBatch, "\u2699");
            if (_foldBatch)
            {
                BridgeStyles.BeginCard();

                // Preset with preview
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
                }

                // Show preset details
                GUILayout.Space(2);
                string presetDetails = GetPresetDetails(_preset);
                EditorGUILayout.LabelField(presetDetails, BridgeStyles.subtitleStyle);

                GUILayout.Space(8);

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

                GUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _batchCrunch = EditorGUILayout.ToggleLeft(UI.Crunch, _batchCrunch, GUILayout.Width(70));

                    GUILayout.Label(UI.CrunchQuality, GUILayout.Width(50));
                    _batchCrunchQuality = EditorGUILayout.IntSlider(_batchCrunchQuality, 0, 100, GUILayout.MinWidth(100), GUILayout.MaxWidth(180));

                    // Quality context label
                    string qualityLabel = _batchCrunchQuality < 30 ? "(Aggressive)" :
                                         _batchCrunchQuality < 60 ? "(Balanced)" : "(High Quality)";
                    GUILayout.Label(qualityLabel, EditorStyles.miniLabel, GUILayout.Width(80));

                    GUILayout.FlexibleSpace();

                    _batchDisableReadWrite = EditorGUILayout.ToggleLeft(UI.DisableReadWrite, _batchDisableReadWrite, GUILayout.MinWidth(120), GUILayout.MaxWidth(160));
                }

                GUILayout.Space(8);

                // Apply button with explicit count
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();

                    GUI.enabled = selectedCount > 0;

                    Color oldBg = GUI.backgroundColor;
                    if (selectedCount > 0)
                        GUI.backgroundColor = BridgeStyles.brandPurple;

                    string applyText = selectedCount > 0
                        ? $"Apply to {selectedCount} Selected Texture(s)"
                        : "Select Textures First";

                    if (GUILayout.Button(applyText, GUILayout.Height(28), GUILayout.MinWidth(200)))
                        ApplyBatchToSelected();

                    GUI.backgroundColor = oldBg;
                    GUI.enabled = true;
                }

                BridgeStyles.EndCard();
            }
        }

        private string GetPresetDetails(Preset p)
        {
            switch (p)
            {
                case Preset.WebGL_Aggressive_512:
                    return "Max 512px, Compressed, Crunch Q45, R/W Off - Smallest size";
                case Preset.WebGL_HighQuality_2048:
                    return "Max 2048px, Compressed, No Crunch, R/W Off - Best quality";
                default:
                    return "Max 1024px, Compressed, Crunch Q50, R/W Off - Good balance";
            }
        }

        private void DrawList()
        {
            // Count visible items
            int visibleCount = 0;
            int selectedCount = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                if (_onlySelected && !r.Selected) continue;
                if (!PassSearch(r.Path, _search)) continue;
                visibleCount++;
                if (r.Selected) selectedCount++;
            }

            string headerText = $"Texture List - Showing {visibleCount} of {_rows.Count}";
            if (selectedCount > 0)
                headerText += $" ({selectedCount} selected)";

            _foldList = BridgeStyles.DrawSectionHeader(headerText, _foldList, "\u25A6");
            if (!_foldList) return;

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Height(400)))
            {
                _scroll = sv.scrollPosition;

                if (_rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tracked textures found.", MessageType.Info);
                    return;
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox("No textures match current filter.", MessageType.Info);
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

            // Bottom status bar
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Selected: {GetSelectedCount()} | Total: {_rows.Count}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRow(Row r)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 26);
            rect.x += 6;
            rect.y += 2;
            rect.height -= 4;

            // Background based on status
            Color bg = StatusToColor(r.Status);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), bg);

            // Selection highlight - more prominent
            if (r.Selected)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 4, rect.height), BridgeStyles.brandPurple);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 2), new Color(0.55f, 0.36f, 0.96f, 0.5f));
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 2, rect.width, 2), new Color(0.55f, 0.36f, 0.96f, 0.5f));
            }

            float availableWidth = rect.width - 12;
            float buttonWidth = 90;
            float checkboxWidth = 24;
            float contentWidth = availableWidth - buttonWidth - checkboxWidth;

            bool compactMode = contentWidth < 450;
            bool veryCompactMode = contentWidth < 300;

            float x = rect.x + 6;

            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 4, 18, rect.height - 4), r.Selected);
            x += checkboxWidth;

            if (veryCompactMode)
            {
                float nameWidth = contentWidth * 0.65f;
                float sizeWidth = contentWidth * 0.35f;

                string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 20), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                string size = SharedTypes.FormatBytes(r.SizeBytes);
                if (r.IsSizeEstimated) size += " ~";
                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
            }
            else if (compactMode)
            {
                float nameWidth = contentWidth * 0.35f;
                float sizeWidth = contentWidth * 0.2f;
                float maxWidth = contentWidth * 0.2f;
                float compWidth = contentWidth * 0.25f;

                string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 25), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                string size = SharedTypes.FormatBytes(r.SizeBytes);
                if (r.IsSizeEstimated) size += " ~";
                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, maxWidth, rect.height), "Max " + r.WebGLMaxSize, EditorStyles.miniLabel);
                x += maxWidth;

                string compText = r.Compression.ToString();
                if (r.Crunch) compText += " Cr";
                EditorGUI.LabelField(new Rect(x, rect.y + 4, compWidth, rect.height), compText, EditorStyles.miniLabel);
            }
            else
            {
                float nameWidth = Mathf.Max(100, contentWidth * 0.22f);
                float sizeWidth = Mathf.Max(70, contentWidth * 0.13f);
                float maxWidth = Mathf.Max(55, contentWidth * 0.1f);
                float rwWidth = Mathf.Max(55, contentWidth * 0.1f);
                float compWidth = Mathf.Max(80, contentWidth * 0.18f);
                float crunchWidth = Mathf.Max(70, contentWidth * 0.15f);

                string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 30), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                string size = SharedTypes.FormatBytes(r.SizeBytes);
                if (r.IsSizeEstimated) size += " ~";
                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, maxWidth, rect.height), "Max " + r.WebGLMaxSize, EditorStyles.miniLabel);
                x += maxWidth;

                string rwText = r.ReadWrite ? "R/W ON" : "R/W OFF";
                GUIStyle rwStyle = new GUIStyle(EditorStyles.miniLabel);
                if (r.ReadWrite) rwStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
                EditorGUI.LabelField(new Rect(x, rect.y + 4, rwWidth, rect.height), rwText, rwStyle);
                x += rwWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, compWidth, rect.height), r.Compression.ToString(), EditorStyles.miniLabel);
                x += compWidth;

                string crunchText = r.Crunch ? "Crunch " + r.CrunchQuality : "Crunch OFF";
                EditorGUI.LabelField(new Rect(x, rect.y + 4, crunchWidth, rect.height), crunchText, EditorStyles.miniLabel);
            }

            Rect pingR = new Rect(rect.x + rect.width - 90, rect.y + 3, 42, rect.height - 2);
            if (GUI.Button(pingR, UI.Ping, EditorStyles.miniButton))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }

            Rect selR = new Rect(rect.x + rect.width - 45, rect.y + 3, 42, rect.height - 2);
            if (GUI.Button(selR, UI.Select, EditorStyles.miniButton))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) Selection.activeObject = obj;
            }
        }

        private static string TruncateWithEllipsis(string s, int maxLen)
        {
            if (string.IsNullOrEmpty(s)) return "—";
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen - 1) + "…";
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
                    _redCount = 0;
                    _yellowCount = 0;
                    _greenCount = 0;

                    for (int i = 0; i < _buildInfo.assets.Count; i++)
                    {
                        var a = _buildInfo.assets[i];
                        if (a == null) continue;
                        if (a.category != AssetCategory.Textures) continue;
                        if (string.IsNullOrEmpty(a.path)) continue;

                        var main = AssetDatabase.LoadMainAssetAtPath(a.path);
                        bool isTex2D = main is Texture2D;
                        Texture2D tex = main as Texture2D;

                        var imp = AssetImporter.GetAtPath(a.path) as TextureImporter;

                        var row = new Row
                        {
                            Path = a.path,
                            SizeBytes = a.sizeBytes,
                            SourceSizeBytes = a.sizeBytes,
                            IsSizeEstimated = a.isSizeEstimated,
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

                        // Count by status
                        switch (row.Status)
                        {
                            case StatusLevel.Red: _redCount++; break;
                            case StatusLevel.Yellow: _yellowCount++; break;
                            case StatusLevel.Green: _greenCount++; break;
                        }

                        _rows.Add(row);
                    }

                    _rows.Sort((x, y) => y.SizeBytes.CompareTo(x.SizeBytes));
                    _status = $"Tracked textures: {_rows.Count}";

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

        private void RequestRebuild(string reason)
        {
            _needsRebuild = true;
            _status = "Rebuild requested: " + reason;
        }

        private void SelectAll(bool v)
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = v;
        }

        private void SelectByStatus(StatusLevel status)
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = (_rows[i].Status == status);
        }

        private void InvertSelection()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = !_rows[i].Selected;
        }

        private int GetSelectedCount()
        {
            int c = 0;
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].Selected) c++;
            return c;
        }

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

        private static Color StatusToColor(StatusLevel s)
        {
            switch (s)
            {
                case StatusLevel.Red: return BridgeStyles.statusRed;
                case StatusLevel.Yellow: return BridgeStyles.statusYellow;
                case StatusLevel.Green: return BridgeStyles.statusGreen;
                default: return BridgeStyles.statusGray;
            }
        }

        private static bool PassSearch(string path, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
