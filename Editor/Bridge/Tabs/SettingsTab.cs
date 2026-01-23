using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
{
    public sealed class SettingsTab : ITab
    {
        public string TabName { get { return "Settings"; } }

        private BuildInfo _buildInfo;
        private string _status = "";
        private Vector2 _scroll;

        private bool _foldHeader = false;
        private bool _foldBridge = true;

        private bool _showEditorDebugWindows = true;

        public const string Pref_ShowEditorDebugWindows = "PlaygamaBridge_ShowEditorDebugWindows";

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
            _showEditorDebugWindows = EditorPrefs.GetBool(Pref_ShowEditorDebugWindows, true);
        }

        public void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawHeader();
                DrawBridgeSettingsBlock();

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private void DrawHeader()
        {
            _foldHeader = BridgeStyles.DrawSectionHeader("About Settings", _foldHeader, "\u2139");
            if (_foldHeader)
            {
                BridgeStyles.BeginCard();
                EditorGUILayout.LabelField("Configure Playgama Bridge and optimization settings.", BridgeStyles.SubtitleStyle);
                BridgeStyles.EndCard();
            }
        }

        private void DrawBridgeSettingsBlock()
        {
            _foldBridge = BridgeStyles.DrawSectionHeader("Bridge Settings", _foldBridge, "\u2699");
            if (!_foldBridge) return;

            BridgeStyles.BeginCard();

            // Debug windows toggle with better explanation
            using (new EditorGUILayout.HorizontalScope())
            {
                bool newShowDebug = EditorGUILayout.ToggleLeft("Show Editor Debug Windows", _showEditorDebugWindows, GUILayout.Width(200));
                if (newShowDebug != _showEditorDebugWindows)
                {
                    _showEditorDebugWindows = newShowDebug;
                    EditorPrefs.SetBool(Pref_ShowEditorDebugWindows, _showEditorDebugWindows);
                    _status = _showEditorDebugWindows ? "Debug windows enabled." : "Debug windows disabled.";
                }

                // Status indicator
                string statusIcon = _showEditorDebugWindows ? "ON" : "OFF";
                Color statusColor = _showEditorDebugWindows ? BridgeStyles.StatusGreen : BridgeStyles.StatusGray;
                GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white }
                };
                Rect badgeRect = GUILayoutUtility.GetRect(35, 18);
                EditorGUI.DrawRect(badgeRect, statusColor);
                GUI.Label(badgeRect, statusIcon, statusStyle);
            }

            GUILayout.Space(4);

            // Explanation box
            BridgeStyles.BeginCard();
            GUIStyle explainStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, richText = true };
            EditorGUILayout.LabelField(
                "<b>What are debug windows?</b>\n" +
                "When testing in the Editor, Bridge shows popup windows to simulate:\n" +
                "• Advertisement responses (reward earned, closed, etc.)\n" +
                "• In-app purchase dialogs\n" +
                "• Platform-specific features\n\n" +
                "Disable this if the popups interfere with your workflow.",
                explainStyle);
            BridgeStyles.EndCard();

            BridgeStyles.EndCard();
        }
    }
}
