using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor.Tabs
{
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

        private bool _foldHeader = false;
        private bool _foldTips = true;
        private bool _foldList = true;

        // Sorting
        private enum SortMode { Size, Name, Type }
        private SortMode _sortMode = SortMode.Size;
        private bool _sortAscending = false;

        // Status counts
        private int _largeFontCount = 0;    // > 500KB
        private int _tmpFontCount = 0;
        private int _normalFontCount = 0;
        private long _totalFontBytes = 0;

        private const long LARGE_FONT_THRESHOLD = 500 * 1024; // 500KB

        private sealed class Row
        {
            public string Path;
            public long SizeBytes;
            public bool IsSizeEstimated;
            public bool Selected;
            public string FontName;
            public string FontType;
            public bool IsTMP;
            public StatusLevel Status;
        }

        private enum StatusLevel { Good, Warning, Critical }

        private static class UI
        {
            public static readonly GUIContent Refresh = new GUIContent("Refresh", "Rebuild the font list from the latest analysis data.");
            public static readonly GUIContent SearchLabel = new GUIContent("Search", "Filter fonts by path (case-insensitive substring match).");
            public static readonly GUIContent OnlySelected = new GUIContent("Only Selected", "Show only currently selected rows.");
            public static readonly GUIContent SelectAll = new GUIContent("Select All", "Select every visible row.");
            public static readonly GUIContent Deselect = new GUIContent("Deselect", "Clear selection for every row.");
            public static readonly GUIContent Invert = new GUIContent("Invert", "Invert selection state for every row.");
            public static readonly GUIContent Ping = new GUIContent("Ping", "Ping the asset in the Project window.");
            public static readonly GUIContent Select = new GUIContent("Select", "Select the asset in the Project window.");

            public static readonly GUIContent LargeFonts = new GUIContent(">500KB", "Select fonts larger than 500KB that may need optimization.");
            public static readonly GUIContent TMPFonts = new GUIContent("TMP", "Select TextMeshPro font assets.");

            public static readonly GUIContent SortSize = new GUIContent("Size", "Sort by file size.");
            public static readonly GUIContent SortName = new GUIContent("Name", "Sort alphabetically by name.");
            public static readonly GUIContent SortType = new GUIContent("Type", "Sort by font type.");
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

                if (_isRebuilding)
                {
                    EditorGUILayout.HelpBox("Rebuilding font list...", MessageType.Info);
                    return;
                }

                EnsureRebuilt();

                // Urgent callout for large fonts
                if (_largeFontCount > 0)
                {
                    DrawLargeFontWarning();
                }

                DrawTipsPanel();
                DrawToolbar();
                DrawList();

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private void DrawHeader()
        {
            string headerText = "Analysis Info";

            _foldHeader = BridgeStyles.DrawSectionHeader(headerText, _foldHeader, "\u2139");

            // Draw status badges
            if (_rows.Count > 0)
            {
                Rect lastRect = GUILayoutUtility.GetLastRect();
                float badgeX = lastRect.x + 140;

                if (_largeFontCount > 0)
                {
                    DrawStatusBadge(ref badgeX, lastRect.y + 4, $"{_largeFontCount} Large", BridgeStyles.StatusRed);
                }
                if (_tmpFontCount > 0)
                {
                    DrawStatusBadge(ref badgeX, lastRect.y + 4, $"{_tmpFontCount} TMP", BridgeStyles.StatusYellow);
                }
                if (_normalFontCount > 0)
                {
                    DrawStatusBadge(ref badgeX, lastRect.y + 4, $"{_normalFontCount} OK", BridgeStyles.StatusGreen);
                }
            }

            if (_foldHeader)
            {
                BridgeStyles.BeginCard();
                EditorGUILayout.LabelField("Analysis Mode", _buildInfo.DataMode.ToString());
                EditorGUILayout.LabelField("Tracked Assets", _buildInfo.TrackedAssetCount.ToString());

                string tb = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
                if (_buildInfo.DataMode == BuildDataMode.DependenciesFallback) tb += " (estimated)";
                EditorGUILayout.LabelField("Tracked Bytes", tb);

                if (_rows.Count > 0)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.LabelField($"Total Font Size: {SharedTypes.FormatBytes(_totalFontBytes)}", EditorStyles.boldLabel);
                }

                GUILayout.Space(4);
                EditorGUILayout.LabelField("Fonts (especially TMP fonts with many characters) can be large.", BridgeStyles.SubtitleStyle);
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

        private void DrawLargeFontWarning()
        {
            BridgeStyles.BeginCard();

            // Red warning background
            Rect warningRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(warningRect, new Color(0.6f, 0.15f, 0.15f, 0.4f));

            // Warning icon and text
            GUIStyle warningStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.4f, 0.4f) }
            };

            GUI.Label(new Rect(warningRect.x + 10, warningRect.y + 5, warningRect.width - 20, 25),
                $"⚠ {_largeFontCount} font(s) exceed 500KB!", warningStyle);

            GUIStyle descStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            GUI.Label(new Rect(warningRect.x + 10, warningRect.y + 28, warningRect.width - 20, 30),
                "Large fonts significantly impact download size. Consider stripping unused characters.", descStyle);

            BridgeStyles.EndCard();
        }

        private void DrawTipsPanel()
        {
            _foldTips = BridgeStyles.DrawSectionHeader("Font Optimization Tips", _foldTips, "\u26A1");
            if (!_foldTips) return;

            BridgeStyles.BeginCard();

            GUIStyle tipStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };

            EditorGUILayout.LabelField("<b>Strip Unused Characters:</b> For TMP fonts, use Font Asset Creator to include only needed character sets.", tipStyle);
            GUILayout.Space(2);

            EditorGUILayout.LabelField("<b>Use Static Fonts:</b> If you don't need runtime text generation, use static font atlases.", tipStyle);
            GUILayout.Space(2);

            EditorGUILayout.LabelField("<b>Subset Languages:</b> Include only the languages your game actually supports.", tipStyle);
            GUILayout.Space(2);

            EditorGUILayout.LabelField("<b>Reduce Atlas Size:</b> Smaller atlas resolution = smaller file size. 512x512 often suffices.", tipStyle);

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("TMP Font Asset optimization guide →", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/manual/FontAssets.html");
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
                if (GUILayout.Button(UI.LargeFonts, EditorStyles.toolbarButton, GUILayout.Width(60)))
                    SelectLargeFonts();

                if (GUILayout.Button(UI.TMPFonts, EditorStyles.toolbarButton, GUILayout.Width(40)))
                    SelectTMPFonts();

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
                bool nameActive = _sortMode == SortMode.Name;
                bool typeActive = _sortMode == SortMode.Type;

                if (GUILayout.Toggle(sizeActive, UI.SortSize, EditorStyles.toolbarButton, GUILayout.Width(40)) && !sizeActive)
                {
                    _sortMode = SortMode.Size;
                    _sortAscending = false;
                    SortRows();
                }
                if (GUILayout.Toggle(nameActive, UI.SortName, EditorStyles.toolbarButton, GUILayout.Width(45)) && !nameActive)
                {
                    _sortMode = SortMode.Name;
                    _sortAscending = true;
                    SortRows();
                }
                if (GUILayout.Toggle(typeActive, UI.SortType, EditorStyles.toolbarButton, GUILayout.Width(40)) && !typeActive)
                {
                    _sortMode = SortMode.Type;
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

            string listHeader = $"Font List ({_rows.Count} items)";
            if (selectedCount > 0) listHeader += $" • {selectedCount} selected";

            _foldList = BridgeStyles.DrawSectionHeader(listHeader, _foldList, "\u0041");
            if (!_foldList) return;

            // Column headers
            BridgeStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(26); // Checkbox space
                GUILayout.Label("Font Name", EditorStyles.boldLabel, GUILayout.Width(180));
                GUILayout.Label("Size", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.Label("Type", EditorStyles.boldLabel, GUILayout.Width(100));
                GUILayout.Label("Status", EditorStyles.boldLabel, GUILayout.Width(80));
                GUILayout.FlexibleSpace();
            }
            BridgeStyles.EndCard();

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll, GUILayout.Height(350)))
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
            Color bgColor = BridgeStyles.StatusGray;
            if (r.Status == StatusLevel.Critical) bgColor = new Color(0.5f, 0.2f, 0.2f, 0.3f);
            else if (r.Status == StatusLevel.Warning) bgColor = new Color(0.5f, 0.4f, 0.1f, 0.3f);

            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width - 12, rect.height), bgColor);

            // Selection highlight
            if (r.Selected)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3, rect.height), BridgeStyles.BrandPurple);
            }

            float x = rect.x + 4;

            // Checkbox
            r.Selected = EditorGUI.Toggle(new Rect(x, rect.y + 3, 18, rect.height), r.Selected);
            x += 22;

            // Font name
            string displayName = !string.IsNullOrEmpty(r.FontName) ? r.FontName : System.IO.Path.GetFileName(r.Path);
            if (string.IsNullOrEmpty(displayName)) displayName = "—";

            EditorGUI.LabelField(new Rect(x, rect.y + 3, 180, rect.height), new GUIContent(TruncateWithEllipsis(displayName, 25), r.Path), EditorStyles.miniLabel);
            x += 180;

            // Size with comparison indicator
            string size = SharedTypes.FormatBytes(r.SizeBytes);
            if (r.IsSizeEstimated) size += " ~";

            GUIStyle sizeStyle = EditorStyles.miniLabel;
            if (r.SizeBytes > LARGE_FONT_THRESHOLD)
            {
                sizeStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.4f, 0.4f) }, fontStyle = FontStyle.Bold };
            }
            EditorGUI.LabelField(new Rect(x, rect.y + 3, 80, rect.height), size, sizeStyle);
            x += 80;

            // Type
            string typeText = r.FontType;
            if (r.IsTMP) typeText = "TMP";
            EditorGUI.LabelField(new Rect(x, rect.y + 3, 100, rect.height), typeText, EditorStyles.miniLabel);
            x += 100;

            // Status indicator
            string statusText = "OK";
            Color statusColor = BridgeStyles.StatusGreen;
            if (r.Status == StatusLevel.Critical)
            {
                statusText = "Too Large";
                statusColor = BridgeStyles.StatusRed;
            }
            else if (r.Status == StatusLevel.Warning)
            {
                statusText = "TMP";
                statusColor = BridgeStyles.StatusYellow;
            }

            Rect statusRect = new Rect(x, rect.y + 4, 65, rect.height - 4);
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
                    _largeFontCount = 0;
                    _tmpFontCount = 0;
                    _normalFontCount = 0;
                    _totalFontBytes = 0;

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
                            if (ext == ".ttf" || ext == ".otf")
                                row.FontType = "TrueType/OpenType";
                            else if (ext == ".fnt")
                                row.FontType = "Bitmap Font";
                            else if (ext == ".fontsettings")
                                row.FontType = "Font Settings";
                            else if (ext == ".asset")
                                row.FontType = "Font Asset";
                        }

                        // Evaluate status
                        if (row.SizeBytes > LARGE_FONT_THRESHOLD)
                        {
                            row.Status = StatusLevel.Critical;
                            _largeFontCount++;
                        }
                        else if (row.IsTMP)
                        {
                            row.Status = StatusLevel.Warning;
                            _tmpFontCount++;
                        }
                        else
                        {
                            row.Status = StatusLevel.Good;
                            _normalFontCount++;
                        }

                        _totalFontBytes += row.SizeBytes;
                        _rows.Add(row);
                    }

                    SortRows();
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

        private void SortRows()
        {
            switch (_sortMode)
            {
                case SortMode.Size:
                    _rows.Sort((a, b) => _sortAscending ? a.SizeBytes.CompareTo(b.SizeBytes) : b.SizeBytes.CompareTo(a.SizeBytes));
                    break;
                case SortMode.Name:
                    _rows.Sort((a, b) =>
                    {
                        string nameA = !string.IsNullOrEmpty(a.FontName) ? a.FontName : a.Path;
                        string nameB = !string.IsNullOrEmpty(b.FontName) ? b.FontName : b.Path;
                        return _sortAscending ? string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase) : string.Compare(nameB, nameA, StringComparison.OrdinalIgnoreCase);
                    });
                    break;
                case SortMode.Type:
                    _rows.Sort((a, b) => _sortAscending ? string.Compare(a.FontType, b.FontType, StringComparison.OrdinalIgnoreCase) : string.Compare(b.FontType, a.FontType, StringComparison.OrdinalIgnoreCase));
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

        private void SelectLargeFonts()
        {
            SelectAll(false);
            foreach (var r in _rows)
            {
                if (r.SizeBytes > LARGE_FONT_THRESHOLD) r.Selected = true;
            }
        }

        private void SelectTMPFonts()
        {
            SelectAll(false);
            foreach (var r in _rows)
            {
                if (r.IsTMP) r.Selected = true;
            }
        }

        private static bool PassSearch(Row r, string search)
        {
            if (string.IsNullOrEmpty(search)) return true;
            if (!string.IsNullOrEmpty(r.Path) && r.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (!string.IsNullOrEmpty(r.FontName) && r.FontName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
