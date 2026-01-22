using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
{
    public sealed class AudioTab : ITab
    {
        public string TabName { get { return "Audio"; } }

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
        private bool _foldBatch = true;
        private bool _foldList = true;

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
        }

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
                "Select All",
                "Select every visible row (ignores the 'Only Selected' filter).");

            public static readonly GUIContent Deselect = new GUIContent(
                "Deselect",
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
                "Higher = better quality, usually larger download size.");

            public static readonly GUIContent ForceToMono = new GUIContent(
                "Force To Mono",
                "If enabled: converts stereo clips to mono on import.\n" +
                "Can reduce size, but may reduce spatial feeling if the clip relies on stereo.");

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
                    EditorGUILayout.HelpBox("Rebuilding audio list...", MessageType.Info);
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

                string tb = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
                if (_buildInfo.DataMode == BuildDataMode.DependenciesFallback) tb += " (estimated)";
                EditorGUILayout.LabelField("Tracked Bytes", tb);

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Batch operations modify AudioImporter settings and reimport assets.", BridgeStyles.SubtitleStyle);
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
                GUILayout.Label(UI.SearchLabel, GUILayout.Width(45));
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

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(UI.LoadType, GUILayout.Width(65));
                _loadType = (AudioClipLoadType)EditorGUILayout.EnumPopup(_loadType, GUILayout.MinWidth(120), GUILayout.MaxWidth(170));

                GUILayout.FlexibleSpace();

                GUILayout.Label(UI.Format, GUILayout.Width(50));
                _format = (AudioCompressionFormat)EditorGUILayout.EnumPopup(_format, GUILayout.MinWidth(80), GUILayout.MaxWidth(130));
            }

            GUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(UI.Quality, GUILayout.Width(50));
                _quality = EditorGUILayout.Slider(_quality, 0.0f, 1.0f, GUILayout.MinWidth(120), GUILayout.MaxWidth(250));
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                _forceToMono = EditorGUILayout.ToggleLeft(UI.ForceToMono, _forceToMono, GUILayout.MinWidth(100), GUILayout.MaxWidth(130));
                _preload = EditorGUILayout.ToggleLeft(UI.Preload, _preload, GUILayout.MinWidth(120), GUILayout.MaxWidth(160));

                GUILayout.FlexibleSpace();

                GUI.enabled = GetSelectedCount() > 0;
                if (GUILayout.Button(UI.ApplyToSelected, GUILayout.Height(26), GUILayout.MinWidth(100), GUILayout.MaxWidth(140)))
                    ApplyBatchToSelected();
                GUI.enabled = true;
            }

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Recommended: Vorbis, Compressed In Memory, Mono, Quality ~0.5-0.7", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();
        }

        private void DrawList()
        {
            _foldList = BridgeStyles.DrawSectionHeader($"Audio List ({_rows.Count} items)", _foldList, "\u266A");
            if (!_foldList) return;

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                if (_rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tracked audio assets found.", MessageType.Info);
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

            bool compactMode = contentWidth < 450;
            bool veryCompactMode = contentWidth < 300;

            float x = rect.x + 4;

            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 2, 18, rect.height), r.Selected);
            x += checkboxWidth;

            string fileName = string.IsNullOrEmpty(r.Path) ? "-" : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(fileName)) fileName = "-";

            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";

            if (veryCompactMode)
            {
                float nameWidth = contentWidth * 0.65f;
                float sizeWidth = contentWidth * 0.35f;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 20), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
            }
            else if (compactMode)
            {
                float nameWidth = contentWidth * 0.28f;
                float sizeWidth = contentWidth * 0.18f;
                float ltWidth = contentWidth * 0.3f;
                float fmtWidth = contentWidth * 0.24f;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 22), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, ltWidth, rect.height), TruncateLoadType(r.LoadType), EditorStyles.miniLabel);
                x += ltWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, fmtWidth, rect.height), r.Format.ToString(), EditorStyles.miniLabel);
            }
            else
            {
                float nameWidth = Mathf.Max(100, contentWidth * 0.2f);
                float sizeWidth = Mathf.Max(70, contentWidth * 0.12f);
                float ltWidth = Mathf.Max(100, contentWidth * 0.22f);
                float fmtWidth = Mathf.Max(80, contentWidth * 0.16f);
                float qWidth = Mathf.Max(50, contentWidth * 0.1f);
                float monoWidth = Mathf.Max(60, contentWidth * 0.12f);

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(fileName, 25), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, ltWidth, rect.height), r.LoadType.ToString(), EditorStyles.miniLabel);
                x += ltWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, fmtWidth, rect.height), r.Format.ToString(), EditorStyles.miniLabel);
                x += fmtWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, qWidth, rect.height), "Q " + r.Quality.ToString("0.00"), EditorStyles.miniLabel);
                x += qWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, monoWidth, rect.height), r.ForceToMono ? "Mono" : "Stereo", EditorStyles.miniLabel);
            }

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
