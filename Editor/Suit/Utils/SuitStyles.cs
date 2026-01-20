using UnityEngine;
using UnityEditor;

namespace Playgama.Suit
{
    /// <summary>
    /// Shared UI styles, colors, and helper methods for Playgama Suit tabs.
    /// Provides Material Design-inspired visuals with Playgama branding.
    /// </summary>
    public static class SuitStyles
    {
        // Brand Colors
        /// <summary>Playgama brand purple (#9748ff)</summary>
        public static readonly Color BrandPurple = new Color(0.592f, 0.282f, 1f);

        /// <summary>Lighter variant of brand purple for backgrounds</summary>
        public static readonly Color BrandPurpleLight = new Color(0.592f, 0.282f, 1f, 0.15f);

        /// <summary>Darker variant of brand purple for hover/active states</summary>
        public static readonly Color BrandPurpleDark = new Color(0.45f, 0.2f, 0.8f);

        // Status Colors (Dark theme friendly)
        /// <summary>Success/Pass green</summary>
        public static readonly Color StatusGreen = new Color(0.2f, 0.5f, 0.3f);

        /// <summary>Warning yellow/amber</summary>
        public static readonly Color StatusYellow = new Color(0.55f, 0.45f, 0.2f);

        /// <summary>Error/Critical red</summary>
        public static readonly Color StatusRed = new Color(0.55f, 0.25f, 0.25f);

        /// <summary>Neutral/Default gray</summary>
        public static readonly Color StatusGray = new Color(0.28f, 0.28f, 0.28f);

        /// <summary>Card/Panel background</summary>
        public static readonly Color CardBackground = new Color(0.22f, 0.22f, 0.22f);

        /// <summary>Darker background for contrast</summary>
        public static readonly Color DarkBackground = new Color(0.18f, 0.18f, 0.18f);

        /// <summary>Subtle divider/border color</summary>
        public static readonly Color Divider = new Color(0.35f, 0.35f, 0.35f);

        // Cached Styles
        private static GUIStyle _sectionHeader;
        private static GUIStyle _sectionHeaderLabel;
        private static GUIStyle _cardStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _tagStyle;
        private static GUIStyle _tabButton;
        private static GUIStyle _tabButtonSelected;

        /// <summary>
        /// Style for section headers with brand color accent.
        /// </summary>
        public static GUIStyle SectionHeader
        {
            get
            {
                if (_sectionHeader == null)
                {
                    _sectionHeader = new GUIStyle(EditorStyles.foldoutHeader);
                    _sectionHeader.fontStyle = FontStyle.Bold;
                    _sectionHeader.fontSize = 12;
                }
                return _sectionHeader;
            }
        }

        /// <summary>
        /// Style for section header labels (non-foldout).
        /// </summary>
        public static GUIStyle SectionHeaderLabel
        {
            get
            {
                if (_sectionHeaderLabel == null)
                {
                    _sectionHeaderLabel = new GUIStyle(EditorStyles.boldLabel);
                    _sectionHeaderLabel.fontSize = 12;
                    _sectionHeaderLabel.padding = new RectOffset(4, 4, 6, 6);
                }
                return _sectionHeaderLabel;
            }
        }

        /// <summary>
        /// Style for card/panel backgrounds.
        /// </summary>
        public static GUIStyle CardStyle
        {
            get
            {
                if (_cardStyle == null)
                {
                    _cardStyle = new GUIStyle(EditorStyles.helpBox);
                    _cardStyle.padding = new RectOffset(10, 10, 8, 8);
                    _cardStyle.margin = new RectOffset(0, 0, 4, 4);
                }
                return _cardStyle;
            }
        }

        /// <summary>
        /// Style for subtitle text.
        /// </summary>
        public static GUIStyle SubtitleStyle
        {
            get
            {
                if (_subtitleStyle == null)
                {
                    _subtitleStyle = new GUIStyle(EditorStyles.miniLabel);
                    _subtitleStyle.fontStyle = FontStyle.Italic;
                    _subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                }
                return _subtitleStyle;
            }
        }

        /// <summary>
        /// Style for small tag/badge labels.
        /// </summary>
        public static GUIStyle TagStyle
        {
            get
            {
                if (_tagStyle == null)
                {
                    _tagStyle = new GUIStyle(EditorStyles.miniLabel);
                    _tagStyle.alignment = TextAnchor.MiddleCenter;
                    _tagStyle.fontStyle = FontStyle.Bold;
                    _tagStyle.fontSize = 9;
                    _tagStyle.padding = new RectOffset(6, 6, 2, 2);
                }
                return _tagStyle;
            }
        }

        /// <summary>
        /// Style for unselected tab buttons in the left navigation.
        /// </summary>
        public static GUIStyle TabButton
        {
            get
            {
                if (_tabButton == null)
                {
                    _tabButton = new GUIStyle(EditorStyles.toolbarButton);
                    _tabButton.alignment = TextAnchor.MiddleLeft;
                    _tabButton.padding = new RectOffset(10, 6, 4, 4);
                    _tabButton.fontStyle = FontStyle.Normal;
                }
                return _tabButton;
            }
        }

