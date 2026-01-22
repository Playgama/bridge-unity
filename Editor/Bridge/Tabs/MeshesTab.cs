using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
{
    public sealed class MeshesTab : ITab
    {
        public string TabName { get { return "Meshes"; } }

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
        }

        private readonly List<Row> _rows = new List<Row>(1024);
        private bool _needsRebuild = true;
        private bool _isRebuilding = false;
        private Vector2 _scrollPage;
        private Vector2 _scroll;
        private string _status = "";

        // Foldout states
        private bool _foldHeader = true;
        private bool _foldBatch = true;
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
                "Select All",
                "Select every visible row (ignores the 'Only Selected' filter).");

            public static readonly GUIContent Deselect = new GUIContent(
                "Deselect",
                "Clear selection for every row.");

            public static readonly GUIContent Invert = new GUIContent(
                "Invert",
                "Invert selection state for every row.");

            public static readonly GUIContent BatchApplyTitle = new GUIContent(
                "Batch Apply",
                "Batch tools for changing ModelImporter settings and (optionally) static flags on model root.");

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

            public static readonly GUIContent SelectedCount = new GUIContent(
                "Selected:",
                "Number of rows currently selected.");

            public static readonly GUIContent Ping = new GUIContent(
                "Ping",
                "Ping the asset in the Project window.");

            public static readonly GUIContent Select = new GUIContent(
                "Select",
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

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Batch operations: Mesh Compression, Read/Write, Static Flags", BridgeStyles.SubtitleStyle);
                BridgeStyles.EndCard();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(UI.Refresh, EditorStyles.toolbarButton, GUILayout.Width(70)))
                    RequestRebuild("User Refresh");

                GUILayout.Space(8);

                GUILayout.Label(UI.Search, GUILayout.Width(45));
                string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(120));
                if (newSearch != _search) _search = newSearch;

                GUILayout.Space(8);

                _onlySelected = GUILayout.Toggle(_onlySelected, UI.OnlySelected, EditorStyles.toolbarButton, GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                if (GUILayout.Button(UI.SelectAll, EditorStyles.toolbarButton, GUILayout.Width(80))) SelectAll(true);
                if (GUILayout.Button(UI.Deselect, EditorStyles.toolbarButton, GUILayout.Width(70))) SelectAll(false);
                if (GUILayout.Button(UI.Invert, EditorStyles.toolbarButton, GUILayout.Width(60))) InvertSelection();
            }
        }

        private void DrawBatchPanel()
        {
            _foldBatch = BridgeStyles.DrawSectionHeader("Batch Apply", _foldBatch, "\u2699");
            if (!_foldBatch) return;

            BridgeStyles.BeginCard();

            // Mesh Compression
            using (new EditorGUILayout.HorizontalScope())
            {
                _applyCompression = EditorGUILayout.ToggleLeft(UI.ApplyMeshCompression, _applyCompression, GUILayout.MinWidth(130), GUILayout.MaxWidth(170));
                GUI.enabled = _applyCompression;
                _compression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup(_compression, GUILayout.MinWidth(80), GUILayout.MaxWidth(120));
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(2);

            // Read/Write
            using (new EditorGUILayout.HorizontalScope())
            {
                _applyReadable = EditorGUILayout.ToggleLeft(UI.ApplyReadWrite, _applyReadable, GUILayout.MinWidth(100), GUILayout.MaxWidth(140));
                GUI.enabled = _applyReadable;
                _setReadable = EditorGUILayout.ToggleLeft(UI.ReadWriteEnabled, _setReadable, GUILayout.MinWidth(80), GUILayout.MaxWidth(120));
                GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _applyStaticFlags = EditorGUILayout.ToggleLeft(UI.ApplyStaticFlags, _applyStaticFlags);
                GUI.enabled = _applyStaticFlags;

                DrawStaticFlagsSelector(ref _staticFlagsToSet);

                GUI.enabled = true;

                EditorGUILayout.LabelField("Static flags are applied to the imported model root.", BridgeStyles.SubtitleStyle);
            }

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                int selected = GetSelectedCount();
                GUILayout.Label(UI.SelectedCount, EditorStyles.miniLabel);
                GUILayout.Label(selected.ToString(), EditorStyles.miniLabel);

                GUILayout.FlexibleSpace();

                GUI.enabled = selected > 0 && (_applyCompression || _applyReadable || _applyStaticFlags);

                if (GUILayout.Button(UI.ApplyToSelected, GUILayout.Height(28), GUILayout.MinWidth(100), GUILayout.MaxWidth(160)))
                    ApplyBatchToSelected();

                GUI.enabled = true;
            }
            BridgeStyles.EndCard();
        }

        private void DrawStaticFlagsSelector(ref StaticEditorFlags flags)
        {
            var names = Enum.GetNames(typeof(StaticEditorFlags));
            Array values = Enum.GetValues(typeof(StaticEditorFlags));

            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label(UI.StaticFlags, EditorStyles.miniBoldLabel);

                for (int i = 0; i < names.Length; i++)
                {
                    string n = names[i];
                    if (string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)) continue;
                    if (kDeprecatedStaticNames.Contains(n)) continue;

                    var v = (StaticEditorFlags)values.GetValue(i);
                    bool has = (flags & v) != 0;

                    bool newHas = EditorGUILayout.ToggleLeft(new GUIContent(n, "Toggle this static flag on the model root."), has);
                    if (newHas != has)
                    {
                        if (newHas) flags |= v;
                        else flags &= ~v;
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(UI.StaticAll, GUILayout.Width(60)))
                    {
                        flags = 0;
                        for (int i = 0; i < names.Length; i++)
                        {
                            string n = names[i];
                            if (string.Equals(n, "None", StringComparison.OrdinalIgnoreCase)) continue;
                            if (kDeprecatedStaticNames.Contains(n)) continue;
                            flags |= (StaticEditorFlags)values.GetValue(i);
                        }
                    }

                    if (GUILayout.Button(UI.StaticNone, GUILayout.Width(60)))
                        flags = 0;

                    GUILayout.FlexibleSpace();
                }
            }
        }

        private void DrawList()
        {
            _foldList = BridgeStyles.DrawSectionHeader($"Mesh List ({_rows.Count} items)", _foldList, "\u25B2");
            if (!_foldList) return;

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                if (_rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tracked model assets found.", MessageType.Info);
                    return;
                }

                DrawListHeader();

                for (int i = 0; i < _rows.Count; i++)
                {
                    var r = _rows[i];
                    if (_onlySelected && !r.Selected) continue;
                    if (!PassSearch(r.Path, _search)) continue;

                    DrawRow(r);
                }
            }
        }

        private void DrawListHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(new GUIContent("Sel", "Row selection for batch operations."), GUILayout.Width(26));
                GUILayout.Label(new GUIContent("Name", "File name of the model asset."), GUILayout.Width(155));
                GUILayout.Label(new GUIContent("Size", "Tracked size (bytes), '~' means estimated."), GUILayout.Width(95));
                GUILayout.Label(new GUIContent("Compression", "ModelImporter mesh compression snapshot."), GUILayout.Width(125));
                GUILayout.Label(new GUIContent("Read/Write", "ModelImporter isReadable snapshot."), GUILayout.Width(95));
                GUILayout.Label(new GUIContent("Static Flags", "Static flags snapshot from the model root GameObject."), GUILayout.Width(125));
                GUILayout.FlexibleSpace();
            }
        }

        private void DrawRow(Row r)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 24);
            rect.x += 6;
            rect.y += 2;
            rect.height -= 4;

            Color bg = r.ImporterFound ? BridgeStyles.StatusGray : BridgeStyles.StatusRed;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), bg);

            float availableWidth = rect.width - 12;
            float buttonWidth = 108;
            float checkboxWidth = 22;
            float contentWidth = availableWidth - buttonWidth - checkboxWidth;

            bool compactMode = contentWidth < 400;
            bool veryCompactMode = contentWidth < 280;

            float x = rect.x + 4;

            // Checkbox
            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 2, 18, rect.height), r.Selected);
            x += checkboxWidth;

            string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(fileName)) fileName = "—";

            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";

            string compression = r.ImporterFound ? ShortCompression(r.MeshCompression) : "N/A";
            string readable = r.ImporterFound ? (r.IsReadable ? "R/W" : "—") : "N/A";
            string sf = r.RootFound ? ShortStaticFlags(r.RootStaticFlags) : "N/A";

            if (veryCompactMode)
            {
                float nameWidth = contentWidth * 0.65f;
                float sizeWidth = contentWidth * 0.35f;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 18), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
            }
            else if (compactMode)
            {
                float nameWidth = contentWidth * 0.4f;
                float sizeWidth = contentWidth * 0.25f;
                float compWidth = contentWidth * 0.35f;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 22), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                string compInfo = compression;
                if (readable == "R/W") compInfo += " R/W";
                EditorGUI.LabelField(new Rect(x, rect.y + 2, compWidth, rect.height), compInfo, EditorStyles.miniLabel);
            }
            else
            {
                float nameWidth = Mathf.Max(100, contentWidth * 0.25f);
                float sizeWidth = Mathf.Max(70, contentWidth * 0.14f);
                float compWidth = Mathf.Max(80, contentWidth * 0.18f);
                float rwWidth = Mathf.Max(40, contentWidth * 0.1f);
                float flagsWidth = Mathf.Max(80, contentWidth * 0.2f);

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 28), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, compWidth, rect.height), compression, EditorStyles.miniLabel);
                x += compWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, rwWidth, rect.height), readable, EditorStyles.miniLabel);
                x += rwWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, flagsWidth, rect.height), TruncateWithEllipsis(sf, 15), EditorStyles.miniLabel);
            }

            // Buttons
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
