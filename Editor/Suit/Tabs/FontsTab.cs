using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit.Tabs
{
    /// <summary>
    /// Editor tab that displays tracked font assets from the latest build analysis.
    /// Shows font sizes from the build report with search and selection helpers.
    /// </summary>
    public sealed class FontsTab : ITab
    {
        public string TabName => "Fonts";

        private BuildInfo _buildInfo;

        private readonly List<Row> _rows = new List<Row>(256);

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
            public string FontName;
            public string FontType;
            public bool IsTMP;
        }

        private static class UI
        {
            public static readonly GUIContent Refresh = new GUIContent(
                "Refresh",
                "Rebuild the font list from the latest analysis data.");

            public static readonly GUIContent SearchLabel = new GUIContent(
                "Search",
                "Filter fonts by path (case-insensitive substring match).");

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
                    EditorGUILayout.HelpBox("Rebuilding font list...", MessageType.Info);
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
                EditorGUILayout.LabelField("Fonts (especially TMP fonts with many characters) can be large.", SuitStyles.SubtitleStyle);
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
            _foldList = SuitStyles.DrawSectionHeader($"Font List ({_rows.Count} items)", _foldList, "\u0041");
            if (!_foldList) return;

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                if (_rows.Count == 0)
                {
                    EditorGUILayout.HelpBox("No tracked font assets found.", MessageType.Info);
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

            // TMP fonts often larger, highlight them
            Color bg = r.IsTMP ? SuitStyles.StatusYellow : SuitStyles.StatusGray;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), bg);

            Rect cb = new Rect(rect.x + 4, rect.y + 2, 18, rect.height);
            r.Selected = EditorGUI.Toggle(cb, r.Selected);

            // Font name
            string displayName = !string.IsNullOrEmpty(r.FontName) ? r.FontName : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(displayName)) displayName = "—";
            Rect nameR = new Rect(rect.x + 26, rect.y + 2, 200, rect.height);
            EditorGUI.LabelField(nameR, new GUIContent(displayName, r.Path), EditorStyles.miniLabel);

            // Size
            Rect sizeR = new Rect(rect.x + 230, rect.y + 2, 95, rect.height);
            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";
            EditorGUI.LabelField(sizeR, size, EditorStyles.miniLabel);

            // Font type
            Rect typeR = new Rect(rect.x + 330, rect.y + 2, 120, rect.height);
            EditorGUI.LabelField(typeR, r.FontType, EditorStyles.miniLabel);

            // TMP indicator
            if (r.IsTMP)
            {
                Rect tmpR = new Rect(rect.x + 455, rect.y + 2, 60, rect.height);
                EditorGUI.LabelField(tmpR, "TMP", EditorStyles.miniBoldLabel);
            }

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
                        if (a.Category != AssetCategory.Fonts) continue;
                        if (string.IsNullOrEmpty(a.Path)) continue;

                        var mainAsset = AssetDatabase.LoadMainAssetAtPath(a.Path);
                        string ext = System.IO.Path.GetExtension(a.Path).ToLowerInvariant();

                        var row = new Row
                        {
                            Path = a.Path,
                            SizeBytes = a.SizeBytes,
                            IsSizeEstimated = a.IsSizeEstimated,
                            Selected = false,
                            FontName = "",
                            FontType = "Unknown",
                            IsTMP = false
                        };

                        // Determine font type and name
                        if (mainAsset != null)
                        {
                            string typeName = mainAsset.GetType().Name;
                            row.FontName = mainAsset.name;

                            if (typeName == "TMP_FontAsset" || typeName == "FontAsset")
                            {
                                row.FontType = "TextMeshPro";
                                row.IsTMP = true;
                            }
                            else if (mainAsset is Font)
                            {
                                row.FontType = "Legacy Font";
                            }
                            else
                            {
                                row.FontType = typeName;
                            }
                        }
                        else
                        {
                            // Determine by extension
                            if (ext == ".ttf" || ext == ".otf")
                                row.FontType = "TrueType/OpenType";
                            else if (ext == ".fnt")
                                row.FontType = "Bitmap Font";
                            else if (ext == ".fontsettings")
                                row.FontType = "Font Settings";
                            else if (ext == ".asset")
                                row.FontType = "Font Asset";
                        }

                        _rows.Add(row);
                    }

                    _rows.Sort((x, y) => y.SizeBytes.CompareTo(x.SizeBytes));
                    _status = $"Tracked fonts: {_rows.Count}";

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
