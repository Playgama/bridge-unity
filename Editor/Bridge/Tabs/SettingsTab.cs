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

        private static class UI
        {
            public static readonly GUIContent Header = new GUIContent(
                "Settings",
                "Global plugin settings.\n" +
                "Tinify is optional: the plugin works without it.");

            public static readonly GUIContent HeaderHelp = new GUIContent(
                "Tinify is optional. If no key is set, the plugin continues to work without Tinify.",
                "Tinify is used only when you explicitly run Tinify optimization from other tabs.");

            public static readonly GUIContent BridgeBlockTitle = new GUIContent(
                "Bridge Settings",
                "Settings for Playgama Bridge SDK behavior in the editor.");

            public static readonly GUIContent ShowEditorDebugWindows = new GUIContent(
                "Show Editor Debug Windows",
                "When enabled, Bridge will show debug windows for advertisements and other features in the Unity Editor.\n" +
                "Disable this to hide the debug popups during editor testing.");

            public static readonly GUIContent TinifyBlockTitle = new GUIContent(
                "Tinify (TinyPNG) API Key",
                "Manage the Tinify API key used for source PNG/JPG optimization.");

            public static readonly GUIContent KeyLabel = new GUIContent(
                "Key",
                "Your Tinify API key.\n" +
                "It is stored locally in the editor (via TinifyUtility).");

            public static readonly GUIContent Save = new GUIContent(
                "Save",
                "Save the current key value.\n" +
                "This does not contact Tinify; use Validate to check if the key works.");

            public static readonly GUIContent Clear = new GUIContent(
                "Clear",
                "Remove the stored key.\n" +
                "Tinify features will be disabled until a new key is saved.");

            public static readonly GUIContent Validate = new GUIContent(
                "Validate",
                "Validate the saved key by performing a minimal Tinify API request.\n" +
                "No project assets are uploaded during validation.");

            public static readonly GUIContent StatusLabel = new GUIContent(
                "Status:",
                "Current key state:\n" +
                "• Not set: there is no stored key\n" +
                "• Validating: a request is in progress\n" +
                "• Valid/Invalid: last validation result\n" +
                "• Unknown: not validated yet");

            public static readonly GUIContent ValidationHelp = new GUIContent(
                "Validation sends a minimal image to Tinify API to verify authentication. No project assets are uploaded during validation.",
                "This is a safe connectivity/auth check only.");
        }

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

                _foldHeader = BridgeStyles.DrawSectionHeader("About Settings", _foldHeader, "\u2139");
                if (_foldHeader)
                {
                    BridgeStyles.BeginCard();
                    EditorGUILayout.LabelField("Configure Playgama Bridge and optimization settings.", BridgeStyles.SubtitleStyle);
                    BridgeStyles.EndCard();
                }

                DrawBridgeSettingsBlock();
                DrawTinifyKeyBlock();

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private void DrawBridgeSettingsBlock()
        {
            _foldBridge = BridgeStyles.DrawSectionHeader("Bridge Settings", _foldBridge, "\u2699");
            if (!_foldBridge) return;

            BridgeStyles.BeginCard();

            bool newShowDebug = EditorGUILayout.ToggleLeft(UI.ShowEditorDebugWindows, _showEditorDebugWindows);
            if (newShowDebug != _showEditorDebugWindows)
            {
                _showEditorDebugWindows = newShowDebug;
                EditorPrefs.SetBool(Pref_ShowEditorDebugWindows, _showEditorDebugWindows);
                _status = _showEditorDebugWindows
                    ? "Editor debug windows enabled."
                    : "Editor debug windows disabled.";
            }

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Controls whether Bridge shows ad debug popups in the Editor.", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();
        }

        private void DrawTinifyKeyBlock()
        {
            _foldTinify = BridgeStyles.DrawSectionHeader("Tinify (TinyPNG) API Key", _foldTinify, "\u26A1");
            if (!_foldTinify) return;

            BridgeStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(UI.KeyLabel, GUILayout.Width(30));
                _keyInput = EditorGUILayout.PasswordField(_keyInput);

                if (GUILayout.Button(UI.Save, GUILayout.Width(70)))
                {
                    TinifyUtility.SetKey(_keyInput);
                    _status = "Key saved.";
                    _keyValid = null;
                }

                if (GUILayout.Button(UI.Clear, GUILayout.Width(70)))
                {
                    TinifyUtility.ClearKey();
                    _keyInput = "";
                    _status = "Key cleared.";
                    _keyValid = null;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !_validating;

                if (BridgeStyles.DrawAccentButton(UI.Validate, GUILayout.Width(90)))
                    ValidateKey();

                GUI.enabled = true;

                GUILayout.Space(10);

                string keyState;
                if (string.IsNullOrEmpty(TinifyUtility.GetKey())) keyState = "Not set";
                else if (_validating) keyState = "Validating...";
                else if (_keyValid == true) keyState = "Valid";
                else if (_keyValid == false) keyState = "Invalid";
                else keyState = "Unknown";

                GUILayout.Label(new GUIContent(UI.StatusLabel.text + " " + keyState, UI.StatusLabel.tooltip), EditorStyles.miniLabel);
            }

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Validation sends a minimal image to verify auth. No project assets uploaded.", BridgeStyles.SubtitleStyle);
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