        /// <summary>
        /// Style for the selected tab button in the left navigation.
        /// </summary>
        public static GUIStyle TabButtonSelected
        {
            get
            {
                if (_tabButtonSelected == null)
                {
                    _tabButtonSelected = new GUIStyle(TabButton);
                    _tabButtonSelected.fontStyle = FontStyle.Bold;
                    _tabButtonSelected.normal.textColor = BrandPurple;
                }
                return _tabButtonSelected;
            }
        }

        // Drawing Helpers

        /// <summary>
        /// Draws a styled section header with brand color accent bar.
        /// </summary>
        public static bool DrawSectionHeader(string title, bool foldout, string icon = null)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 28);

            // Draw accent bar on the left
            Rect accentRect = new Rect(headerRect.x, headerRect.y + 2, 4, headerRect.height - 4);
            EditorGUI.DrawRect(accentRect, BrandPurple);

            // Draw header background
            Rect bgRect = new Rect(headerRect.x + 4, headerRect.y, headerRect.width - 4, headerRect.height);
            EditorGUI.DrawRect(bgRect, CardBackground);

            // Draw foldout with custom positioning
            Rect foldoutRect = new Rect(headerRect.x + 8, headerRect.y + 4, headerRect.width - 12, headerRect.height - 8);

            string displayTitle = string.IsNullOrEmpty(icon) ? title : $"{icon}  {title}";

            bool newFoldout = EditorGUI.Foldout(foldoutRect, foldout, displayTitle, true, SectionHeader);

            return newFoldout;
        }

        /// <summary>
        /// Draws a non-foldable section header with brand color accent bar.
        /// </summary>
        public static void DrawSectionTitle(string title, string icon = null)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 28);

            // Draw accent bar on the left
            Rect accentRect = new Rect(headerRect.x, headerRect.y + 2, 4, headerRect.height - 4);
            EditorGUI.DrawRect(accentRect, BrandPurple);

            // Draw header background
            Rect bgRect = new Rect(headerRect.x + 4, headerRect.y, headerRect.width - 4, headerRect.height);
            EditorGUI.DrawRect(bgRect, CardBackground);

            // Draw title
            Rect labelRect = new Rect(headerRect.x + 14, headerRect.y + 4, headerRect.width - 18, headerRect.height - 8);
            string displayTitle = string.IsNullOrEmpty(icon) ? title : $"{icon}  {title}";
            EditorGUI.LabelField(labelRect, displayTitle, SectionHeaderLabel);
        }

        /// <summary>
        /// Draws a small colored tag/badge.
        /// </summary>
        public static void DrawTag(Rect rect, string text, Color bgColor)
        {
            EditorGUI.DrawRect(rect, bgColor);
            EditorGUI.LabelField(rect, text, TagStyle);
        }

        /// <summary>
        /// Draws a horizontal divider line.
        /// </summary>
        public static void DrawDivider()
        {
            GUILayout.Space(4);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, Divider);
            GUILayout.Space(4);
        }

        /// <summary>
        /// Draws a subtle horizontal separator.
        /// </summary>
        public static void DrawSeparator()
        {
            GUILayout.Space(8);
        }

        /// <summary>
        /// Begins a card/panel scope with Material-style background.
        /// </summary>
        public static void BeginCard()
        {
            GUILayout.BeginVertical(CardStyle);
        }

        /// <summary>
        /// Ends a card/panel scope.
        /// </summary>
        public static void EndCard()
        {
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draws a row with alternating background for lists.
        /// </summary>
        public static void DrawListRowBackground(Rect rect, int index, Color baseColor)
        {
            Color rowColor = index % 2 == 0 ? baseColor : new Color(baseColor.r + 0.03f, baseColor.g + 0.03f, baseColor.b + 0.03f);
            EditorGUI.DrawRect(rect, rowColor);
        }

        /// <summary>
        /// Gets the appropriate status color based on severity/status.
        /// </summary>
        public static Color GetStatusColor(StatusLevel level)
        {
            switch (level)
            {
                case StatusLevel.Success: return StatusGreen;
                case StatusLevel.Warning: return StatusYellow;
                case StatusLevel.Error: return StatusRed;
                default: return StatusGray;
            }
        }

        /// <summary>
        /// Draws a styled button with brand color.
        /// </summary>
        public static bool DrawAccentButton(string text, params GUILayoutOption[] options)
        {
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = BrandPurple;
            bool clicked = GUILayout.Button(text, options);
            GUI.backgroundColor = originalBg;
            return clicked;
        }

        /// <summary>
        /// Draws a styled button with brand color.
        /// </summary>
        public static bool DrawAccentButton(GUIContent content, params GUILayoutOption[] options)
        {
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = BrandPurple;
            bool clicked = GUILayout.Button(content, options);
            GUI.backgroundColor = originalBg;
            return clicked;
        }

        public enum StatusLevel
        {
            Default,
            Success,
            Warning,
            Error
        }
    }
}
