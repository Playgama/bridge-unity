using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit.Tabs
{
    /// <summary>
    /// Editor tab that displays tracked shader assets from the latest build analysis.
    /// Shows shader sizes from the build report with search and selection helpers.
    /// </summary>
    public sealed class ShadersTab : ITab
    {
        public string TabName => "Shaders";

        private BuildInfo _buildInfo;

        private readonly List<Row> _rows = new List<Row>(512);

        private bool _needsRebuild = true;
        private bool _isRebuilding = false;

        private Vector2 _scrollPage;
        private Vector2 _scroll;
        private string _status = "";
        private string _search = "";
        private bool _onlySelected = false;

        private bool _foldHeader = true;
        private bool _foldList = true;

        private sealed class Row
        {
            public string Path;
            public long SizeBytes;
            public bool IsSizeEstimated;
            public bool Selected;
            public string ShaderName;
            public int PassCount;
            public int PropertyCount;
        }

        private static class UI
        {
            public static readonly GUIContent Refresh = new GUIContent(
                "Refresh",
                "Rebuild the shader list from the latest analysis data.");

            public static readonly GUIContent SearchLabel = new GUIContent(
                "Search",
                "Filter shaders by path (case-insensitive substring match).");

            public static readonly GUIContent OnlySelected = new GUIContent(
                "Only Selected",
                "Show only currently selected rows.");

            public static readonly GUIContent SelectAll = new GUIContent(
                "Select All",
                "Select every visible row.");

            public static readonly GUIContent Deselect = new GUIContent(
                "Deselect",
                "Clear selection for every row.");

            public static readonly GUIContent Invert = new GUIContent(
                "Invert",
                "Invert selection state for every row.");

            public static readonly GUIContent Ping = new GUIContent(
                "Ping",
                "Ping the asset in the Project window.");

            public static readonly GUIContent Select = new GUIContent(
                "Select",
                "Select the asset in the Project window.");
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
                    EditorGUILayout.HelpBox("Rebuilding shader list...", MessageType.Info);
                    return;
                }

                EnsureRebuilt();

                DrawList();

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

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
                EditorGUILayout.LabelField("Shaders can significantly impact build size and compile time.", SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
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

        private void DrawList()
        {
            _foldList = SuitStyles.DrawSectionHeader($"Shader List ({_rows.Count} items)", _foldList, "\u2726");
            if (!_foldList) return;

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                if (_rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tracked shader assets found.", MessageType.Info);
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

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), SuitStyles.StatusGray);

            Rect cb = new Rect(rect.x + 4, rect.y + 2, 18, rect.height);
            r.Selected = EditorGUI.Toggle(cb, r.Selected);

            // Shader name
            string displayName = !string.IsNullOrEmpty(r.ShaderName) ? r.ShaderName : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(displayName)) displayName = "—";
            Rect nameR = new Rect(rect.x + 26, rect.y + 2, 250, rect.height);
            EditorGUI.LabelField(nameR, new GUIContent(displayName, r.Path), EditorStyles.miniLabel);

            // Size
            Rect sizeR = new Rect(rect.x + 280, rect.y + 2, 95, rect.height);
            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";
            EditorGUI.LabelField(sizeR, size, EditorStyles.miniLabel);

            // Pass count
            Rect passR = new Rect(rect.x + 380, rect.y + 2, 80, rect.height);
            if (r.PassCount > 0)
                EditorGUI.LabelField(passR, $"{r.PassCount} passes", EditorStyles.miniLabel);

            // Property count
            Rect propR = new Rect(rect.x + 465, rect.y + 2, 80, rect.height);
            if (r.PropertyCount > 0)
                EditorGUI.LabelField(propR, $"{r.PropertyCount} props", EditorStyles.miniLabel);

            // Ping button
            Rect pingR = new Rect(rect.x + rect.width - 112, rect.y + 2, 50, rect.height);
            if (GUI.Button(pingR, UI.Ping))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }

            // Select button
            Rect selR = new Rect(rect.x + rect.width - 58, rect.y + 2, 55, rect.height);
            if (GUI.Button(selR, UI.Select))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) Selection.activeObject = obj;
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
                        if (a.Category != AssetCategory.Shaders) continue;
                        if (string.IsNullOrEmpty(a.Path)) continue;

                        var shader = AssetDatabase.LoadAssetAtPath<Shader>(a.Path);

                        var row = new Row
                        {
                            Path = a.Path,
                            SizeBytes = a.SizeBytes,
                            IsSizeEstimated = a.IsSizeEstimated,
                            Selected = false,
                            ShaderName = shader != null ? shader.name : "",
                            PassCount = 0,
                            PropertyCount = 0
                        };

                        // Try to get shader info
                        if (shader != null)
                        {
                            try
                            {
                                row.PassCount = shader.passCount;
                                row.PropertyCount = ShaderUtil.GetPropertyCount(shader);
                            }
                            catch { }
                        }

                        _rows.Add(row);
                    }

                    _rows.Sort((x, y) => y.SizeBytes.CompareTo(x.SizeBytes));
                    _status = $"Tracked shaders: {_rows.Count}";

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
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = v;
        }

        private void InvertSelection()
        {
            for (int i = 0; i < _rows.Count; i++) _rows[i].Selected = !_rows[i].Selected;
        }

        private static bool PassSearch(string path, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (string.IsNullOrEmpty(path)) return false;
            return path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
