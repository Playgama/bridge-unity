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

            // Calculate available width for content (excluding margins and buttons)
            float availableWidth = rect.width - 12;
            float buttonWidth = 108;
            float checkboxWidth = 22;
            float contentWidth = availableWidth - buttonWidth - checkboxWidth;

            // Determine layout mode based on available width
            bool compactMode = contentWidth < 350;
            bool veryCompactMode = contentWidth < 220;

            float x = rect.x + 4;

            // Checkbox
            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 2, 18, rect.height), r.Selected);
            x += checkboxWidth;

            // Shader name
            string displayName = !string.IsNullOrEmpty(r.ShaderName) ? r.ShaderName : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(displayName)) displayName = "—";

            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";

            if (veryCompactMode)
            {
                // Very compact: only name and size
                float nameWidth = contentWidth * 0.65f;
                float sizeWidth = contentWidth * 0.35f;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(displayName, 18), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
            }
            else if (compactMode)
            {
                // Compact: name, size, passes
                float nameWidth = contentWidth * 0.5f;
                float sizeWidth = contentWidth * 0.25f;
                float passWidth = contentWidth * 0.25f;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(displayName, 25), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                if (r.PassCount > 0)
                    EditorGUI.LabelField(new Rect(x, rect.y + 2, passWidth, rect.height), $"{r.PassCount}p", EditorStyles.miniLabel);
            }
            else
            {
                // Full layout: all columns
                float nameWidth = Mathf.Max(120, contentWidth * 0.4f);
                float sizeWidth = Mathf.Max(70, contentWidth * 0.18f);
                float passWidth = Mathf.Max(60, contentWidth * 0.18f);
                float propWidth = Mathf.Max(60, contentWidth * 0.18f);

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(displayName, 35), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                if (r.PassCount > 0)
                    EditorGUI.LabelField(new Rect(x, rect.y + 2, passWidth, rect.height), $"{r.PassCount} passes", EditorStyles.miniLabel);
                x += passWidth;

                if (r.PropertyCount > 0)
                    EditorGUI.LabelField(new Rect(x, rect.y + 2, propWidth, rect.height), $"{r.PropertyCount} props", EditorStyles.miniLabel);
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

        /// <summary>Truncates a string and adds ellipsis if it exceeds the max length.</summary>
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
