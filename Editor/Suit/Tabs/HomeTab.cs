using System;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit.Tabs
{
    /// <summary>
    /// Home tab for Playgama Suite - the main landing page with branding and quick actions.
    /// </summary>
    public sealed class HomeTab : ITab
    {
        public string TabName => "Home";

        private BuildInfo _buildInfo;
        private Vector2 _scroll;

        // Cached styles
        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _versionStyle;
        private static GUIStyle _menuButtonStyle;
        private static GUIStyle _menuDescStyle;
        private static Texture2D _headerBgTexture;

        private const string Version = "1.0.0";
        private const string DocsUrl = "https://wiki.playgama.com/playgama/sdk/engines/unity/intro";
        private const string SupportUrl = "https://discord.gg/zMb3TmuvvG";

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
        }

        public void OnGUI()
        {
            EnsureStyles();

            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawHeader();
                GUILayout.Space(20);
                DrawQuickActions();
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
            // Header background
            Rect headerRect = EditorGUILayout.GetControlRect(false, 120);

            // Gradient-like background
            EditorGUI.DrawRect(headerRect, new Color(0.15f, 0.15f, 0.18f));

            // Purple accent line at top
            Rect accentRect = new Rect(headerRect.x, headerRect.y, headerRect.width, 4);
            EditorGUI.DrawRect(accentRect, SuitStyles.BrandPurple);

            // Title
            Rect titleRect = new Rect(headerRect.x + 20, headerRect.y + 25, headerRect.width - 40, 40);
            EditorGUI.LabelField(titleRect, "Playgama Bridge", _titleStyle);

            // Subtitle
            Rect subRect = new Rect(headerRect.x + 20, headerRect.y + 65, headerRect.width - 40, 24);
            EditorGUI.LabelField(subRect, "Cross-Platform Game Publishing SDK", _subtitleStyle);

            // Version badge
            Rect versionRect = new Rect(headerRect.x + headerRect.width - 80, headerRect.y + 30, 60, 20);
            EditorGUI.DrawRect(versionRect, SuitStyles.BrandPurple);
            EditorGUI.LabelField(versionRect, "v" + Version, _versionStyle);
        }

        private void DrawQuickActions()
        {
            SuitStyles.DrawSectionTitle("Quick Actions", "\u26A1");

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);

                // Install Files button
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(180)))
                {
                    if (DrawMenuButton("Install Files", "Set up WebGL templates and required files for your project"))
                    {
                        InstallFiles();
                    }
                }

                GUILayout.Space(10);

                // Build & Analyze button
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(180)))
                {
                    if (DrawMenuButton("Build & Analyze", "Build WebGL and analyze asset sizes"))
                    {
                        EditorApplication.delayCall += () => BuildAnalyzer.BuildAndAnalyze();
                    }
                }

                GUILayout.Space(10);

                // Open Settings button
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(180)))
                {
                    if (DrawMenuButton("Settings", "Configure Playgama Suite settings"))
                    {
                        // Settings tab is index 7 (after Home)
                        OpenTab(7);
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawFeatures()
        {
            SuitStyles.DrawSectionTitle("Optimization Tools", "\u2699");

            GUILayout.Space(8);

            SuitStyles.BeginCard();

            DrawFeatureRow("\u25B6", "Textures", "Optimize texture compression, max sizes, and crunch settings", 2);
            DrawFeatureRow("\u266A", "Audio", "Configure audio compression and load settings for WebGL", 3);
            DrawFeatureRow("\u25B2", "Meshes", "Manage mesh compression and read/write settings", 4);
            DrawFeatureRow("\u2699", "Build Settings", "Control scenes, WebGL compression, and build options", 5);
            DrawFeatureRow("\u2714", "Platform Checks", "Validate build size against platform requirements", 6);

            SuitStyles.EndCard();
        }

        private void DrawFeatureRow(string icon, string title, string description, int tabIndex)
        {
            Rect rowRect = EditorGUILayout.GetControlRect(false, 36);

            // Hover effect
            if (rowRect.Contains(Event.current.mousePosition))
            {
                EditorGUI.DrawRect(rowRect, new Color(1f, 1f, 1f, 0.05f));
                EditorGUIUtility.AddCursorRect(rowRect, MouseCursor.Link);
            }

            // Icon
            Rect iconRect = new Rect(rowRect.x + 10, rowRect.y + 8, 24, 20);
            EditorGUI.LabelField(iconRect, icon, EditorStyles.boldLabel);

            // Title
            Rect titleRect = new Rect(rowRect.x + 40, rowRect.y + 4, 120, 18);
            EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);

            // Description
            Rect descRect = new Rect(rowRect.x + 40, rowRect.y + 20, rowRect.width - 100, 14);
            EditorGUI.LabelField(descRect, description, _menuDescStyle);

            // Arrow
            Rect arrowRect = new Rect(rowRect.x + rowRect.width - 30, rowRect.y + 10, 20, 16);
            EditorGUI.LabelField(arrowRect, "\u25B8", EditorStyles.miniLabel);

            // Click handler
            if (Event.current.type == EventType.MouseDown && rowRect.Contains(Event.current.mousePosition))
            {
                OpenTab(tabIndex);
                Event.current.Use();
            }
        }

        private void DrawResources()
        {
            SuitStyles.DrawSectionTitle("Resources", "\u2139");

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);

                if (DrawLinkButton("Documentation", "Read the full documentation"))
                {
                    Application.OpenURL(DocsUrl);
                }

                GUILayout.Space(10);

                if (DrawLinkButton("Discord Support", "Join our Discord community"))
                {
                    Application.OpenURL(SupportUrl);
                }

                GUILayout.Space(10);

                if (DrawLinkButton("Developer Portal", "Access Playgama Developer Portal"))
                {
                    Application.OpenURL("https://developer.playgama.com/");
                }

                GUILayout.FlexibleSpace();
            }
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

        private bool DrawMenuButton(string title, string tooltip)
        {
            Rect btnRect = EditorGUILayout.GetControlRect(false, 70);

            // Background
            Color bgColor = new Color(0.22f, 0.22f, 0.25f);
            bool isHover = btnRect.Contains(Event.current.mousePosition);
            if (isHover)
            {
                bgColor = new Color(0.28f, 0.28f, 0.32f);
                EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Link);
            }
            EditorGUI.DrawRect(btnRect, bgColor);

            // Purple left accent
            Rect accentRect = new Rect(btnRect.x, btnRect.y, 4, btnRect.height);
            EditorGUI.DrawRect(accentRect, SuitStyles.BrandPurple);

            // Title
            Rect titleRect = new Rect(btnRect.x + 14, btnRect.y + 12, btnRect.width - 20, 22);
            EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);

            // Description
            Rect descRect = new Rect(btnRect.x + 14, btnRect.y + 34, btnRect.width - 20, 30);
            EditorGUI.LabelField(descRect, tooltip, _menuDescStyle);

            // Handle click
            if (Event.current.type == EventType.MouseDown && btnRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private bool DrawLinkButton(string title, string tooltip)
        {
            Rect btnRect = EditorGUILayout.GetControlRect(false, 36, GUILayout.Width(160));

            bool isHover = btnRect.Contains(Event.current.mousePosition);

            // Background with hover effect
            Color bgColor = isHover
                ? new Color(0.35f, 0.25f, 0.45f) // Purple tint on hover
                : new Color(0.2f, 0.2f, 0.23f);
            EditorGUI.DrawRect(btnRect, bgColor);

            // Border on hover
            if (isHover)
            {
                // Top border
                EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, btnRect.width, 1), SuitStyles.BrandPurple);
                // Bottom border
                EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y + btnRect.height - 1, btnRect.width, 1), SuitStyles.BrandPurple);
                // Left border
                EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, 1, btnRect.height), SuitStyles.BrandPurple);
                // Right border
                EditorGUI.DrawRect(new Rect(btnRect.x + btnRect.width - 1, btnRect.y, 1, btnRect.height), SuitStyles.BrandPurple);

                EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Link);
            }

            // Center text
            GUIStyle centeredStyle = new GUIStyle(EditorStyles.miniLabel);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.fontSize = 11;
            centeredStyle.normal.textColor = isHover ? Color.white : SuitStyles.BrandPurple;

            EditorGUI.LabelField(btnRect, new GUIContent(title, tooltip), centeredStyle);

            // Handle click
            if (Event.current.type == EventType.MouseDown && btnRect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }

            return false;
        }

        private void InstallFiles()
        {
            InstallFilesWindow.Show();
        }

        private void OpenTab(int index)
        {
            // Find the SuitWindow and change tab
            var window = EditorWindow.GetWindow<SuitWindow>();
            if (window != null)
            {
                var field = typeof(SuitWindow).GetField("_selectedTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(window, index);
                    EditorPrefs.SetInt("SUIT_SELECTED_TAB", index);
                    window.Repaint();
                }
            }
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
        }
    }
}
