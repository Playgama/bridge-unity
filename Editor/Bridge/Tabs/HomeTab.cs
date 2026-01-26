using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor.Tabs
{
    public sealed class HomeTab : ITab
    {
        public string TabName => "Home";

        private BuildInfo _buildInfo;
        private Vector2 _scroll;

        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _versionStyle;
        private static GUIStyle _menuButtonStyle;
        private static GUIStyle _menuDescStyle;
        private static GUIStyle _statusCardStyle;
        private static GUIStyle _statusValueStyle;
        private static GUIStyle _statusLabelStyle;
        private static Texture2D _headerBgTexture;

        private static string _cachedVersion;
        private static bool _versionLoaded;
        private const string DocsUrl = "https://wiki.playgama.com/playgama/sdk/engines/unity/intro";
        private const string SupportUrl = "https://discord.gg/TZg3rF3sdT";

        // Cached issue counts for status indicators
        private int _textureIssues = 0;
        private int _audioIssues = 0;
        private int _meshIssues = 0;
        private int _shaderIssues = 0;
        private int _fontIssues = 0;
        private int _buildSettingsIssues = 0;
        private int _totalWarnings = 0;
        private int _totalSuggestions = 0;

        // Category size breakdown for chart
        private long _textureBytes = 0;
        private long _audioBytes = 0;
        private long _meshBytes = 0;
        private long _shaderBytes = 0;
        private long _fontBytes = 0;
        private long _otherBytes = 0;
        private int _textureCount = 0;
        private int _audioCount = 0;
        private int _meshCount = 0;
        private int _shaderCount = 0;
        private int _fontCount = 0;
        private int _otherCount = 0;

        // Chart colors
        private static readonly Color ChartTextures = new Color(0.4f, 0.7f, 0.9f);
        private static readonly Color ChartAudio = new Color(0.9f, 0.6f, 0.4f);
        private static readonly Color ChartMeshes = new Color(0.5f, 0.8f, 0.5f);
        private static readonly Color ChartShaders = new Color(0.8f, 0.5f, 0.8f);
        private static readonly Color ChartFonts = new Color(0.9f, 0.8f, 0.4f);
        private static readonly Color ChartOther = new Color(0.6f, 0.6f, 0.6f);

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
            AnalyzeIssues();
        }

        private void AnalyzeIssues()
        {
            _textureIssues = 0;
            _audioIssues = 0;
            _meshIssues = 0;
            _shaderIssues = 0;
            _fontIssues = 0;
            _buildSettingsIssues = 0;

            // Reset category sizes
            _textureBytes = 0;
            _audioBytes = 0;
            _meshBytes = 0;
            _shaderBytes = 0;
            _fontBytes = 0;
            _otherBytes = 0;
            _textureCount = 0;
            _audioCount = 0;
            _meshCount = 0;
            _shaderCount = 0;
            _fontCount = 0;
            _otherCount = 0;

            if (_buildInfo == null || !_buildInfo.HasData || _buildInfo.Assets == null)
                return;

            foreach (var asset in _buildInfo.Assets)
            {
                switch (asset.Category)
                {
                    case AssetCategory.Textures:
                        _textureBytes += asset.SizeBytes;
                        _textureCount++;
                        if (asset.SizeBytes > 1024 * 1024) _textureIssues++;
                        break;
                    case AssetCategory.Audio:
                        _audioBytes += asset.SizeBytes;
                        _audioCount++;
                        if (asset.SizeBytes > 512 * 1024) _audioIssues++;
                        break;
                    case AssetCategory.Meshes:
                    case AssetCategory.Models:
                        _meshBytes += asset.SizeBytes;
                        _meshCount++;
                        if (asset.SizeBytes > 256 * 1024) _meshIssues++;
                        break;
                    case AssetCategory.Shaders:
                        _shaderBytes += asset.SizeBytes;
                        _shaderCount++;
                        if (asset.SizeBytes > 100 * 1024) _shaderIssues++;
                        break;
                    case AssetCategory.Fonts:
                        _fontBytes += asset.SizeBytes;
                        _fontCount++;
                        if (asset.SizeBytes > 500 * 1024) _fontIssues++;
                        break;
                    default:
                        _otherBytes += asset.SizeBytes;
                        _otherCount++;
                        break;
                }
            }

            // Check build settings
            if (EditorUserBuildSettings.development)
                _buildSettingsIssues++;

            _totalWarnings = _textureIssues + _audioIssues + _meshIssues + _shaderIssues + _fontIssues;
            _totalSuggestions = _buildSettingsIssues;
        }

        public void OnGUI()
        {
            EnsureStyles();

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawHeader();
                GUILayout.Space(12);
                DrawTemplateWarning();
                DrawStatusDashboard();
                GUILayout.Space(12);
                DrawBuildBreakdown();
                GUILayout.Space(16);
                DrawGetStarted();
                GUILayout.Space(16);
                DrawFeatures();
                GUILayout.Space(16);
                DrawResources();
                GUILayout.Space(20);
                DrawFooter();
            }
        }

        private void DrawHeader()
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 120);

            EditorGUI.DrawRect(headerRect, new Color(0.15f, 0.15f, 0.18f));

            Rect accentRect = new Rect(headerRect.x, headerRect.y, headerRect.width, 4);
            EditorGUI.DrawRect(accentRect, BridgeStyles.BrandPurple);

            Rect titleRect = new Rect(headerRect.x + 20, headerRect.y + 25, headerRect.width - 40, 40);
            EditorGUI.LabelField(titleRect, "Playgama Bridge", _titleStyle);

            Rect subRect = new Rect(headerRect.x + 20, headerRect.y + 65, headerRect.width - 40, 24);
            EditorGUI.LabelField(subRect, "Cross-Platform Game Publishing SDK", _subtitleStyle);

            string version = GetPackageVersion();
            float versionWidth = version.Length > 6 ? 70 : 60;
            Rect versionRect = new Rect(headerRect.x + headerRect.width - versionWidth - 20, headerRect.y + 30, versionWidth, 20);
            EditorGUI.DrawRect(versionRect, BridgeStyles.BrandPurple);
            EditorGUI.LabelField(versionRect, "v" + version, _versionStyle);
        }

        private void DrawTemplateWarning()
        {
            // Check if Bridge template is installed
            string templatePath = Path.Combine(Application.dataPath, "WebGLTemplates/Bridge/index.html");
            bool templateInstalled = File.Exists(templatePath);

            // Check current WebGL template setting
            string currentTemplate = PlayerSettings.WebGL.template;
            bool isCorrectTemplate = currentTemplate == "PROJECT:Bridge";

            // If template is correct, don't show anything
            if (isCorrectTemplate)
                return;

            // Show warning/error box
            Rect warningRect = EditorGUILayout.GetControlRect(false, templateInstalled ? 60 : 75);

            // Red/orange background for error
            Color bgColor = templateInstalled ? new Color(0.5f, 0.3f, 0.15f) : new Color(0.5f, 0.2f, 0.2f);
            EditorGUI.DrawRect(warningRect, bgColor);

            // Left accent bar
            Color accentColor = templateInstalled ? BridgeStyles.StatusYellow : BridgeStyles.StatusRed;
            EditorGUI.DrawRect(new Rect(warningRect.x, warningRect.y, 4, warningRect.height), accentColor);

            // Warning icon
            GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                normal = { textColor = accentColor }
            };
            Rect iconRect = new Rect(warningRect.x + 14, warningRect.y + 12, 30, 30);
            EditorGUI.LabelField(iconRect, "\u26A0", iconStyle);

            // Title and description
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.white }
            };

            float textX = warningRect.x + 44;
            float textWidth = warningRect.width - 180;

            if (templateInstalled)
            {
                Rect titleRect = new Rect(textX, warningRect.y + 10, textWidth, 20);
                EditorGUI.LabelField(titleRect, "WebGL Template Not Selected", titleStyle);

                Rect descRect = new Rect(textX, warningRect.y + 30, textWidth, 20);
                EditorGUI.LabelField(descRect, "The Bridge template is installed but not selected in Player Settings.", _menuDescStyle);
            }
            else
            {
                Rect titleRect = new Rect(textX, warningRect.y + 10, textWidth, 20);
                EditorGUI.LabelField(titleRect, "Bridge Template Not Installed", titleStyle);

                Rect descRect = new Rect(textX, warningRect.y + 30, textWidth, 36);
                GUIStyle wrapStyle = new GUIStyle(_menuDescStyle) { wordWrap = true };
                EditorGUI.LabelField(descRect, "Install the Bridge WebGL template to enable cross-platform publishing features.", wrapStyle);
            }

            // Action button
            Rect buttonRect = new Rect(warningRect.x + warningRect.width - 120, warningRect.y + (warningRect.height - 28) / 2, 105, 28);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = templateInstalled ? BridgeStyles.StatusYellow : BridgeStyles.BrandPurple;

            string buttonText = templateInstalled ? "Fix Now" : "Install";
            if (GUI.Button(buttonRect, buttonText))
            {
                if (templateInstalled)
                {
                    // Set the template
                    PlayerSettings.WebGL.template = "PROJECT:Bridge";
                    Debug.Log("[Playgama Bridge] WebGL template set to 'Bridge'");
                }
                else
                {
                    // Open install files window
                    InstallFilesWindow.Show();
                }
            }

            GUI.backgroundColor = oldBg;

            GUILayout.Space(12);
        }

        private void DrawStatusDashboard()
        {
            Rect cardRect = EditorGUILayout.GetControlRect(false, 70);
            EditorGUI.DrawRect(cardRect, new Color(0.18f, 0.18f, 0.21f));

            // Left border accent
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 3, cardRect.height), BridgeStyles.BrandPurple);

            float columnWidth = (cardRect.width - 40) / 4f;
            float startX = cardRect.x + 20;
            float centerY = cardRect.y + cardRect.height / 2f;

            // Build Size
            DrawStatusColumn(startX, centerY, columnWidth,
                _buildInfo != null && _buildInfo.HasData ? SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes) : "No build",
                "Last Build");

            // Warnings
            string warningText = _buildInfo != null && _buildInfo.HasData ? _totalWarnings.ToString() : "-";
            Color warningColor = _totalWarnings > 0 ? BridgeStyles.StatusYellow : BridgeStyles.StatusGreen;
            DrawStatusColumn(startX + columnWidth, centerY, columnWidth, warningText, "Warnings", warningColor);

            // Suggestions
            string suggestionText = _buildInfo != null && _buildInfo.HasData ? _totalSuggestions.ToString() : "-";
            DrawStatusColumn(startX + columnWidth * 2, centerY, columnWidth, suggestionText, "Suggestions");

            // Assets Tracked
            string assetsText = _buildInfo != null && _buildInfo.HasData ? _buildInfo.TrackedAssetCount.ToString() : "-";
            DrawStatusColumn(startX + columnWidth * 3, centerY, columnWidth, assetsText, "Assets");
        }

        private void DrawStatusColumn(float x, float centerY, float width, string value, string label, Color? valueColor = null)
        {
            Rect valueRect = new Rect(x, centerY - 22, width - 10, 24);
            Rect labelRect = new Rect(x, centerY + 2, width - 10, 18);

            GUIStyle valueStyle = new GUIStyle(_statusValueStyle);
            if (valueColor.HasValue)
                valueStyle.normal.textColor = valueColor.Value;

            EditorGUI.LabelField(valueRect, value, valueStyle);
            EditorGUI.LabelField(labelRect, label, _statusLabelStyle);
        }

        private void DrawBuildBreakdown()
        {
            if (_buildInfo == null || !_buildInfo.HasData)
                return;

            long totalTracked = _textureBytes + _audioBytes + _meshBytes + _shaderBytes + _fontBytes + _otherBytes;
            if (totalTracked <= 0)
                return;

            BridgeStyles.DrawSectionTitle("Build Breakdown", "\u25A0");

            GUILayout.Space(4);

            // Main card
            Rect cardRect = EditorGUILayout.GetControlRect(false, 200);
            EditorGUI.DrawRect(cardRect, new Color(0.18f, 0.18f, 0.21f));

            float padding = 16f;

            // Donut chart on the left
            float chartSize = 140;
            float chartX = cardRect.x + padding + 20;
            float chartY = cardRect.y + (cardRect.height - chartSize) / 2f;
            Vector2 center = new Vector2(chartX + chartSize / 2f, chartY + chartSize / 2f);
            float outerRadius = chartSize / 2f;
            float innerRadius = outerRadius * 0.55f;

            // Build segments data
            var segments = new List<(long bytes, Color color, string label, int count)>
            {
                (_textureBytes, ChartTextures, "Textures", _textureCount),
                (_audioBytes, ChartAudio, "Audio", _audioCount),
                (_meshBytes, ChartMeshes, "Meshes", _meshCount),
                (_shaderBytes, ChartShaders, "Shaders", _shaderCount),
                (_fontBytes, ChartFonts, "Fonts", _fontCount),
                (_otherBytes, ChartOther, "Other", _otherCount)
            };

            // Draw donut chart
            float startAngle = -90f; // Start from top
            foreach (var seg in segments)
            {
                if (seg.bytes <= 0) continue;
                float sweepAngle = (float)seg.bytes / totalTracked * 360f;
                DrawDonutSegment(center, innerRadius, outerRadius, startAngle, sweepAngle, seg.color);
                startAngle += sweepAngle;
            }

            // Center text - total size
            GUIStyle centerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = Color.white }
            };
            Rect centerTextRect = new Rect(center.x - 40, center.y - 20, 80, 20);
            EditorGUI.LabelField(centerTextRect, SharedTypes.FormatBytes(totalTracked), centerStyle);

            GUIStyle centerSubStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.65f) }
            };
            Rect centerSubRect = new Rect(center.x - 40, center.y + 2, 80, 16);
            EditorGUI.LabelField(centerSubRect, "Total", centerSubStyle);

            // Legend on the right
            float legendX = chartX + chartSize + 30;
            float legendWidth = cardRect.width - (legendX - cardRect.x) - padding;
            float legendY = cardRect.y + 20;
            float rowHeight = 26;

            foreach (var seg in segments)
            {
                if (seg.bytes <= 0) continue;
                DrawLegendItem(legendX, legendY, legendWidth, seg.label, seg.bytes, seg.count, totalTracked, seg.color);
                legendY += rowHeight;
            }
        }

        private void DrawDonutSegment(Vector2 center, float innerRadius, float outerRadius, float startAngle, float sweepAngle, Color color)
        {
            if (sweepAngle <= 0) return;

            // Draw using small rectangles to approximate the arc
            int segments = Mathf.Max(8, Mathf.CeilToInt(sweepAngle / 3f));
            float angleStep = sweepAngle / segments;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = (startAngle + i * angleStep) * Mathf.Deg2Rad;
                float angle2 = (startAngle + (i + 1) * angleStep) * Mathf.Deg2Rad;

                // Calculate the 4 corners of this segment
                Vector2 outerP1 = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * outerRadius;
                Vector2 outerP2 = center + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * outerRadius;
                Vector2 innerP1 = center + new Vector2(Mathf.Cos(angle1), Mathf.Sin(angle1)) * innerRadius;
                Vector2 innerP2 = center + new Vector2(Mathf.Cos(angle2), Mathf.Sin(angle2)) * innerRadius;

                // Draw as two triangles using GUI
                DrawTriangle(outerP1, outerP2, innerP1, color);
                DrawTriangle(innerP1, outerP2, innerP2, color);
            }
        }

        private void DrawTriangle(Vector2 p1, Vector2 p2, Vector2 p3, Color color)
        {
            // Approximate triangle with small rectangles along the edges
            // For better performance, we draw filled quads
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawAAConvexPolygon(new Vector3(p1.x, p1.y, 0), new Vector3(p2.x, p2.y, 0), new Vector3(p3.x, p3.y, 0));
            Handles.EndGUI();
        }

        private void DrawLegendItem(float x, float y, float width, string label, long bytes, int count, long total, Color color)
        {
            // Color box
            Rect colorRect = new Rect(x, y + 4, 14, 14);
            EditorGUI.DrawRect(colorRect, color);

            // Label
            Rect labelRect = new Rect(x + 20, y + 2, 70, 18);
            EditorGUI.LabelField(labelRect, label, EditorStyles.miniLabel);

            // Size and percentage
            float pct = total > 0 ? (float)bytes / total * 100f : 0f;
            string sizeText = SharedTypes.FormatBytes(bytes);

            Rect sizeRect = new Rect(x + 85, y + 2, 55, 18);
            EditorGUI.LabelField(sizeRect, sizeText, EditorStyles.miniLabel);

            // Percentage
            GUIStyle pctStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.5f, 0.5f, 0.55f) }
            };
            Rect pctRect = new Rect(x + 140, y + 2, 50, 18);
            EditorGUI.LabelField(pctRect, $"{pct:0.0}%", pctStyle);

            // Count
            GUIStyle countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.45f, 0.45f, 0.5f) }
            };
            Rect countRect = new Rect(x + 185, y + 2, 40, 18);
            EditorGUI.LabelField(countRect, $"({count})", countStyle);
        }

        private void DrawGetStarted()
        {
            BridgeStyles.DrawSectionTitle("Get Started", "\u26A1");

            GUILayout.Space(8);

            // Wizard Card with enhanced description
            Rect wizardRect = EditorGUILayout.GetControlRect(false, 85);
            bool isHover = wizardRect.Contains(Event.current.mousePosition);

            Color bgColor = isHover ? new Color(0.35f, 0.28f, 0.45f) : new Color(0.25f, 0.2f, 0.32f);
            EditorGUI.DrawRect(wizardRect, bgColor);

            // Purple accent on left
            EditorGUI.DrawRect(new Rect(wizardRect.x, wizardRect.y, 4, wizardRect.height), BridgeStyles.BrandPurple);

            if (isHover)
                EditorGUIUtility.AddCursorRect(wizardRect, MouseCursor.Link);

            // Icon and title
            Rect iconRect = new Rect(wizardRect.x + 16, wizardRect.y + 16, 24, 24);
            EditorGUI.LabelField(iconRect, "\u2728", EditorStyles.boldLabel);

            Rect titleRect = new Rect(wizardRect.x + 46, wizardRect.y + 14, wizardRect.width - 160, 22);
            EditorGUI.LabelField(titleRect, "Start Optimization Wizard", EditorStyles.boldLabel);

            // Description
            Rect descRect = new Rect(wizardRect.x + 46, wizardRect.y + 36, wizardRect.width - 160, 36);
            EditorGUI.LabelField(descRect, "Step-by-step guide through Build, Textures, Audio, Meshes, and Settings optimization.", _menuDescStyle);

            // Steps badge
            Rect stepsRect = new Rect(wizardRect.x + wizardRect.width - 100, wizardRect.y + 30, 80, 24);
            EditorGUI.DrawRect(stepsRect, new Color(0.4f, 0.3f, 0.5f));
            GUIStyle stepsStyle = new GUIStyle(EditorStyles.miniLabel);
            stepsStyle.alignment = TextAnchor.MiddleCenter;
            stepsStyle.normal.textColor = Color.white;
            EditorGUI.LabelField(stepsRect, "6 Steps", stepsStyle);

            if (Event.current.type == EventType.MouseDown && wizardRect.Contains(Event.current.mousePosition))
            {
                OptimizationWizard.ShowWizard();
                Event.current.Use();
            }

            GUILayout.Space(12);

            // Quick action buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(145)))
                {
                    if (DrawQuickActionButton("\u25B6", "Install Files", "Set up WebGL templates"))
                    {
                        InstallFilesWindow.Show();
                    }
                }

                GUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(145)))
                {
                    if (DrawQuickActionButton("\u26A1", "Build & Analyze", "Build and analyze sizes"))
                    {
                        EditorApplication.delayCall += () => BuildAnalyzer.BuildAndAnalyze();
                    }
                }

                GUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(145)))
                {
                    if (DrawQuickActionButton("\u2699", "Settings", "Configure Bridge"))
                    {
                        OpenTab(BridgeWindow.TabSettings);
                    }
                }

                GUILayout.Space(8);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(145)))
                {
                    bool hasData = _buildInfo != null && _buildInfo.HasData;
                    if (DrawQuickActionButton("\u2398", "Export Report", hasData ? "Copy to clipboard" : "Build first", !hasData))
                    {
                        if (hasData)
                        {
                            string report = GenerateReport();
                            EditorGUIUtility.systemCopyBuffer = report;
                            Debug.Log("[Bridge] Report copied to clipboard");
                        }
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private bool DrawQuickActionButton(string icon, string title, string desc, bool disabled = false)
        {
            Rect btnRect = EditorGUILayout.GetControlRect(false, 60);

            bool isHover = !disabled && btnRect.Contains(Event.current.mousePosition);
            Color bgColor = disabled ? new Color(0.18f, 0.18f, 0.2f) : (isHover ? new Color(0.28f, 0.28f, 0.32f) : new Color(0.22f, 0.22f, 0.25f));
            EditorGUI.DrawRect(btnRect, bgColor);

            Rect accentRect = new Rect(btnRect.x, btnRect.y, 3, btnRect.height);
            EditorGUI.DrawRect(accentRect, disabled ? new Color(0.3f, 0.3f, 0.35f) : BridgeStyles.BrandPurple);

            if (isHover)
                EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Link);

            Color oldColor = GUI.color;
            if (disabled) GUI.color = new Color(0.5f, 0.5f, 0.5f);

            Rect iconRect = new Rect(btnRect.x + 10, btnRect.y + 10, 20, 20);
            EditorGUI.LabelField(iconRect, icon, EditorStyles.boldLabel);

            Rect titleRect = new Rect(btnRect.x + 10, btnRect.y + 28, btnRect.width - 16, 18);
            EditorGUI.LabelField(titleRect, title, EditorStyles.miniLabel);

            Rect descRect = new Rect(btnRect.x + 10, btnRect.y + 42, btnRect.width - 16, 14);
            EditorGUI.LabelField(descRect, desc, _menuDescStyle);

            GUI.color = oldColor;

            if (!disabled && Event.current.type == EventType.MouseDown && btnRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private void DrawFeatures()
        {
            BridgeStyles.DrawSectionTitle("Optimization Tools", "\u2699");

            GUILayout.Space(8);

            BridgeStyles.BeginCard();

            DrawFeatureRow("\u25B6", "Textures", "Optimize texture compression, max sizes, and crunch settings", BridgeWindow.TabTextures, _textureIssues);
            DrawFeatureRow("\u266A", "Audio", "Configure audio compression and load settings for WebGL", BridgeWindow.TabAudio, _audioIssues);
            DrawFeatureRow("\u25B2", "Meshes", "Manage mesh compression and read/write settings", BridgeWindow.TabMeshes, _meshIssues);
            DrawFeatureRow("\u2726", "Shaders", "View shader sizes and pass counts from build report", BridgeWindow.TabShaders, _shaderIssues);
            DrawFeatureRow("\u0041", "Fonts", "View font sizes including TextMeshPro assets", BridgeWindow.TabFonts, _fontIssues);
            DrawFeatureRow("\u2699", "Build Settings", "Control scenes, WebGL compression, and build options", BridgeWindow.TabBuildSettings, _buildSettingsIssues);

            BridgeStyles.EndCard();
        }

        private void DrawFeatureRow(string icon, string title, string description, int tabIndex, int issueCount)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, 36);

            if (rowRect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));
                EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
            }

            Rect iconRect = new Rect(rowRect.x + 10, rowRect.y + 8, 24, 20);
            EditorGUI.LabelField(iconRect, icon, EditorStyles.boldLabel);

            Rect titleRect = new Rect(rowRect.x + 40, rowRect.y + 4, 120, 18);
            EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);

            // Status indicator
            Rect statusRect = new Rect(rowRect.x + 160, rowRect.y + 6, 70, 16);
            if (_buildInfo != null && _buildInfo.HasData)
            {
                if (issueCount > 0)
                {
                    EditorGUI.DrawRect(statusRect, new Color(0.8f, 0.6f, 0.2f, 0.3f));
                    GUIStyle warningStyle = new GUIStyle(EditorStyles.miniLabel);
                    warningStyle.normal.textColor = BridgeStyles.StatusYellow;
                    warningStyle.alignment = TextAnchor.MiddleCenter;
                    EditorGUI.LabelField(statusRect, $"\u26A0 {issueCount} issues", warningStyle);
                }
                else
                {
                    GUIStyle okStyle = new GUIStyle(EditorStyles.miniLabel);
                    okStyle.normal.textColor = BridgeStyles.StatusGreen;
                    okStyle.alignment = TextAnchor.MiddleCenter;
                    EditorGUI.LabelField(statusRect, "\u2714 OK", okStyle);
                }
            }

            Rect descRect = new Rect(rowRect.x + 40, rowRect.y + 20, rowRect.width - 100, 14);
            EditorGUI.LabelField(descRect, description, _menuDescStyle);

            Rect arrowRect = new Rect(rowRect.x + rowRect.width - 30, rowRect.y + 10, 20, 16);
            EditorGUI.LabelField(arrowRect, "\u25B8", EditorStyles.miniLabel);

            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                OpenTab(tabIndex);
                Event.current.Use();
            }
        }

        private void DrawResources()
        {
            BridgeStyles.DrawSectionTitle("Resources", "\u2139");

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);

                if (DrawResourceButton("\u2139", "Documentation", "Read the full documentation"))
                {
                    Application.OpenURL(DocsUrl);
                }

                GUILayout.Space(10);

                if (DrawResourceButton("\u2B22", "Discord", "Join our Discord community"))
                {
                    Application.OpenURL(SupportUrl);
                }

                GUILayout.Space(10);

                if (DrawResourceButton("\u2302", "Developer Portal", "Publish your game"))
                {
                    Application.OpenURL("https://developer.playgama.com/");
                }

                GUILayout.FlexibleSpace();
            }
        }

        private bool DrawResourceButton(string icon, string title, string tooltip)
        {
            Rect btnRect = EditorGUILayout.GetControlRect(false, 50, GUILayout.Width(170));

            bool isHover = btnRect.Contains(Event.current.mousePosition);

            Color bgColor = isHover ? new Color(0.3f, 0.25f, 0.38f) : new Color(0.22f, 0.22f, 0.26f);
            EditorGUI.DrawRect(btnRect, bgColor);

            // Border on hover
            if (isHover)
            {
                EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, btnRect.width, 2), BridgeStyles.BrandPurple);
                EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y + btnRect.height - 2, btnRect.width, 2), BridgeStyles.BrandPurple);
                EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Link);
            }

            // Icon
            Rect iconRect = new Rect(btnRect.x + 12, btnRect.y + 10, 24, 24);
            GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel);
            iconStyle.fontSize = 16;
            iconStyle.normal.textColor = BridgeStyles.BrandPurple;
            EditorGUI.LabelField(iconRect, icon, iconStyle);

            // Title
            Rect titleRect = new Rect(btnRect.x + 42, btnRect.y + 10, btnRect.width - 50, 18);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.label);
            titleStyle.normal.textColor = isHover ? Color.white : new Color(0.85f, 0.85f, 0.9f);
            EditorGUI.LabelField(titleRect, title, titleStyle);

            // Tooltip as subtitle
            Rect tooltipRect = new Rect(btnRect.x + 42, btnRect.y + 28, btnRect.width - 50, 14);
            EditorGUI.LabelField(tooltipRect, tooltip, _menuDescStyle);

            if (Event.current.type == EventType.MouseDown && btnRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private void DrawFooter()
        {
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Made with \u2665 by Playgama", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(10);
        }

        private void InstallFiles()
        {
            InstallFilesWindow.Show();
        }

        private void OpenTab(int index)
        {
            var window = EditorWindow.GetWindow<BridgeWindow>();
            if (window != null)
            {
                window.SetSelectedTab(index);
            }
        }

        private string GenerateReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Playgama Bridge Build Report");
            sb.AppendLine("============================");
            sb.AppendLine();

            if (_buildInfo != null && _buildInfo.HasData)
            {
                sb.AppendLine($"Total Build Size: {SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes)}");
                sb.AppendLine($"Analysis Mode: {_buildInfo.DataMode}");
                sb.AppendLine($"Tracked Assets: {_buildInfo.TrackedAssetCount}");
                sb.AppendLine($"Build Time: {_buildInfo.BuildTime.TotalSeconds:F1}s");
                sb.AppendLine();
                sb.AppendLine("Issues Detected:");
                sb.AppendLine($"  Textures: {_textureIssues}");
                sb.AppendLine($"  Audio: {_audioIssues}");
                sb.AppendLine($"  Meshes: {_meshIssues}");
                sb.AppendLine($"  Shaders: {_shaderIssues}");
                sb.AppendLine($"  Fonts: {_fontIssues}");
                sb.AppendLine();
                sb.AppendLine("Top 5 Largest Assets:");

                if (_buildInfo.Assets != null)
                {
                    var top5 = _buildInfo.Assets.OrderByDescending(a => a.SizeBytes).Take(5);
                    foreach (var asset in top5)
                    {
                        if (asset != null)
                            sb.AppendLine($"  {SharedTypes.FormatBytes(asset.SizeBytes)} - {Path.GetFileName(asset.Path)}");
                    }
                }
            }
            else
            {
                sb.AppendLine("No build data available. Run Build & Analyze first.");
            }

            sb.AppendLine();
            sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return sb.ToString();
        }

        private void EnsureStyles()
        {
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(EditorStyles.boldLabel);
                _titleStyle.fontSize = 28;
                _titleStyle.normal.textColor = Color.white;
                _titleStyle.fontStyle = FontStyle.Bold;
            }

            if (_subtitleStyle == null)
            {
                _subtitleStyle = new GUIStyle(EditorStyles.label);
                _subtitleStyle.fontSize = 14;
                _subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.75f);
            }

            if (_versionStyle == null)
            {
                _versionStyle = new GUIStyle(EditorStyles.miniLabel);
                _versionStyle.alignment = TextAnchor.MiddleCenter;
                _versionStyle.normal.textColor = Color.white;
                _versionStyle.fontStyle = FontStyle.Bold;
            }

            if (_menuDescStyle == null)
            {
                _menuDescStyle = new GUIStyle(EditorStyles.miniLabel);
                _menuDescStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
                _menuDescStyle.wordWrap = true;
            }

            if (_statusValueStyle == null)
            {
                _statusValueStyle = new GUIStyle(EditorStyles.boldLabel);
                _statusValueStyle.fontSize = 18;
                _statusValueStyle.alignment = TextAnchor.MiddleCenter;
                _statusValueStyle.normal.textColor = Color.white;
            }

            if (_statusLabelStyle == null)
            {
                _statusLabelStyle = new GUIStyle(EditorStyles.miniLabel);
                _statusLabelStyle.alignment = TextAnchor.MiddleCenter;
                _statusLabelStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
            }
        }

        private static string GetPackageVersion()
        {
            if (_versionLoaded)
                return _cachedVersion ?? "1.0.0";

            _versionLoaded = true;
            _cachedVersion = "1.0.0";

            string[] possiblePaths = new[]
            {
                "Packages/com.playgama.bridge/package.json",
                Path.Combine(Application.dataPath, "../Packages/com.playgama.bridge/package.json")
            };

            foreach (var path in possiblePaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    if (File.Exists(fullPath))
                    {
                        string json = File.ReadAllText(fullPath);
                        int versionIndex = json.IndexOf("\"version\"");
                        if (versionIndex >= 0)
                        {
                            int colonIndex = json.IndexOf(":", versionIndex);
                            int startQuote = json.IndexOf("\"", colonIndex + 1);
                            int endQuote = json.IndexOf("\"", startQuote + 1);
                            if (startQuote >= 0 && endQuote > startQuote)
                            {
                                _cachedVersion = json.Substring(startQuote + 1, endQuote - startQuote - 1);
                                return _cachedVersion;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore errors, try next path
                }
            }

            return _cachedVersion;
        }
    }
}
