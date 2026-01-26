using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor.Tabs
{
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

        private bool _foldHeader = false;
        private bool _foldTips = true;
        private bool _foldList = true;

        // Sorting
        private enum SortMode { Size, Passes, Name }
        private SortMode _sortMode = SortMode.Size;
        private bool _sortAscending = false;

        // Status counts
        private int _multiPassCount = 0;
        private int _highPassCount = 0;
        private int _optimizedCount = 0;

        private sealed class Row
        {
            public string Path;
            public long SizeBytes;
            public bool IsSizeEstimated;
            public bool Selected;
            public string ShaderName;
            public int PassCount;
            public int PropertyCount;
            public StatusLevel Status;
        }

        private enum StatusLevel { Good, Warning, Critical }

        private static class UI
        {
            public static readonly GUIContent Refresh = new GUIContent("Refresh", "Rebuild the shader list from the latest analysis data.");
            public static readonly GUIContent SearchLabel = new GUIContent("Search", "Filter shaders by path or name.");
            public static readonly GUIContent OnlySelected = new GUIContent("Only Selected", "Show only currently selected rows.");
            public static readonly GUIContent SelectAll = new GUIContent("Select All", "Select every visible row.");
            public static readonly GUIContent Deselect = new GUIContent("Deselect", "Clear selection for every row.");
            public static readonly GUIContent Invert = new GUIContent("Invert", "Invert selection state for every row.");
            public static readonly GUIContent Ping = new GUIContent("Ping", "Ping the asset in the Project window.");
            public static readonly GUIContent Select = new GUIContent("Select", "Select the asset in the Project window.");

            public static readonly GUIContent MultiPass = new GUIContent("Multi-Pass", "Select shaders with 2+ passes that increase draw calls.");
            public static readonly GUIContent HighPass = new GUIContent("4+ Passes", "Select shaders with 4+ passes (high impact).");

            public static readonly GUIContent SortSize = new GUIContent("Size", "Sort by file size.");
            public static readonly GUIContent SortPasses = new GUIContent("Passes", "Sort by pass count.");
            public static readonly GUIContent SortName = new GUIContent("Name", "Sort alphabetically by name.");

            public static readonly GUIContent TipsTitle = new GUIContent(
                "Shader Optimization Tips",
                "Recommendations for optimizing shaders in WebGL builds.");

            public static readonly GUIContent PropsTooltip = new GUIContent(
                "Props",
                "Number of shader properties (uniforms).\nMore properties = more memory and slightly slower uploads.");
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

                if (_isRebuilding)
                {
                    EditorGUILayout.HelpBox("Rebuilding shader list...", MessageType.Info);
                    return;
                }

                EnsureRebuilt();

                DrawTipsPanel();
                DrawToolbar();
                DrawList();

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private void DrawHeader()
        {
            // Build header with status badges
            string headerText = "Analysis Info";
            if (_rows.Count > 0)
            {
                headerText = $"Analysis Info  ";
            }

            _foldHeader = BridgeStyles.DrawSectionHeader(headerText, _foldHeader, "\u2139");

            // Draw status badges after header
            if (_rows.Count > 0)
            {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                float badgeX = lastRect.x + 140;

                if (_highPassCount > 0)
                {
                    DrawStatusBadge(ref badgeX, lastRect.y + 4, $"{_highPassCount} High", BridgeStyles.statusRed);
                }
                if (_multiPassCount > 0)
                {
                    DrawStatusBadge(ref badgeX, lastRect.y + 4, $"{_multiPassCount} Multi", BridgeStyles.statusYellow);
                }
                if (_optimizedCount > 0)
                {
                    DrawStatusBadge(ref badgeX, lastRect.y + 4, $"{_optimizedCount} OK", BridgeStyles.statusGreen);
                }
            }

            if (_foldHeader)
            {
                BridgeStyles.BeginCard();
                EditorGUILayout.LabelField("Analysis Mode", _buildInfo.dataMode.ToString());
                EditorGUILayout.LabelField("Tracked Assets", _buildInfo.trackedAssetCount.ToString());

                string tb = SharedTypes.FormatBytes(_buildInfo.trackedBytes);
                if (_buildInfo.dataMode == BuildDataMode.DependenciesFallback) tb += " (estimated)";
                EditorGUILayout.LabelField("Tracked Bytes", tb);

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Shaders are read-only in this view. Use tips below to optimize.", BridgeStyles.subtitleStyle);
                BridgeStyles.EndCard();
            }
        }

        private void DrawStatusBadge(ref float x, float y, string text, Color color)
        {
            GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            Vector2 size = badgeStyle.CalcSize(new GUIContent(text));
            size.x += 8;
            size.y = 16;

            Rect badgeRect = new Rect(x, y, size.x, size.y);
            EditorGUI.DrawRect(badgeRect, color);
            GUI.Label(badgeRect, text, badgeStyle);

            x += size.x + 4;
        }

        private void DrawTipsPanel()
        {
            _foldTips = BridgeStyles.DrawSectionHeader("Shader Optimization Tips", _foldTips, "\u26A1");
            if (!_foldTips) return;

            BridgeStyles.BeginCard();

            // Warning about read-only nature
            EditorGUILayout.HelpBox("Shaders cannot be batch-modified. Use these tips to optimize manually.", MessageType.Info);

            GUILayout.Space(4);

            // Tips list
            GUIStyle tipStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };

            EditorGUILayout.LabelField("<b>Multi-Pass Shaders:</b> Each pass = extra draw call. Consider single-pass alternatives.", tipStyle);
            GUILayout.Space(2);

            EditorGUILayout.LabelField("<b>Shader Variants:</b> Use #pragma skip_variants to exclude unused features.", tipStyle);
            GUILayout.Space(2);

            EditorGUILayout.LabelField("<b>Mobile Shaders:</b> Use Mobile/ or Unlit/ shaders for better WebGL performance.", tipStyle);
            GUILayout.Space(2);

            EditorGUILayout.LabelField("<b>Shader Properties:</b> Fewer properties = less memory. Remove unused ones.", tipStyle);

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Learn about shader optimization →", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://docs.unity3d.com/Manual/SL-ShaderPerformance.html");
                }
                GUILayout.FlexibleSpace();
            }

            BridgeStyles.EndCard();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(UI.Refresh, EditorStyles.toolbarButton, GUILayout.Width(70)))
                    RequestRebuild("User Refresh");

                GUILayout.Space(4);

                // Quick filters
                if (GUILayout.Button(UI.MultiPass, EditorStyles.toolbarButton, GUILayout.Width(75)))
                    SelectMultiPass();

                if (GUILayout.Button(UI.HighPass, EditorStyles.toolbarButton, GUILayout.Width(70)))
                    SelectHighPass();

                GUILayout.Space(8);
                GUILayout.Label(UI.SearchLabel, GUILayout.Width(45));
                string newSearch = GUILayout.TextField(_search, EditorStyles.toolbarTextField, GUILayout.MinWidth(100));
                if (newSearch != _search) _search = newSearch;

                GUILayout.Space(4);
                _onlySelected = GUILayout.Toggle(_onlySelected, UI.OnlySelected, EditorStyles.toolbarButton, GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                // Sorting controls
                GUILayout.Label("Sort:", EditorStyles.miniLabel, GUILayout.Width(30));

                bool sizeActive = _sortMode == SortMode.Size;
                bool passActive = _sortMode == SortMode.Passes;
                bool nameActive = _sortMode == SortMode.Name;

                if (GUILayout.Toggle(sizeActive, UI.SortSize, EditorStyles.toolbarButton, GUILayout.Width(40)) && !sizeActive)
                {
                    _sortMode = SortMode.Size;
                    _sortAscending = false;
                    SortRows();
                }
                if (GUILayout.Toggle(passActive, UI.SortPasses, EditorStyles.toolbarButton, GUILayout.Width(50)) && !passActive)
                {
                    _sortMode = SortMode.Passes;
                    _sortAscending = false;
                    SortRows();
                }
                if (GUILayout.Toggle(nameActive, UI.SortName, EditorStyles.toolbarButton, GUILayout.Width(45)) && !nameActive)
                {
                    _sortMode = SortMode.Name;
                    _sortAscending = true;
                    SortRows();
                }

                string arrow = _sortAscending ? "▲" : "▼";
                if (GUILayout.Button(arrow, EditorStyles.toolbarButton, GUILayout.Width(22)))
                {
                    _sortAscending = !_sortAscending;
                    SortRows();
                }

                GUILayout.Space(4);

                if (GUILayout.Button(UI.SelectAll, EditorStyles.toolbarButton, GUILayout.Width(70))) SelectAll(true);
                if (GUILayout.Button(UI.Deselect, EditorStyles.toolbarButton, GUILayout.Width(60))) SelectAll(false);
                if (GUILayout.Button(UI.Invert, EditorStyles.toolbarButton, GUILayout.Width(50))) InvertSelection();
            }
        }

        private void DrawList()
        {
            int selectedCount = 0;
            foreach (var r in _rows) if (r.Selected) selectedCount++;

            string listHeader = $"Shader List ({_rows.Count} items)";
            if (selectedCount > 0) listHeader += $" • {selectedCount} selected";

            _foldList = BridgeStyles.DrawSectionHeader(listHeader, _foldList, "\u2726");
            if (!_foldList) return;

            // Column headers
            BridgeStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(26); // Checkbox space
                GUILayout.Label("Shader Name", EditorStyles.boldLabel, GUILayout.Width(200));
                GUILayout.Label("Size", EditorStyles.boldLabel, GUILayout.Width(70));
                GUILayout.Label(new GUIContent("Passes", "Number of render passes.\n⚠ Multiple passes = more draw calls = slower rendering."), EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label(UI.PropsTooltip, EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label("Status", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
            }
            BridgeStyles.EndCard();

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Height(400)))
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
                    if (!PassSearch(r, _search)) continue;

                    DrawRow(r);
                }
            }
        }

        private void DrawRow(Row r)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 26);
            rect.x += 6;
            rect.y += 2;
            rect.height -= 4;

            // Background color based on status
            Color bgColor = BridgeStyles.statusGray;
            if (r.Status == StatusLevel.Critical) bgColor = new Color(0.5f, 0.2f, 0.2f, 0.3f);
            else if (r.Status == StatusLevel.Warning) bgColor = new Color(0.5f, 0.4f, 0.1f, 0.3f);

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width - 12, rect.height), bgColor);

            // Selection highlight
            if (r.Selected)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), BridgeStyles.brandPurple);
            }

            float x = rect.x + 4;

            // Checkbox
            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 3, 18, rect.height), r.Selected);
            x += 22;

            // Shader name
            string displayName = !string.IsNullOrEmpty(r.ShaderName) ? r.ShaderName : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(displayName)) displayName = "—";

            EditorGUI.LabelField(new Rect(x, rect.y + 3, 200, rect.height), new GUIContent(TruncateWithEllipsis(displayName, 30), r.Path), EditorStyles.miniLabel);
            x += 200;

            // Size
            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";
            EditorGUI.LabelField(new Rect(x, rect.y + 3, 70, rect.height), size, EditorStyles.miniLabel);
            x += 70;

            // Passes with warning
            string passText = r.PassCount > 0 ? r.PassCount.ToString() : "—";
            GUIStyle passStyle = EditorStyles.miniLabel;
            if (r.PassCount >= 4)
            {
                passText = "⚠ " + passText;
                passStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
            }
            else if (r.PassCount >= 2)
            {
                passText = "⚠ " + passText;
                passStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.8f, 0.2f) } };
            }
            EditorGUI.LabelField(new Rect(x, rect.y + 3, 60, rect.height), new GUIContent(passText, r.PassCount >= 2 ? "Multiple passes increase draw calls" : ""), passStyle);
            x += 60;

            // Props
            string propText = r.PropertyCount > 0 ? r.PropertyCount.ToString() : "—";
            EditorGUI.LabelField(new Rect(x, rect.y + 3, 60, rect.height), propText, EditorStyles.miniLabel);
            x += 60;

            // Status indicator
            string statusText = "OK";
            Color statusColor = BridgeStyles.statusGreen;
            if (r.Status == StatusLevel.Critical)
            {
                statusText = "High Impact";
                statusColor = BridgeStyles.statusRed;
            }
            else if (r.Status == StatusLevel.Warning)
            {
                statusText = "Multi-Pass";
                statusColor = BridgeStyles.statusYellow;
            }

            Rect statusRect = new Rect(x, rect.y + 4, 70, rect.height - 4);
            EditorGUI.DrawRect(statusRect, statusColor);
            GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(statusRect, statusText, statusStyle);

            // Buttons
            Rect pingR = new Rect(rect.x + rect.width - 124, rect.y + 3, 50, rect.height - 2);
            if (GUI.Button(pingR, UI.Ping))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(r.Path);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }

            Rect selR = new Rect(rect.x + rect.width - 70, rect.y + 3, 55, rect.height - 2);
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
                    _multiPassCount = 0;
                    _highPassCount = 0;
                    _optimizedCount = 0;

                    for (int i = 0; i < _buildInfo.assets.Count; i++)
                    {
                        var a = _buildInfo.assets[i];
                        if (a == null) continue;
                        if (a.category != AssetCategory.Shaders) continue;
                        if (string.IsNullOrEmpty(a.path)) continue;

                        var shader = AssetDatabase.LoadAssetAtPath<Shader>(a.path);

                        var row = new Row
                        {
                            Path = a.path,
                            SizeBytes = a.sizeBytes,
                            IsSizeEstimated = a.isSizeEstimated,
                            Selected = false,
                            ShaderName = shader != null ? shader.name : "",
                            PassCount = 0,
                            PropertyCount = 0
                        };

                        if (shader != null)
                        {
                            try
                            {
                                row.PassCount = shader.passCount;
                                row.PropertyCount = shader.GetPropertyCount();
                            }
                            catch { }
                        }

                        // Evaluate status
                        if (row.PassCount >= 4)
                        {
                            row.Status = StatusLevel.Critical;
                            _highPassCount++;
                        }
                        else if (row.PassCount >= 2)
                        {
                            row.Status = StatusLevel.Warning;
                            _multiPassCount++;
                        }
                        else
                        {
                            row.Status = StatusLevel.Good;
                            _optimizedCount++;
                        }

                        _rows.Add(row);
                    }

                    SortRows();
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

        private void SortRows()
        {
            switch (_sortMode)
            {
                case SortMode.Size:
                    _rows.Sort((a, b) => _sortAscending ? a.SizeBytes.CompareTo(b.SizeBytes) : b.SizeBytes.CompareTo(a.SizeBytes));
                    break;
                case SortMode.Passes:
                    _rows.Sort((a, b) => _sortAscending ? a.PassCount.CompareTo(b.PassCount) : b.PassCount.CompareTo(a.PassCount));
                    break;
                case SortMode.Name:
                    _rows.Sort((a, b) =>
                    {
                        string nameA = !string.IsNullOrEmpty(a.ShaderName) ? a.ShaderName : a.Path;
                        string nameB = !string.IsNullOrEmpty(b.ShaderName) ? b.ShaderName : b.Path;
                        return _sortAscending ? string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase) : string.Compare(nameB, nameA, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
            }
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

        private void SelectMultiPass()
        {
            SelectAll(false);
            foreach (var r in _rows)
            {
                if (r.PassCount >= 2) r.Selected = true;
            }
        }

        private void SelectHighPass()
        {
            SelectAll(false);
            foreach (var r in _rows)
            {
                if (r.PassCount >= 4) r.Selected = true;
            }
        }

        private static bool PassSearch(Row r, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (!string.IsNullOrEmpty(r.Path) && r.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(r.ShaderName) && r.ShaderName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
