using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
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
        private static Texture2D _headerBgTexture;

        private static string _cachedVersion;
        private static bool _versionLoaded;
        private const string DocsUrl = "https://wiki.playgama.com/playgama/sdk/engines/unity/intro";
        private const string SupportUrl = "https://discord.gg/TZg3rF3sdT";

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

        private void DrawQuickActions()
        {
            BridgeStyles.DrawSectionTitle("Quick Actions", "\u26A1");

            GUILayout.Space(8);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);

                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = BridgeStyles.BrandPurple;

                if (GUILayout.Button(new GUIContent("  Start Optimization Wizard  ", "Step-by-step guide to optimize your WebGL build"), GUILayout.Height(35)))
                {
                    OptimizationWizard.ShowWizard();
                }

                GUI.backgroundColor = oldBg;

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(5);
            EditorGUILayout.LabelField("New to optimization? The wizard will guide you through each step.", BridgeStyles.SubtitleStyle);

            GUILayout.Space(15);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(180)))
                {
                    if (DrawMenuButton("Install Files", "Set up WebGL templates and required files for your project"))
                    {
                        InstallFiles();
                    }
                }

                GUILayout.Space(10);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(180)))
                {
                    if (DrawMenuButton("Build & Analyze", "Build WebGL and analyze asset sizes"))
                    {
                        EditorApplication.delayCall += () => BuildAnalyzer.BuildAndAnalyze();
                    }
                }

                GUILayout.Space(10);

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(180)))
                {
                    if (DrawMenuButton("Settings", "Configure Playgama Bridge settings"))
                    {
                        OpenTab(9);
                    }
                }

                GUILayout.FlexibleSpace();
            }
        }

        private void DrawFeatures()
        {
            BridgeStyles.DrawSectionTitle("Optimization Tools", "\u2699");

            GUILayout.Space(8);

            BridgeStyles.BeginCard();

            DrawFeatureRow("\u25B6", "Textures", "Optimize texture compression, max sizes, and crunch settings", 2);
            DrawFeatureRow("\u266A", "Audio", "Configure audio compression and load settings for WebGL", 3);
            DrawFeatureRow("\u25B2", "Meshes", "Manage mesh compression and read/write settings", 4);
            DrawFeatureRow("\u2726", "Shaders", "View shader sizes and pass counts from build report", 5);
            DrawFeatureRow("\u0041", "Fonts", "View font sizes including TextMeshPro assets", 6);
            DrawFeatureRow("\u2699", "Build Settings", "Control scenes, WebGL compression, and build options", 7);
            DrawFeatureRow("\u2714", "Platform Checks", "Validate build size against platform requirements", 8);

            BridgeStyles.EndCard();
        }

        private void DrawFeatureRow(string icon, string title, string description, int tabIndex)
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

            Color bgColor = new Color(0.22f, 0.22f, 0.25f);
            bool isHover = btnRect.Contains(Event.current.mousePosition);
            if (isHover)
            {
                bgColor = new Color(0.28f, 0.28f, 0.32f);
                EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Link);
            }
            EditorGUI.DrawRect(btnRect, bgColor);

            Rect accentRect = new Rect(btnRect.x, btnRect.y, 4, btnRect.height);
            EditorGUI.DrawRect(accentRect, BridgeStyles.BrandPurple);

            Rect titleRect = new Rect(btnRect.x + 14, btnRect.y + 12, btnRect.width - 20, 22);
            EditorGUI.LabelField(titleRect, title, EditorStyles.boldLabel);

            Rect descRect = new Rect(btnRect.x + 14, btnRect.y + 34, btnRect.width - 20, 30);
            EditorGUI.LabelField(descRect, tooltip, _menuDescStyle);

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

            Color bgColor = isHover
                ? new Color(0.35f, 0.25f, 0.45f)
                : new Color(0.2f, 0.2f, 0.23f);
            EditorGUI.DrawRect(btnRect, bgColor);

            if (isHover)
            {
                EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, btnRect.width, 1), BridgeStyles.BrandPurple);
                EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y + btnRect.height - 1, btnRect.width, 1), BridgeStyles.BrandPurple);
                EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, 1, btnRect.height), BridgeStyles.BrandPurple);
                EditorGUI.DrawRect(new Rect(btnRect.x + btnRect.width - 1, btnRect.y, 1, btnRect.height), BridgeStyles.BrandPurple);

                EditorGUIUtility.AddCursorRect(btnRect, MouseCursor.Link);
            }

            GUIStyle centeredStyle = new GUIStyle(EditorStyles.miniLabel);
            centeredStyle.alignment = TextAnchor.MiddleCenter;
            centeredStyle.fontSize = 11;
            centeredStyle.normal.textColor = isHover ? Color.white : BridgeStyles.BrandPurple;

            EditorGUI.LabelField(btnRect, new GUIContent(title, tooltip), centeredStyle);

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
            var window = EditorWindow.GetWindow<BridgeWindow>();
            if (window != null)
            {
                var field = typeof(BridgeWindow).GetField("_selectedTab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(window, index);
                    EditorPrefs.SetInt("BRIDGE_SELECTED_TAB", index);
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
