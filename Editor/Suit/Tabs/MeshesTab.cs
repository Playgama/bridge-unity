using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit.Tabs
{
    /// <summary>
    /// Editor tab that displays tracked mesh/model assets from the latest build analysis.
    /// Provides:
    /// - A size-sorted list of model assets with a best-effort importer snapshot.
    /// - Search + selection helpers.
    /// - Batch operations for ModelImporter settings (Mesh Compression, Read/Write).
    /// - Optional application of Static Flags to the imported model root GameObject
    ///   (deprecated static flags are intentionally excluded from the selector).
    /// </summary>
    public sealed class MeshesTab : ITab
    {
        /// <summary>Displayed name of the tab in the parent UI.</summary>
        public string TabName { get { return "Meshes"; } }

        /// <summary>Latest analysis/build data provided by the hosting window.</summary>
        private BuildInfo _buildInfo;

        /// <summary>
        /// UI row model for a single tracked model asset.
        /// Values are extracted from analysis data (size/path) and importer/root GameObject snapshots.
        /// </summary>
        private sealed class Row
        {
            /// <summary>AssetDatabase path (e.g., "Assets/Models/Tree.fbx").</summary>
            public string Path;

            /// <summary>Tracked size from analysis (bytes). May be estimated depending on analysis mode.</summary>
            public long SizeBytes;

            /// <summary>True if the size value is an estimate rather than an exact measurement.</summary>
            public bool IsSizeEstimated;

            /// <summary>UI selection flag used by batch operations.</summary>
            public bool Selected;

            /// <summary>True if a ModelImporter was successfully resolved for this path.</summary>
            public bool ImporterFound;

            /// <summary>Snapshot: whether Read/Write is enabled on the importer (imp.isReadable).</summary>
            public bool IsReadable;

            /// <summary>Snapshot: mesh compression setting on the importer.</summary>
            public ModelImporterMeshCompression MeshCompression;

            /// <summary>True if the imported model root GameObject was resolved.</summary>
            public bool RootFound;

            /// <summary>Snapshot: static flags on the imported model root GameObject.</summary>
            public StaticEditorFlags RootStaticFlags;
        }

        /// <summary>Cached UI rows representing tracked model assets.</summary>
        private readonly List<Row> _rows = new List<Row>(1024);

        /// <summary>
        /// When true, the next repaint will schedule a rebuild of the internal row cache.
        /// This avoids doing heavy work directly inside OnGUI.
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

        // Foldout states for collapsible sections.
        private bool _foldHeader = true;
        private bool _foldBatch = true;
        private bool _foldList = true;

        /// <summary>Case-insensitive filter applied to asset paths.</summary>
        private string _search = "";

        /// <summary>If true, the list view only renders rows that are currently selected.</summary>
        private bool _onlySelected = false;

        /// <summary>Batch: desired Read/Write value when applying Read/Write changes.</summary>
        private bool _setReadable = false;

        /// <summary>Batch: enable/disable applying Read/Write during batch operations.</summary>
        private bool _applyReadable = true;

        /// <summary>Batch: desired mesh compression value when applying compression changes.</summary>
        private ModelImporterMeshCompression _compression = ModelImporterMeshCompression.Medium;

        /// <summary>Batch: enable/disable applying mesh compression during batch operations.</summary>
        private bool _applyCompression = true;

        /// <summary>Batch: static flags to set on the imported model root (when enabled).</summary>
        private StaticEditorFlags _staticFlagsToSet = 0;

        /// <summary>Batch: enable/disable applying static flags to the imported model root GameObject.</summary>
        private bool _applyStaticFlags = false;

        /// <summary>
        /// Deprecated static flags are intentionally excluded from the UI selector.
        /// We filter by name (string) to avoid compile-time references that may trigger obsolete warnings.
        /// </summary>
        private static readonly HashSet<string> kDeprecatedStaticNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NavigationStatic",
            "OffMeshLinkGeneration"
        };

        /// <summary>Centralized GUIContent labels with tooltips for consistent IMGUI.</summary>
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

        /// <summary>
        /// Displays analysis context and explains what batch operations modify.
        /// </summary>
        private void DrawHeader()
        {
            _foldHeader = SuitStyles.DrawSectionHeader("Analysis Info", _foldHeader, "\u2139");
            if (_foldHeader)
            {
                SuitStyles.BeginCard();
                EditorGUILayout.LabelField("Analysis Mode", _buildInfo.DataMode.ToString());
                EditorGUILayout.LabelField("Tracked Assets", _buildInfo.TrackedAssetCount.ToString());

                string tracked = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
                if (_buildInfo.DataMode == BuildDataMode.DependenciesFallback) tracked += " (estimated)";
                EditorGUILayout.LabelField("Tracked Bytes", tracked);

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Batch operations: Mesh Compression, Read/Write, Static Flags", SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
            }
        }

        /// <summary>
        /// Toolbar with quick actions and filters:
        /// - Refresh (rebuild list)
        /// - Search (path filter)
        /// - Only Selected (view filter)
        /// - Selection helpers (select all / deselect / invert)
        /// </summary>
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

        /// <summary>
        /// Batch settings panel:
        /// - Enables/disables importer changes (compression, read/write)
        /// - Optional static flags application to model root GameObject
        /// - Applies changes to currently selected rows
        /// </summary>
        private void DrawBatchPanel()
        {
            _foldBatch = SuitStyles.DrawSectionHeader("Batch Apply", _foldBatch, "\u2699");
            if (!_foldBatch) return;

            SuitStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                _applyCompression = EditorGUILayout.ToggleLeft(UI.ApplyMeshCompression, _applyCompression, GUILayout.Width(170));
                GUI.enabled = _applyCompression;
                _compression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup(UI.MeshCompression, _compression, GUILayout.Width(220));
                GUI.enabled = true;

                GUILayout.Space(12);

                _applyReadable = EditorGUILayout.ToggleLeft(UI.ApplyReadWrite, _applyReadable, GUILayout.Width(140));
                GUI.enabled = _applyReadable;
                _setReadable = EditorGUILayout.ToggleLeft(UI.ReadWriteEnabled, _setReadable, GUILayout.Width(160));
                GUI.enabled = true;
            }

            GUILayout.Space(8);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                _applyStaticFlags = EditorGUILayout.ToggleLeft(UI.ApplyStaticFlags, _applyStaticFlags);
                GUI.enabled = _applyStaticFlags;

                DrawStaticFlagsSelector(ref _staticFlagsToSet);

                GUI.enabled = true;

                EditorGUILayout.LabelField("Static flags are applied to the imported model root GameObject.", SuitStyles.SubtitleStyle);
            }

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                int selected = GetSelectedCount();
                GUI.enabled = selected > 0 && (_applyCompression || _applyReadable || _applyStaticFlags);

                if (GUILayout.Button(UI.ApplyToSelected, GUILayout.Height(28), GUILayout.Width(160)))
                    ApplyBatchToSelected();

                GUI.enabled = true;

                GUILayout.Label(UI.SelectedCount, EditorStyles.miniLabel);
                GUILayout.Label(selected.ToString(), EditorStyles.miniLabel);
            }
            SuitStyles.EndCard();
        }

        /// <summary>
        /// Custom checklist UI for StaticEditorFlags that:
        /// - Enumerates flags by name to avoid direct references to deprecated members.
        /// - Skips "None" and known deprecated flag names.
        /// - Provides "All" and "None" quick actions.
        /// </summary>
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

        /// <summary>
        /// Renders the list view:
        /// - Applies "Only Selected" and search filter at draw time
        /// - Shows a compact summary per row with quick actions (Ping/Select)
        /// </summary>
        private void DrawList()
        {
            _foldList = SuitStyles.DrawSectionHeader($"Mesh List ({_rows.Count} items)", _foldList, "\u25B2");
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

        /// <summary>Draws the list header row.</summary>
        private void DrawListHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUILayout.Label(new GUIContent("Sel", "Row selection for batch operations."), GUILayout.Width(30));
                GUILayout.Label(new GUIContent("Size", "Tracked size (bytes), '~' means estimated."), GUILayout.Width(90));
                GUILayout.Label(new GUIContent("Compression", "ModelImporter mesh compression snapshot."), GUILayout.Width(120));
                GUILayout.Label(new GUIContent("Read/Write", "ModelImporter isReadable snapshot."), GUILayout.Width(90));
                GUILayout.Label(new GUIContent("Static Flags", "Static flags snapshot from the model root GameObject."), GUILayout.Width(120));
                GUILayout.Label(new GUIContent("Path", "AssetDatabase path of the model asset."), GUILayout.ExpandWidth(true));
            }
        }

        /// <summary>
        /// Draws a single row:
        /// - background highlights missing importers
        /// - selection checkbox
        /// - file name
        /// - size + importer snapshot
        /// - static flags snapshot (short form)
        /// - Ping/Select shortcuts
        /// </summary>
        private void DrawRow(Row r)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 24);
            rect.x += 6;
            rect.y += 2;
            rect.height -= 4;

            Color bg = r.ImporterFound ? SuitStyles.StatusGray : SuitStyles.StatusRed;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), bg);

            float x = rect.x + 4;

            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 2, 18, rect.height), r.Selected);
            x += 26;

            // Mesh file name (extracted from path)
            string fileName = string.IsNullOrEmpty(r.Path) ? "—" : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(fileName)) fileName = "—";
            EditorGUI.LabelField(new Rect(x, rect.y + 2, 150, rect.height), new GUIContent(fileName, r.Path), EditorStyles.miniLabel);
            x += 155;

            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";
            EditorGUI.LabelField(new Rect(x, rect.y + 2, 85, rect.height), size, EditorStyles.miniLabel);
            x += 95;

            EditorGUI.LabelField(
                new Rect(x, rect.y + 2, 115, rect.height),
                r.ImporterFound ? r.MeshCompression.ToString() : "N/A",
                EditorStyles.miniLabel);
            x += 125;

            EditorGUI.LabelField(
                new Rect(x, rect.y + 2, 85, rect.height),
                r.ImporterFound ? (r.IsReadable ? "ON" : "OFF") : "N/A",
                EditorStyles.miniLabel);
            x += 95;

            string sf = r.RootFound ? ShortStaticFlags(r.RootStaticFlags) : "N/A";
            EditorGUI.LabelField(new Rect(x, rect.y + 2, 115, rect.height), sf, EditorStyles.miniLabel);
            x += 125;

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
        /// Creates a short, readable summary of static flags without including deprecated names.
        /// Limits output length for list readability.
        /// </summary>
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

        /// <summary>
        /// Ensures the row cache is built.
        /// The rebuild is scheduled via EditorApplication.delayCall to keep OnGUI fast.
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
                        if (string.IsNullOrEmpty(a.Path)) continue;

                        // Accept both "Meshes" and "Models" categories if present in the analysis model.
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

                        // Imported model root (asset root) is loaded as a GameObject for static flag snapshot.
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
        /// Applies enabled batch settings to all selected rows:
        /// - ModelImporter changes (Read/Write, Mesh Compression)
        /// - Optional static flags on model root GameObject
        /// Importer changes trigger reimport so settings take effect.
        /// </summary>
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
                    Undo.SetCurrentGroupName("Suit Mesh Batch Apply");

                    AssetDatabase.StartAssetEditing();

                    int changedImporters = 0;
                    int changedStatic = 0;
                    int skipped = 0;

                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (i % 8 == 0)
                            EditorUtility.DisplayProgressBar(
                                "Playgama Suit",
                                "Applying mesh settings...",
                                i / Mathf.Max(1f, (float)paths.Count));

                        string path = paths[i];

                        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                        if (imp != null)
                        {
                            Undo.RecordObject(imp, "Suit ModelImporter Change");

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
                                Undo.RecordObject(go, "Suit Static Flags Change");

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
                                    "Playgama Suit",
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

        /// <summary>
        /// Marks the internal row cache as stale and sets a user-facing status message.
        /// </summary>
        private void RequestRebuild(string reason)
        {
            _needsRebuild = true;
            _status = "Rebuild requested: " + reason;
        }

        /// <summary>Select or deselect every cached row.</summary>
        private void SelectAll(bool v)
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = v;
        }

        /// <summary>Invert selection state for every cached row.</summary>
        private void InvertSelection()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = !_rows[i].Selected;
        }

        /// <summary>Returns the number of selected rows.</summary>
        private int GetSelectedCount()
        {
            int c = 0;
            for (int i = 0; i < _rows.Count; i++) if (_rows[i].Selected) c++;
            return c;
        }

        /// <summary>Collects AssetDatabase paths for selected rows.</summary>
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

        /// <summary>Case-insensitive substring search against the asset path.</summary>
        private static bool PassSearch(string path, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
