using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor.Tabs
{
    public sealed class MeshesTab : ITab
    {
        public string TabName => "Meshes";

        private BuildInfo _buildInfo;

        private sealed class Row
        {
            public string Path;
            public long SizeBytes;
            public bool IsSizeEstimated;
            public bool Selected;
            public bool ImporterFound;
            public bool IsReadable;
            public ModelImporterMeshCompression MeshCompression;
            public bool RootFound;
            public StaticEditorFlags RootStaticFlags;
            public StatusLevel Status;
        }

        private enum StatusLevel { Green, Yellow, Red, Unknown }

        private readonly List<Row> _rows = new List<Row>(1024);
        private bool _needsRebuild = true;
        private bool _isRebuilding = false;
        private Vector2 _scrollPage;
        private Vector2 _scroll;
        private string _status = "";

        // Foldout states
        private bool _foldHeader = true;
        private bool _foldBatch = true;
        private bool _foldStaticFlags = false;
        private bool _foldList = true;

        private string _search = "";
        private bool _onlySelected = false;

        // Batch settings
        private bool _setReadable = false;
        private bool _applyReadable = true;
        private ModelImporterMeshCompression _compression = ModelImporterMeshCompression.Medium;
        private bool _applyCompression = true;
        private StaticEditorFlags _staticFlagsToSet = 0;
        private bool _applyStaticFlags = false;

        // Issue counts
        private int _uncompressedCount = 0;
        private int _readableCount = 0;
        private int _optimizedCount = 0;

        // Deprecated static flags excluded from UI
        private static readonly HashSet<string> kDeprecatedStaticNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NavigationStatic",
            "OffMeshLinkGeneration"
        };

        private static class UI
        {
            public static readonly GUIContent Refresh = new GUIContent(
                "Refresh",
                "Rebuild the internal meshes/models list from the latest analysis data.\n" +
                "Use this after changing import settings externally or after batch apply.");

            public static readonly GUIContent Search = new GUIContent(
                "Search",
                "Filter rows by asset path (case-insensitive substring match).");

            public static readonly GUIContent OnlySelected = new GUIContent(
                "Only Selected",
                "Show only currently selected rows.\n" +
                "Useful when working with a small batch.");

            public static readonly GUIContent SelectAll = new GUIContent(
                "All",
                "Select every visible row (ignores the 'Only Selected' filter).");

            public static readonly GUIContent Deselect = new GUIContent(
                "None",
                "Clear selection for every row.");

            public static readonly GUIContent Invert = new GUIContent(
                "Invert",
                "Invert selection state for every row.");

            public static readonly GUIContent ApplyMeshCompression = new GUIContent(
                "Apply Mesh Compression",
                "If enabled: sets ModelImporter.meshCompression for selected models.\n" +
                "Higher compression may reduce file size but can affect vertex precision.");

            public static readonly GUIContent MeshCompression = new GUIContent(
                "Compression",
                "ModelImporter mesh compression level applied to selected models.");

            public static readonly GUIContent ApplyReadWrite = new GUIContent(
                "Apply Read/Write",
                "If enabled: sets ModelImporter.isReadable for selected models.\n" +
                "Read/Write ON increases memory usage (keeps a CPU-readable copy).");

            public static readonly GUIContent ReadWriteEnabled = new GUIContent(
                "Read/Write Enabled",
                "Target value for ModelImporter.isReadable when applying Read/Write changes.");

            public static readonly GUIContent ApplyStaticFlags = new GUIContent(
                "Apply Static Flags to Model Root",
                "If enabled: applies the selected Static Flags to the imported model root GameObject.\n" +
                "Deprecated flags are excluded from the selector.");

            public static readonly GUIContent StaticFlags = new GUIContent(
                "Static Flags",
                "Choose which static flags should be set on the model root GameObject.\n" +
                "This affects things like batching, lightmapping, reflection probes, etc.");

            public static readonly GUIContent StaticAll = new GUIContent(
                "All",
                "Enable all non-deprecated static flags in this selector.");

            public static readonly GUIContent StaticNone = new GUIContent(
                "None",
                "Clear all static flags in this selector.");

            public static readonly GUIContent ApplyToSelected = new GUIContent(
                "Apply to Selected",
                "Apply enabled batch settings to all selected rows.\n" +
                "Importer changes trigger a reimport so settings take effect.");

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
                    EditorGUILayout.HelpBox("Rebuilding meshes list...", MessageType.Info);
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
                EditorGUILayout.LabelField("Analysis Mode", _buildInfo.DataMode.ToString());
                EditorGUILayout.LabelField("Tracked Assets", _buildInfo.TrackedAssetCount.ToString());

                string tracked = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
                if (_buildInfo.DataMode == BuildDataMode.DependenciesFallback) tracked += " (estimated)";
                EditorGUILayout.LabelField("Tracked Bytes", tracked);

                GUILayout.Space(8);

                // Status summary
                EditorGUILayout.LabelField("Mesh Status Summary:", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawStatusBadge(BridgeStyles.StatusRed, $"{_uncompressedCount} uncompressed", "No mesh compression applied");
                    GUILayout.Space(10);
                    DrawStatusBadge(BridgeStyles.StatusYellow, $"{_readableCount} Read/Write ON", "Using extra memory");
                    GUILayout.Space(10);
                    DrawStatusBadge(BridgeStyles.StatusGreen, $"{_optimizedCount} optimized", "Good settings");
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

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(UI.Refresh, EditorStyles.toolbarButton, GUILayout.Width(60)))
                    RequestRebuild("User Refresh");

                GUILayout.Space(6);

                GUILayout.Label(UI.Search, GUILayout.Width(42));
                string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(100));
                if (newSearch != _search) _search = newSearch;

                GUILayout.Space(6);

                _onlySelected = GUILayout.Toggle(_onlySelected, UI.OnlySelected, EditorStyles.toolbarButton, GUILayout.Width(90));

                GUILayout.FlexibleSpace();

                // Quick filter buttons
                if (GUILayout.Button(new GUIContent("No Comp", "Select meshes with no compression"), EditorStyles.toolbarButton, GUILayout.Width(55)))
                    SelectUncompressed();
                if (GUILayout.Button(new GUIContent("R/W ON", "Select meshes with Read/Write enabled"), EditorStyles.toolbarButton, GUILayout.Width(50)))
                    SelectReadable();
                if (GUILayout.Button(new GUIContent(">50KB", "Select meshes larger than 50KB"), EditorStyles.toolbarButton, GUILayout.Width(45)))
                    SelectLarge();

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
                ? $"Batch Apply - {selectedCount} mesh(es) selected"
                : "Batch Apply - Select meshes to apply";

            _foldBatch = BridgeStyles.DrawSectionHeader(headerText, _foldBatch, "\u2699");
            if (!_foldBatch) return;

            BridgeStyles.BeginCard();

            // Mesh Compression
            using (new EditorGUILayout.HorizontalScope())
            {
                _applyCompression = EditorGUILayout.ToggleLeft(UI.ApplyMeshCompression, _applyCompression, GUILayout.Width(160));
                GUI.enabled = _applyCompression;
                _compression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup(_compression, GUILayout.Width(100));

                // Show what will happen
                if (_applyCompression && selectedCount > 0)
                {
                    GUILayout.Space(10);
                    GUILayout.Label($"Will apply {_compression} to {selectedCount} mesh(es)", BridgeStyles.SubtitleStyle);
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(4);

            // Read/Write
            using (new EditorGUILayout.HorizontalScope())
            {
                _applyReadable = EditorGUILayout.ToggleLeft(UI.ApplyReadWrite, _applyReadable, GUILayout.Width(120));
                GUI.enabled = _applyReadable;
                _setReadable = EditorGUILayout.ToggleLeft(UI.ReadWriteEnabled, _setReadable, GUILayout.Width(130));

                // Explain what the setting means
                if (_applyReadable)
                {
                    GUILayout.Space(10);
                    string readableInfo = _setReadable ? "(Enables CPU access - uses more memory)" : "(Disables CPU access - saves memory)";
                    GUILayout.Label(readableInfo, BridgeStyles.SubtitleStyle);
                }

                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(8);

            // Static Flags in collapsible section
            _foldStaticFlags = EditorGUILayout.Foldout(_foldStaticFlags,
                new GUIContent("Static Flags (Advanced)", "Apply static flags to model root GameObjects. Useful for batching and lightmapping."),
                true);

            if (_foldStaticFlags)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    _applyStaticFlags = EditorGUILayout.ToggleLeft(UI.ApplyStaticFlags, _applyStaticFlags);

                    if (_applyStaticFlags)
                    {
                        GUILayout.Space(4);

                        // Quick preset buttons
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(new GUIContent("Recommended for Static", "Batching + Lightmap + Occlusion + Reflection"), EditorStyles.miniButton, GUILayout.Width(140)))
                            {
                                SetRecommendedStaticFlags();
                            }

                            if (GUILayout.Button(UI.StaticAll, EditorStyles.miniButton, GUILayout.Width(40)))
                            {
                                SetAllStaticFlags();
                            }

                            if (GUILayout.Button(UI.StaticNone, EditorStyles.miniButton, GUILayout.Width(40)))
                            {
                                _staticFlagsToSet = 0;
                            }

                            GUILayout.FlexibleSpace();
                        }

                        GUILayout.Space(4);

                        GUI.enabled = _applyStaticFlags;
                        DrawStaticFlagsSelector(ref _staticFlagsToSet);
                        GUI.enabled = true;
                    }

                    EditorGUILayout.LabelField("Static flags affect batching, lightmapping, occlusion, and reflection probes.", BridgeStyles.SubtitleStyle);
                }
            }

            GUILayout.Space(8);

            // Apply button
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                GUI.enabled = selectedCount > 0 && (_applyCompression || _applyReadable || _applyStaticFlags);

                Color oldBg = GUI.backgroundColor;
                if (selectedCount > 0 && (_applyCompression || _applyReadable || _applyStaticFlags))
                    GUI.backgroundColor = BridgeStyles.BrandPurple;

                string applyText = selectedCount > 0
                    ? $"Apply to {selectedCount} Selected Mesh(es)"
                    : "Select Meshes First";

                if (GUILayout.Button(applyText, GUILayout.Height(28), GUILayout.MinWidth(200)))
                    ApplyBatchToSelected();

                GUI.backgroundColor = oldBg;
                GUI.enabled = true;
            }

            BridgeStyles.EndCard();
        }

        private void SetRecommendedStaticFlags()
        {
            _staticFlagsToSet = StaticEditorFlags.BatchingStatic |
                               StaticEditorFlags.OccludeeStatic |
                               StaticEditorFlags.OccluderStatic |
                               StaticEditorFlags.ReflectionProbeStatic;

            // Try to add ContributeGI if available
            try
            {
                var names = Enum.GetNames(typeof(StaticEditorFlags));
                Array values = Enum.GetValues(typeof(StaticEditorFlags));
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].Contains("ContributeGI") || names[i].Contains("LightmapStatic"))
                    {
                        _staticFlagsToSet |= (StaticEditorFlags)values.GetValue(i);
                        break;
                    }
                }
            }
            catch { }
        }

        private void SetAllStaticFlags()
        {
            _staticFlagsToSet = 0;
            var names = Enum.GetNames(typeof(StaticEditorFlags));
            Array values = Enum.GetValues(typeof(StaticEditorFlags));
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];
                if (string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)) continue;
                if (kDeprecatedStaticNames.Contains(n)) continue;
                _staticFlagsToSet |= (StaticEditorFlags)values.GetValue(i);
            }
        }

        private void DrawStaticFlagsSelector(ref StaticEditorFlags flags)
        {
            var names = Enum.GetNames(typeof(StaticEditorFlags));
            Array values = Enum.GetValues(typeof(StaticEditorFlags));

            // Draw in two columns
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    int half = 0;
                    for (int i = 0; i < names.Length; i++)
                    {
                        string n = names[i];
                        if (string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)) continue;
                        if (kDeprecatedStaticNames.Contains(n)) continue;
                        half++;
                    }
                    half = (half + 1) / 2;

                    int count = 0;
                    for (int i = 0; i < names.Length; i++)
                    {
                        string n = names[i];
                        if (string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)) continue;
                        if (kDeprecatedStaticNames.Contains(n)) continue;

                        if (count >= half) break;

                        var v = (StaticEditorFlags)values.GetValue(i);
                        bool has = (flags & v) != 0;
                        bool newHas = EditorGUILayout.ToggleLeft(new GUIContent(n), has, GUILayout.Width(150));
                        if (newHas != has)
                        {
                            if (newHas) flags |= v;
                            else flags &= ~v;
                        }
                        count++;
                    }
                }

                using (new EditorGUILayout.VerticalScope())
                {
                    int half = 0;
                    for (int i = 0; i < names.Length; i++)
                    {
                        string n = names[i];
                        if (string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)) continue;
                        if (kDeprecatedStaticNames.Contains(n)) continue;
                        half++;
                    }
                    half = (half + 1) / 2;

                    int count = 0;
                    for (int i = 0; i < names.Length; i++)
                    {
                        string n = names[i];
                        if (string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)) continue;
                        if (kDeprecatedStaticNames.Contains(n)) continue;

                        count++;
                        if (count <= half) continue;

                        var v = (StaticEditorFlags)values.GetValue(i);
                        bool has = (flags & v) != 0;
                        bool newHas = EditorGUILayout.ToggleLeft(new GUIContent(n), has, GUILayout.Width(150));
                        if (newHas != has)
                        {
                            if (newHas) flags |= v;
                            else flags &= ~v;
                        }
                    }
                }

                GUILayout.FlexibleSpace();
            }
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

            string headerText = $"Mesh List - Showing {visibleCount} of {_rows.Count}";
            _foldList = BridgeStyles.DrawSectionHeader(headerText, _foldList, "\u25B2");
            if (!_foldList) return;

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Height(350)))
            {
                _scroll = sv.scrollPosition;

                if (_rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tracked model assets found.", MessageType.Info);
                    return;
                }

                if (visibleCount == 0)
                {
                    EditorGUILayout.HelpBox("No meshes match current filter.", MessageType.Info);
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

            bool compactMode = contentWidth < 400;
            bool veryCompactMode = contentWidth < 280;

            float x = rect.x + 6;

            // Checkbox
            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 4, 18, rect.height - 4), r.Selected);
            x += checkboxWidth;

            string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(fileName)) fileName = "—";

            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";

            string compression = r.ImporterFound ? ShortCompression(r.MeshCompression) : "N/A";
            string readable = r.ImporterFound ? (r.IsReadable ? "R/W ON" : "R/W OFF") : "N/A";

            if (veryCompactMode)
            {
                float nameWidth = contentWidth * 0.65f;
                float sizeWidth = contentWidth * 0.35f;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 18), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
            }
            else if (compactMode)
            {
                float nameWidth = contentWidth * 0.4f;
                float sizeWidth = contentWidth * 0.25f;
                float compWidth = contentWidth * 0.35f;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 22), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                string compInfo = compression;
                if (r.IsReadable) compInfo += " R/W";
                EditorGUI.LabelField(new Rect(x, rect.y + 4, compWidth, rect.height), compInfo, EditorStyles.miniLabel);
            }
            else
            {
                float nameWidth = Mathf.Max(100, contentWidth * 0.3f);
                float sizeWidth = Mathf.Max(70, contentWidth * 0.15f);
                float compWidth = Mathf.Max(60, contentWidth * 0.15f);
                float rwWidth = Mathf.Max(60, contentWidth * 0.15f);
                float flagsWidth = Mathf.Max(80, contentWidth * 0.2f);

                EditorGUI.LabelField(new Rect(x, rect.y + 4, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 28), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 4, compWidth, rect.height), compression, EditorStyles.miniLabel);
                x += compWidth;

                // R/W with color
                GUIStyle rwStyle = new GUIStyle(EditorStyles.miniLabel);
                if (r.IsReadable) rwStyle.normal.textColor = new Color(1f, 0.6f, 0.4f);
                EditorGUI.LabelField(new Rect(x, rect.y + 4, rwWidth, rect.height), readable, rwStyle);
                x += rwWidth;

                string sf = r.RootFound ? ShortStaticFlags(r.RootStaticFlags) : "N/A";
                EditorGUI.LabelField(new Rect(x, rect.y + 4, flagsWidth, rect.height), TruncateWithEllipsis(sf, 12), EditorStyles.miniLabel);
            }

            // Buttons
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
            if (string.IsNullOrEmpty(s)) return "—";
            if (s.Length <= maxLen) return s;
            return s.Substring(0, maxLen - 1) + "…";
        }

        private static string ShortCompression(ModelImporterMeshCompression c)
        {
            switch (c)
            {
                case ModelImporterMeshCompression.Off: return "Off";
                case ModelImporterMeshCompression.Low: return "Low";
                case ModelImporterMeshCompression.Medium: return "Med";
                case ModelImporterMeshCompression.High: return "High";
                default: return c.ToString();
            }
        }

        private static string ShortStaticFlags(StaticEditorFlags f)
        {
            if (f == 0) return "None";

            var names = Enum.GetNames(typeof(StaticEditorFlags));
            Array values = Enum.GetValues(typeof(StaticEditorFlags));

            var parts = new List<string>(6);
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i];
                if (string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)) continue;
                if (kDeprecatedStaticNames.Contains(n)) continue;

                var v = (StaticEditorFlags)values.GetValue(i);
                if ((f & v) != 0) parts.Add(n);

                if (parts.Count >= 3) break;
            }

            if (parts.Count == 0) return "Custom";
            return string.Join(", ", parts.ToArray());
        }

        private StatusLevel EvaluateStatus(Row r)
        {
            if (!r.ImporterFound) return StatusLevel.Unknown;

            // Uncompressed is a problem for large meshes
            if (r.MeshCompression == ModelImporterMeshCompression.Off && r.SizeBytes > 50 * 1024)
                return StatusLevel.Red;

            // Read/Write ON uses extra memory
            if (r.IsReadable) return StatusLevel.Yellow;

            // No compression at all
            if (r.MeshCompression == ModelImporterMeshCompression.Off) return StatusLevel.Yellow;

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
                    _uncompressedCount = 0;
                    _readableCount = 0;
                    _optimizedCount = 0;

                    for (int i = 0; i < _buildInfo.Assets.Count; i++)
                    {
                        var a = _buildInfo.Assets[i];
                        if (a == null) continue;
                        if (string.IsNullOrEmpty(a.Path)) continue;

                        string cat = a.Category.ToString();
                        bool isModel = cat.Equals("Meshes", StringComparison.OrdinalIgnoreCase) ||
                                       cat.Equals("Models", StringComparison.OrdinalIgnoreCase);
                        if (!isModel) continue;

                        var imp = AssetImporter.GetAtPath(a.Path) as ModelImporter;

                        var row = new Row
                        {
                            Path = a.Path,
                            SizeBytes = a.SizeBytes,
                            IsSizeEstimated = a.IsSizeEstimated,
                            Selected = false,

                            ImporterFound = (imp != null),
                            IsReadable = false,
                            MeshCompression = ModelImporterMeshCompression.Off,

                            RootFound = false,
                            RootStaticFlags = 0
                        };

                        if (imp != null)
                        {
                            row.IsReadable = imp.isReadable;
                            row.MeshCompression = imp.meshCompression;
                        }

                        var go = AssetDatabase.LoadAssetAtPath<GameObject>(a.Path);
                        if (go != null)
                        {
                            row.RootFound = true;
                            row.RootStaticFlags = GameObjectUtility.GetStaticEditorFlags(go);
                        }

                        row.Status = EvaluateStatus(row);

                        // Count issues
                        if (row.MeshCompression == ModelImporterMeshCompression.Off) _uncompressedCount++;
                        if (row.IsReadable) _readableCount++;
                        if (row.Status == StatusLevel.Green) _optimizedCount++;

                        _rows.Add(row);
                    }

                    _rows.Sort((x, y) => y.SizeBytes.CompareTo(x.SizeBytes));
                    _status = $"Tracked model assets: {_rows.Count}";

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

        private void ApplyBatchToSelected()
        {
            var paths = CollectSelectedPaths();
            if (paths.Count == 0)
            {
                _status = "No selected items.";
                return;
            }

            EditorApplication.delayCall += () =>
            {
                try
                {
                    Undo.SetCurrentGroupName("Bridge Mesh Batch Apply");

                    AssetDatabase.StartAssetEditing();

                    int changedImporters = 0;
                    int changedStatic = 0;
                    int skipped = 0;

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (i % 8 == 0)
                            EditorUtility.DisplayProgressBar(
                                "Playgama Bridge",
                                "Applying mesh settings...",
                                i / Mathf.Max(1f, (float)paths.Count));

                        string path = paths[i];

                        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (imp != null)
                        {
                            Undo.RecordObject(imp, "Bridge ModelImporter Change");

                            bool any = false;

                            if (_applyReadable && imp.isReadable != _setReadable)
                            {
                                imp.isReadable = _setReadable;
                                any = true;
                            }

                            if (_applyCompression && imp.meshCompression != _compression)
                            {
                                imp.meshCompression = _compression;
                                any = true;
                            }

                            if (any)
                            {
                                EditorUtility.SetDirty(imp);
                                changedImporters++;
                            }
                        }
                        else
                        {
                            skipped++;
                        }

                        if (_applyStaticFlags)
                        {
                            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                            if (go != null)
                            {
                                Undo.RecordObject(go, "Bridge Static Flags Change");

                                var cur = GameObjectUtility.GetStaticEditorFlags(go);
                                if (cur != _staticFlagsToSet)
                                {
                                    GameObjectUtility.SetStaticEditorFlags(go, _staticFlagsToSet);
                                    EditorUtility.SetDirty(go);
                                    changedStatic++;
                                }
                            }
                        }
                    }

                    AssetDatabase.StopAssetEditing();
                    EditorUtility.ClearProgressBar();

                    if (changedImporters > 0)
                    {
                        for (int i = 0; i < paths.Count; i++)
                        {
                            if (i % 8 == 0)
                                EditorUtility.DisplayProgressBar(
                                    "Playgama Bridge",
                                    "Reimporting models...",
                                    i / Mathf.Max(1f, (float)paths.Count));

                            AssetDatabase.ImportAsset(paths[i], ImportAssetOptions.ForceUpdate);
                        }
                        EditorUtility.ClearProgressBar();
                    }

                    _status = $"Applied. Importers changed: {changedImporters}, Static roots changed: {changedStatic}, Skipped: {skipped}.";
                    RequestRebuild("After Apply");
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

        private void SelectUncompressed()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = (_rows[i].MeshCompression == ModelImporterMeshCompression.Off);
        }

        private void SelectReadable()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = _rows[i].IsReadable;
        }

        private void SelectLarge()
        {
            for (int i = 0; i < _rows.Count; i++)
                _rows[i].Selected = (_rows[i].SizeBytes > 50 * 1024);
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
                var r = _rows[i];
                if (!r.Selected) continue;
                if (string.IsNullOrEmpty(r.Path)) continue;
                list.Add(r.Path);
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
