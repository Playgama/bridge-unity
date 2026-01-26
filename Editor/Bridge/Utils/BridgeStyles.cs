using UnityEngine;
using UnityEditor;

namespace Playgama.Editor
{
    public static class BridgeStyles
    {
        // Brand Colors
        public static readonly Color brandPurple = new Color(0.592f, 0.282f, 1f);
        public static readonly Color brandPurpleLight = new Color(0.592f, 0.282f, 1f, 0.15f);
        public static readonly Color brandPurpleDark = new Color(0.45f, 0.2f, 0.8f);

        // Status Colors
        public static readonly Color statusGreen = new Color(0.2f, 0.5f, 0.3f);
        public static readonly Color statusYellow = new Color(0.55f, 0.45f, 0.2f);
        public static readonly Color statusRed = new Color(0.55f, 0.25f, 0.25f);
        public static readonly Color statusGray = new Color(0.28f, 0.28f, 0.28f);

        // Background Colors
        public static readonly Color cardBackground = new Color(0.22f, 0.22f, 0.22f);
        public static readonly Color darkBackground = new Color(0.18f, 0.18f, 0.18f);
        public static readonly Color divider = new Color(0.35f, 0.35f, 0.35f);

        // Cached Styles
        private static GUIStyle _sectionHeader;
        private static GUIStyle _sectionHeaderLabel;
        private static GUIStyle _cardStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _tagStyle;
        private static GUIStyle _tabButton;
        private static GUIStyle _tabButtonSelected;

        public static GUIStyle sectionHeader
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

        public static GUIStyle sectionHeaderLabel
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

        public static GUIStyle cardStyle
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

        public static GUIStyle subtitleStyle
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

        public static GUIStyle tagStyle
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

        public static GUIStyle tabButton
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

        public static GUIStyle tabButtonSelected
        {
            get
            {
                if (_tabButtonSelected == null)
                {
                    _tabButtonSelected = new GUIStyle(tabButton);
                    _tabButtonSelected.fontStyle = FontStyle.Bold;
                    _tabButtonSelected.normal.textColor = brandPurple;
                }
                return _tabButtonSelected;
            }
        }


        public static bool DrawSectionHeader(string title, bool foldout, string icon = null)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 28);

            Rect accentRect = new Rect(headerRect.x, headerRect.y + 2, 4, headerRect.height - 4);
            EditorGUI.DrawRect(accentRect, brandPurple);

            Rect bgRect = new Rect(headerRect.x + 4, headerRect.y, headerRect.width - 4, headerRect.height);
            EditorGUI.DrawRect(bgRect, cardBackground);

            Rect foldoutRect = new Rect(headerRect.x + 8, headerRect.y + 4, headerRect.width - 12, headerRect.height - 8);
            string displayTitle = string.IsNullOrEmpty(icon) ? title : $"{icon}  {title}";

            return EditorGUI.Foldout(foldoutRect, foldout, displayTitle, true, sectionHeader);
        }

        public static void DrawSectionTitle(string title, string icon = null)
        {
            Rect headerRect = EditorGUILayout.GetControlRect(false, 28);

            Rect accentRect = new Rect(headerRect.x, headerRect.y + 2, 4, headerRect.height - 4);
            EditorGUI.DrawRect(accentRect, brandPurple);

            Rect bgRect = new Rect(headerRect.x + 4, headerRect.y, headerRect.width - 4, headerRect.height);
            EditorGUI.DrawRect(bgRect, cardBackground);

            Rect labelRect = new Rect(headerRect.x + 14, headerRect.y + 4, headerRect.width - 18, headerRect.height - 8);
            string displayTitle = string.IsNullOrEmpty(icon) ? title : $"{icon}  {title}";
            EditorGUI.LabelField(labelRect, displayTitle, sectionHeaderLabel);
        }

        public static void DrawTag(Rect rect, string text, Color bgColor)
        {
            EditorGUI.DrawRect(rect, bgColor);
            EditorGUI.LabelField(rect, text, tagStyle);
        }

        public static void DrawDivider()
        {
            GUILayout.Space(4);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, divider);
            GUILayout.Space(4);
        }

        public static void DrawSeparator()
        {
            GUILayout.Space(8);
        }

        public static void BeginCard()
        {
            GUILayout.BeginVertical(cardStyle);
        }

        public static void EndCard()
        {
            GUILayout.EndVertical();
        }

        public static void DrawListRowBackground(Rect rect, int index, Color baseColor)
        {
            Color rowColor = index % 2 == 0 ? baseColor : new Color(baseColor.r + 0.03f, baseColor.g + 0.03f, baseColor.b + 0.03f);
            EditorGUI.DrawRect(rect, rowColor);
        }

        public static Color GetStatusColor(StatusLevel level)
        {
            switch (level)
            {
                case StatusLevel.Success: return statusGreen;
                case StatusLevel.Warning: return statusYellow;
                case StatusLevel.Error: return statusRed;
                default: return statusGray;
            }
        }

        public static bool DrawAccentButton(string text, params GUILayoutOption[] options)
        {
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = brandPurple;
            bool clicked = GUILayout.Button(text, options);
            GUI.backgroundColor = originalBg;
            return clicked;
        }

        public static bool DrawAccentButton(GUIContent content, params GUILayoutOption[] options)
        {
            Color originalBg = GUI.backgroundColor;
            GUI.backgroundColor = brandPurple;
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
