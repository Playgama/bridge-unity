using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
{
    public sealed class SettingsTab : ITab
    {
        public string TabName { get { return "Settings"; } }

        private BuildInfo _buildInfo;
        private string _keyInput = "";
        private string _status = "";
        private Vector2 _scroll;

        private bool _foldHeader = false;
        private bool _foldBridge = true;
        private bool _foldTinify = true;

        private bool _showEditorDebugWindows = true;

        public const string Pref_ShowEditorDebugWindows = "PlaygamaBridge_ShowEditorDebugWindows";

        private bool? _keyValid = null;
        private bool _validating = false;

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
            _keyInput = TinifyUtility.GetKey();
            _showEditorDebugWindows = EditorPrefs.GetBool(Pref_ShowEditorDebugWindows, true);
        }

        public void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawHeader();
                DrawBridgeSettingsBlock();
                DrawTinifyKeyBlock();

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

        private void DrawTinifyKeyBlock()
        {
            // Header with status indicator
            string headerText = "Tinify (TinyPNG) API Key";
            if (!string.IsNullOrEmpty(TinifyUtility.GetKey()))
            {
                headerText += _keyValid == true ? " ✓" : _keyValid == false ? " ✗" : "";
            }

            _foldTinify = BridgeStyles.DrawSectionHeader(headerText, _foldTinify, "\u26A1");
            if (!_foldTinify) return;

            BridgeStyles.BeginCard();

            // Setup guidance if no key
            if (string.IsNullOrEmpty(TinifyUtility.GetKey()))
            {
                Rect guideRect = EditorGUILayout.GetControlRect(false, 60);
                EditorGUI.DrawRect(guideRect, new Color(0.2f, 0.3f, 0.5f, 0.3f));

                GUIStyle guideStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
                GUI.Label(new Rect(guideRect.x + 10, guideRect.y + 5, guideRect.width - 20, guideRect.height - 10),
                    "<b>TinyPNG Setup</b>\n" +
                    "Get a free API key (500 compressions/month) to optimize PNG/JPG textures.\n" +
                    "This is optional - the plugin works without it.", guideStyle);

                GUILayout.Space(4);

                if (GUILayout.Button("Get Free API Key at tinypng.com →", EditorStyles.linkLabel))
                {
                    Application.OpenURL("https://tinypng.com/developers");
                }

                GUILayout.Space(8);
            }

            // Key input
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("API Key", GUILayout.Width(55));
                _keyInput = EditorGUILayout.PasswordField(_keyInput);

                if (GUILayout.Button("Save", GUILayout.Width(60)))
                {
                    TinifyUtility.SetKey(_keyInput);
                    _status = "Key saved.";
                    _keyValid = null;
                }

                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    TinifyUtility.ClearKey();
                    _keyInput = "";
                    _status = "Key cleared.";
                    _keyValid = null;
                }
            }

            GUILayout.Space(6);

            // Validation section
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !_validating && !string.IsNullOrEmpty(_keyInput);

                if (BridgeStyles.DrawAccentButton(new GUIContent("Validate Key"), GUILayout.Width(100), GUILayout.Height(24)))
                    ValidateKey();

                GUI.enabled = true;

                GUILayout.Space(10);

                // Status display
                string keyState;
                Color stateColor;

                if (string.IsNullOrEmpty(TinifyUtility.GetKey()))
                {
                    keyState = "Not configured";
                    stateColor = BridgeStyles.StatusGray;
                }
                else if (_validating)
                {
                    keyState = "Validating...";
                    stateColor = BridgeStyles.StatusYellow;
                }
                else if (_keyValid == true)
                {
                    keyState = "✓ Valid";
                    stateColor = BridgeStyles.StatusGreen;
                }
                else if (_keyValid == false)
                {
                    keyState = "✗ Invalid";
                    stateColor = BridgeStyles.StatusRed;
                }
                else
                {
                    keyState = "Not validated";
                    stateColor = BridgeStyles.StatusGray;
                }

                // Status badge
                GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
                Rect statusRect = GUILayoutUtility.GetRect(90, 20);
                EditorGUI.DrawRect(statusRect, stateColor);
                GUI.Label(statusRect, keyState, badgeStyle);

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6);

            // Info about validation
            EditorGUILayout.LabelField("Validation sends a tiny test image to verify your key. No project assets are uploaded.", BridgeStyles.SubtitleStyle);

            // Usage info if key is valid
            if (_keyValid == true)
            {
                GUILayout.Space(4);
                BridgeStyles.BeginCard();
                GUIStyle usageStyle = new GUIStyle(EditorStyles.miniLabel) { richText = true };
                EditorGUILayout.LabelField("<color=#66ff66>Ready to use!</color> Go to the Textures tab to optimize images with TinyPNG.", usageStyle);
                BridgeStyles.EndCard();
            }

            BridgeStyles.EndCard();
        }

        private void ValidateKey()
        {
            _validating = true;
            _status = "Validating key...";

            TinifyUtility.SetKey(_keyInput);

            TinifyUtility.ValidateKeyAsync((valid, msg) =>
            {
                _validating = false;
                _keyValid = valid;
                _status = msg;

                try { EditorWindow.focusedWindow?.Repaint(); } catch { }
            });
        }
    }
}
