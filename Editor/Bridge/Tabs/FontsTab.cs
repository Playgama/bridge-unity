using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
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
                EditorGUILayout.LabelField("Fonts (especially TMP fonts with many characters) can be large.", BridgeStyles.SubtitleStyle);
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

        private void DrawList()
        {
            _foldList = BridgeStyles.DrawSectionHeader($"Font List ({_rows.Count} items)", _foldList, "\u0041");
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
            Color bg = r.IsTMP ? BridgeStyles.StatusYellow : BridgeStyles.StatusGray;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, rect.height), bg);

            // Calculate available width for content (excluding margins and buttons)
            float availableWidth = rect.width - 12;
            float buttonWidth = 108;
            float checkboxWidth = 22;
            float contentWidth = availableWidth - buttonWidth - checkboxWidth;

            // Determine layout mode based on available width
            bool compactMode = contentWidth < 320;
            bool veryCompactMode = contentWidth < 200;

            float x = rect.x + 4;

            // Checkbox
            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 2, 18, rect.height), r.Selected);
            x += checkboxWidth;

            // Font name
            string displayName = !string.IsNullOrEmpty(r.FontName) ? r.FontName : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(displayName)) displayName = "—";

            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";

            if (veryCompactMode)
            {
                // Very compact: only name and size
                float nameWidth = contentWidth * 0.6f;
                float sizeWidth = contentWidth * 0.4f;

                string name = displayName;
                if (r.IsTMP) name = "[TMP] " + name;
                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(name, 16), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
            }
            else if (compactMode)
            {
                // Compact: name, size, TMP indicator
                float nameWidth = contentWidth * 0.5f;
                float sizeWidth = contentWidth * 0.3f;
                float tmpWidth = contentWidth * 0.2f;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(displayName, 22), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                if (r.IsTMP)
                    EditorGUI.LabelField(new Rect(x, rect.y + 2, tmpWidth, rect.height), "TMP", EditorStyles.miniBoldLabel);
            }
            else
            {
                // Full layout: all columns
                float nameWidth = Mathf.Max(100, contentWidth * 0.35f);
                float sizeWidth = Mathf.Max(70, contentWidth * 0.18f);
                float typeWidth = Mathf.Max(80, contentWidth * 0.28f);
                float tmpWidth = Mathf.Max(40, contentWidth * 0.12f);

                EditorGUI.LabelField(new Rect(x, rect.y + 2, nameWidth, rect.height), new GUIContent(TruncateWithEllipsis(displayName, 28), r.Path), EditorStyles.miniLabel);
                x += nameWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, sizeWidth, rect.height), size, EditorStyles.miniLabel);
                x += sizeWidth;

                EditorGUI.LabelField(new Rect(x, rect.y + 2, typeWidth, rect.height), TruncateWithEllipsis(r.FontType, 15), EditorStyles.miniLabel);
                x += typeWidth;

                if (r.IsTMP)
                    EditorGUI.LabelField(new Rect(x, rect.y + 2, tmpWidth, rect.height), "TMP", EditorStyles.miniBoldLabel);
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
