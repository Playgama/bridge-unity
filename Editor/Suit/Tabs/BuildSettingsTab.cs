using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit.Tabs
{
    /// <summary>
    /// Build workflow tab focused on build size control:
    /// - Output folder selection (persisted in EditorPrefs)
    /// - Scene enable/disable management (Build Settings)
    /// - WebGL build-size related toggles (Development Build + WebGL compression, best-effort)
    /// - Single entry point to trigger Build & Analyze (always via delayCall, never inside OnGUI)
    /// - Displays a snapshot of the last analysis results (if available)
    /// </summary>
    public sealed class BuildSettingsTab : ITab
    {
        /// <summary>Displayed name of the tab in the parent UI.</summary>
        public string TabName { get { return "Build Settings"; } }

        private const string Pref_OutputPath = "SUIT_BUILD_OUTPUT_PATH";

        private BuildInfo _buildInfo;

        private Vector2 _scroll;
        private string _status = "";

        // Foldout states for collapsible sections.
        private bool _foldHeader = false;
        private bool _foldOutput = true;
        private bool _foldScenes = true;
        private bool _foldWebGL = true;
        private bool _foldBuild = true;
        private bool _foldLastBuild = true;

        // Cached UI state.
        private string _outputPath;
        private bool _devBuild;
        private WebGLCompressionState _compressionState;

        /// <summary>
        /// Summary state for WebGL compression setting read via reflection.
        /// This is intentionally coarse: Unity's API surface differs by version.
        /// </summary>
        private enum WebGLCompressionState
        {
            Unknown,
            Disabled,
            Enabled_Brotli,
            Enabled_Gzip,
            Enabled_Other
        }

        /// <summary>
        /// Centralized GUIContent labels with tooltips to keep IMGUI code readable and consistent.
        /// </summary>
        private static class UI
        {
            public static readonly GUIContent HeaderInfo = new GUIContent(
                "Build Settings",
                "Workflow hub for build-size related settings and the Build & Analyze entry point.");

            public static readonly GUIContent HeaderHelp = new GUIContent(
                "This tab is focused on build size workflow: selecting scenes, WebGL build-size toggles, and a single Build & Analyze entry point.\n" +
                "Build is always triggered via EditorApplication.delayCall (never inside OnGUI).",
                "General notes about how Playgama Suit triggers builds and why it avoids running build logic inside OnGUI.");

            public static readonly GUIContent OutputTitle = new GUIContent(
                "Output Path",
                "Folder where the WebGL build will be written.");

            public static readonly GUIContent OutputFolder = new GUIContent(
                "Folder",
                "Build output directory.\n" +
                "Recommended: keep it outside Assets/ to avoid accidental imports and reindexing.");

            public static readonly GUIContent Browse = new GUIContent(
                "Browse",
                "Choose a build output folder.");

            public static readonly GUIContent Reset = new GUIContent(
                "Reset",
                "Reset output folder to a default location under the project root: Builds/WebGL.");

            public static readonly GUIContent OutputTip = new GUIContent(
                "Tip: keep build output outside Assets/ to avoid accidental imports.",
                "Why: anything under Assets/ is treated as content and may trigger imports or reindexing.");

            public static readonly GUIContent ScenesTitle = new GUIContent(
                "Scenes (Build Settings)",
                "These are scenes from Unity Build Settings.\n" +
                "Playgama Suit uses only enabled scenes when triggering a build.");

            public static readonly GUIContent EnableAll = new GUIContent(
                "Enable All",
                "Enable every scene in Build Settings.");

            public static readonly GUIContent DisableAll = new GUIContent(
                "Disable All",
                "Disable every scene in Build Settings.");

            public static readonly GUIContent AddOpenScenes = new GUIContent(
                "Add Open Scenes",
                "Add currently open scenes (from the Editor) to Build Settings, if they are under Assets/ and not already present.\n" +
                "Scenes are added as enabled.");

            public static readonly GUIContent EnabledCount = new GUIContent(
                "Enabled:",
                "How many Build Settings scenes are enabled.");

            public static readonly GUIContent SceneEnabled = new GUIContent(
                "",
                "Enable/disable this scene for builds.\n" +
                "Only enabled scenes are included when Playgama Suit triggers the build.");

            public static readonly GUIContent ScenePath = new GUIContent(
                "",
                "Scene asset path from Build Settings.");

            public static readonly GUIContent ScenePing = new GUIContent(
                "Ping",
                "Ping the scene asset in the Project window.");

            public static readonly GUIContent WebGLTogglesTitle = new GUIContent(
                "WebGL Build Size Toggles",
                "Quick build-size related toggles for WebGL (best-effort across Unity versions).");

            public static readonly GUIContent DevelopmentBuild = new GUIContent(
                "Development Build",
                "If enabled: creates a development build (bigger, slower, includes extra debug info).\n" +
                "For smallest release size, keep this OFF.");

            public static readonly GUIContent CompressionLabel = new GUIContent(
                "Compression",
                "WebGL build compression format (best-effort).\n" +
                "Works only if the current Unity version exposes the API.\n" +
                "Also depends on your hosting setup supporting the chosen compression.");

            public static readonly GUIContent CompressionButton = new GUIContent(
                "Unknown",
                "Current WebGL compression setting (best-effort).\n" +
                "Click to choose Disabled / Brotli / Gzip.");

            public static readonly GUIContent WebGLTip = new GUIContent(
                "For build size you generally want:\n" +
                "• Development Build OFF (release size)\n" +
                "• WebGL compression ON if hosting supports it (Brotli/Gzip)\n",
                "General recommendation. Hosting must be configured to serve compressed files correctly.");

            public static readonly GUIContent BuildAnalyzeTitle = new GUIContent(
                "Build & Analyze",
                "Trigger a WebGL build and run Playgama Suit analysis.\n" +
                "The build is scheduled via delayCall to keep IMGUI safe and responsive.");

            public static readonly GUIContent BuildAnalyzeButton = new GUIContent(
                "Build & Analyze (WebGL)",
                "Starts a WebGL build to the selected output path, then runs Playgama Suit analysis.\n" +
                "Only enabled Build Settings scenes are included.\n" +
                "Note: does not silently switch Build Target.");

            public static readonly GUIContent OpenOutput = new GUIContent(
                "Open Output Folder",
                "Reveal the build output folder in your file explorer.");

            public static readonly GUIContent BuildAnalyzeHelp = new GUIContent(
                "Build runs with DetailedBuildReport. Analysis mode is chosen automatically:\n" +
                "• Packed Assets (if BuildReport provides usable mapping)\n" +
                "• Dependencies Fallback (guaranteed)\n",
                "Playgama Suit chooses the strongest analysis mode available for the current Unity build pipeline output.");

            public static readonly GUIContent LastSnapshotTitle = new GUIContent(
                "Last Analysis Snapshot",
                "Quick readout of the last analysis results stored in memory.");

            public static readonly GUIContent NoSnapshot = new GUIContent(
                "No build analyzed yet.",
                "Run Build & Analyze to populate this section.");

            public static readonly GUIContent CopySnapshot = new GUIContent(
                "Copy Snapshot to Clipboard",
                "Copy a short build/analysis summary into the system clipboard.");
        }

        /// <summary>
        /// Receives analysis model references and initializes cached UI state.
        /// </summary>
        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;

            _outputPath = EditorPrefs.GetString(Pref_OutputPath, "");
            if (string.IsNullOrEmpty(_outputPath))
            {
                _outputPath = Path.Combine(GetProjectRoot(), "Builds", "WebGL");
                _outputPath = _outputPath.Replace('\\', '/');
            }

            _devBuild = EditorUserBuildSettings.development;

            ReadWebGLCompression(out _compressionState);

            _status = "";
        }

        /// <summary>
        /// IMGUI entry point for this tab.
        /// </summary>
        public void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawHeader();
                DrawOutputBlock();
                DrawScenesBlock();
                DrawWebGLBuildSizeBlock();
                DrawBuildAndAnalyzeBlock();
                DrawLastBuildBlock();

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        /// <summary>
        /// Draws a brief overview of what this tab controls.
        /// </summary>
        private void DrawHeader()
        {
            _foldHeader = SuitStyles.DrawSectionHeader("About Build Settings", _foldHeader, "\u2139");
            if (_foldHeader)
            {
                SuitStyles.BeginCard();
                EditorGUILayout.LabelField("Build-size workflow: scenes, WebGL toggles, Build & Analyze.", SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
            }
        }

        /// <summary>
        /// Output path selection and persistence.
        /// </summary>
        private void DrawOutputBlock()
        {
            _foldOutput = SuitStyles.DrawSectionHeader("Output Path", _foldOutput, "\u2301");
            if (!_foldOutput) return;

            SuitStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(UI.OutputFolder, GUILayout.Width(50));
                _outputPath = EditorGUILayout.TextField(_outputPath);

                if (GUILayout.Button(UI.Browse, GUILayout.Width(80)))
                {
                    string chosen = EditorUtility.SaveFolderPanel("Playgama Suit - Choose Build Output Folder", _outputPath, "");
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        _outputPath = chosen.Replace('\\', '/');
                        EditorPrefs.SetString(Pref_OutputPath, _outputPath);
                        _status = "Output path updated.";
                    }
                }

                if (GUILayout.Button(UI.Reset, GUILayout.Width(70)))
                {
                    _outputPath = Path.Combine(GetProjectRoot(), "Builds", "WebGL").Replace('\\', '/');
                    EditorPrefs.SetString(Pref_OutputPath, _outputPath);
                    _status = "Output path reset.";
                }
            }

            EditorGUILayout.LabelField("Tip: keep build output outside Assets/ to avoid accidental imports.", SuitStyles.SubtitleStyle);
            SuitStyles.EndCard();
        }

        /// <summary>
        /// Scene management: enables/disables scenes in Unity Build Settings and provides quick helpers.
        /// </summary>
        private void DrawScenesBlock()
        {
            int sceneCount = EditorBuildSettings.scenes?.Length ?? 0;
            _foldScenes = SuitStyles.DrawSectionHeader($"Scenes ({sceneCount} in Build Settings)", _foldScenes, "\u2302");
            if (!_foldScenes) return;

            SuitStyles.BeginCard();
            var scenes = EditorBuildSettings.scenes;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(UI.EnableAll, GUILayout.Width(100)))
                    SetAllScenesEnabled(true);

                if (GUILayout.Button(UI.DisableAll, GUILayout.Width(100)))
                    SetAllScenesEnabled(false);

                if (GUILayout.Button(UI.AddOpenScenes, GUILayout.Width(140)))
                    AddOpenScenesToBuild();

                GUILayout.FlexibleSpace();

                int enabledCount = 0;
                for (int i = 0; i < scenes.Length; i++)
                    if (scenes[i] != null && scenes[i].enabled) enabledCount++;

                GUILayout.Label($"{UI.EnabledCount.text} {enabledCount}/{scenes.Length}", EditorStyles.miniLabel);
            }

            GUILayout.Space(6);

            for (int i = 0; i < scenes.Length; i++)
            {
                var s = scenes[i];
                if (s == null) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    bool en = EditorGUILayout.Toggle(UI.SceneEnabled, s.enabled, GUILayout.Width(18));
                    if (en != s.enabled)
                    {
                        s.enabled = en;
                        scenes[i] = s;
                        EditorBuildSettings.scenes = scenes;
                    }

                    EditorGUILayout.LabelField(UI.ScenePath, new GUIContent(s.path, "Scene path in Build Settings."));

                    if (GUILayout.Button(UI.ScenePing, GUILayout.Width(50)))
                    {
                        var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(s.path);
                        if (obj != null) EditorGUIUtility.PingObject(obj);
                    }
                }
            }

            EditorGUILayout.LabelField("Playgama Suit uses enabled scenes from Build Settings for the build.", SuitStyles.SubtitleStyle);
            SuitStyles.EndCard();
        }

        /// <summary>
        /// WebGL-oriented toggles that influence build size (where available).
        /// </summary>
        private void DrawWebGLBuildSizeBlock()
        {
            _foldWebGL = SuitStyles.DrawSectionHeader("WebGL Build Size Toggles", _foldWebGL, "\u2699");
            if (!_foldWebGL) return;

            SuitStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                bool newDev = EditorGUILayout.ToggleLeft(UI.DevelopmentBuild, _devBuild, GUILayout.Width(160));
                if (newDev != _devBuild)
                {
                    _devBuild = newDev;
                    EditorUserBuildSettings.development = _devBuild;
                    _status = "Development Build updated.";
                }

                GUILayout.Space(12);

                GUILayout.Label(UI.CompressionLabel, GUILayout.Width(90));
                DrawCompressionDropdown();

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.LabelField("Development Build OFF + WebGL compression ON for smallest size.", SuitStyles.SubtitleStyle);
            SuitStyles.EndCard();
        }

        /// <summary>
        /// Build & Analyze trigger and convenience actions.
        /// </summary>
        private void DrawBuildAndAnalyzeBlock()
        {
            _foldBuild = SuitStyles.DrawSectionHeader("Build & Analyze", _foldBuild, "\u26A1");
            if (!_foldBuild) return;

            SuitStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = HasEnabledScenes();

                if (SuitStyles.DrawAccentButton(UI.BuildAnalyzeButton, GUILayout.Height(32)))
                {
                    // Build must never run inside OnGUI; delayCall is safe for long operations.
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            EnsureDirectory(_outputPath);

                            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                            {
                                UnityEngine.Debug.LogWarning("Suit: Active build target is not WebGL. Build size for WebGL may be invalid unless you switch target.");
                            }

                            InvokeBuildAnalyzer(_outputPath);

                            _status = "Build started...";
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogException(ex);
                            _status = "Build failed to start: " + ex.Message;
                        }
                    };
                }

                GUI.enabled = true;

                if (GUILayout.Button(UI.OpenOutput, GUILayout.Height(32), GUILayout.Width(160)))
                {
                    if (Directory.Exists(_outputPath))
                        EditorUtility.RevealInFinder(_outputPath);
                    else
                        _status = "Folder does not exist yet.";
                }
            }

            EditorGUILayout.LabelField("Analysis mode chosen automatically: Packed Assets or Dependencies Fallback.", SuitStyles.SubtitleStyle);
            SuitStyles.EndCard();
        }

        /// <summary>
        /// Displays a quick snapshot of the last analysis stored in BuildInfo.
        /// </summary>
        private void DrawLastBuildBlock()
        {
            _foldLastBuild = SuitStyles.DrawSectionHeader("Last Build Snapshot", _foldLastBuild, "\u2139");
            if (!_foldLastBuild) return;

            SuitStyles.BeginCard();
            if (_buildInfo == null || !_buildInfo.HasData)
            {
                EditorGUILayout.LabelField(UI.NoSnapshot.text, SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
                return;
            }

            EditorGUILayout.LabelField(new GUIContent("Total Build Size (real)", "Total build size as measured from build output."), SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes));
            EditorGUILayout.LabelField(new GUIContent("Analysis Mode", "Which data source Playgama Suit used to map assets to size."), _buildInfo.DataMode.ToString());
            EditorGUILayout.LabelField(new GUIContent("Tracked Asset Count", "Number of assets included in the analysis."), _buildInfo.TrackedAssetCount.ToString());

            string tracked = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
            if (_buildInfo.DataMode == BuildDataMode.DependenciesFallback) tracked += " (estimated)";
            EditorGUILayout.LabelField(new GUIContent("Tracked Bytes", "Sum of tracked asset sizes (may be estimated in fallback mode)."), tracked);

            if (GUILayout.Button(UI.CopySnapshot, GUILayout.Width(220)))
            {
                string txt =
                    "Playgama Suit Build Snapshot\n" +
                    "-------------------\n" +
                    $"Total Build Size: {SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes)}\n" +
                    $"Mode: {_buildInfo.DataMode}\n" +
                    $"Tracked Assets: {_buildInfo.TrackedAssetCount}\n" +
                    $"Tracked Bytes: {tracked}\n";
                EditorGUIUtility.systemCopyBuffer = txt;
                _status = "Snapshot copied to clipboard.";
            }
            SuitStyles.EndCard();
        }

        /// <summary>
        /// Checks if at least one scene in Build Settings is enabled.
        /// Used to disable the Build button when a build would be empty.
        /// </summary>
        private bool HasEnabledScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] != null && scenes[i].enabled)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Enables/disables all scenes in Build Settings.
        /// </summary>
        private void SetAllScenesEnabled(bool enabled)
        {
            var scenes = EditorBuildSettings.scenes;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i] == null) continue;
                scenes[i].enabled = enabled;
            }
            EditorBuildSettings.scenes = scenes;
            _status = enabled ? "All scenes enabled." : "All scenes disabled.";
        }

        /// <summary>
        /// Adds all currently open Editor scenes to Build Settings if:
        /// - The scene is valid
        /// - The scene has a path under Assets/
        /// - It is not already present in the Build Settings list
        /// Added scenes are marked enabled.
        /// </summary>
        private void AddOpenScenesToBuild()
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var sc = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (!sc.IsValid()) continue;
                if (string.IsNullOrEmpty(sc.path)) continue;
                if (!sc.path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;

                bool exists = false;
                for (int k = 0; k < list.Count; k++)
                {
                    if (string.Equals(list[k].path, sc.path, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    list.Add(new EditorBuildSettingsScene(sc.path, true));
            }

            EditorBuildSettings.scenes = list.ToArray();
            _status = "Open scenes added (if not present).";
        }

        /// <summary>
        /// Returns the project root folder (parent of Assets/).
        /// Used to create a stable default output folder.
        /// </summary>
        private static string GetProjectRoot()
        {
            var assets = Application.dataPath.Replace('\\', '/');
            if (assets.EndsWith("/Assets"))
                return assets.Substring(0, assets.Length - "/Assets".Length);
            return Directory.GetParent(Application.dataPath).FullName;
        }

        /// <summary>
        /// Ensures the output folder exists; throws if the path is empty.
        /// </summary>
        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new Exception("Output path is empty.");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        /// <summary>
        /// Draws a dropdown-like button for WebGL compression.
        /// Reads current state best-effort and offers a context menu to set a desired mode.
        /// </summary>
        private void DrawCompressionDropdown()
        {
            ReadWebGLCompression(out _compressionState);

            UI.CompressionButton.text = CompressionLabel(_compressionState);

            if (GUILayout.Button(UI.CompressionButton, GUILayout.Width(160)))
            {
                var menu = new GenericMenu();

                AddCompressionItem(menu, "Unknown (no API)", WebGLCompressionState.Unknown, false);
                AddCompressionItem(menu, "Disabled", WebGLCompressionState.Disabled, true);
                AddCompressionItem(menu, "Brotli", WebGLCompressionState.Enabled_Brotli, true);
                AddCompressionItem(menu, "Gzip", WebGLCompressionState.Enabled_Gzip, true);

                menu.ShowAsContext();
            }
        }

        /// <summary>
        /// Adds an item to the compression menu.
        /// If the item is not selectable, it only reports a status message.
        /// </summary>
        private void AddCompressionItem(GenericMenu menu, string title, WebGLCompressionState state, bool selectable)
        {
            bool on = _compressionState == state;
            menu.AddItem(new GUIContent(title, "Set WebGL compression format (best-effort via reflection)."), on, () =>
            {
                if (!selectable)
                {
                    _status = "Compression API not available in this Unity version.";
                    return;
                }

                bool ok = TrySetWebGLCompression(state);
                _status = ok ? $"Compression set to {title} (best-effort)." : "Failed to set compression (API not available).";
            });
        }

        /// <summary>
        /// Converts compression state to a short label for the dropdown button.
        /// </summary>
        private static string CompressionLabel(WebGLCompressionState s)
        {
            switch (s)
            {
                case WebGLCompressionState.Disabled: return "Disabled";
                case WebGLCompressionState.Enabled_Brotli: return "Brotli";
                case WebGLCompressionState.Enabled_Gzip: return "Gzip";
                case WebGLCompressionState.Enabled_Other: return "Enabled (Other)";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Reads WebGL compression setting via reflection (best-effort).
        /// If the API surface does not exist, state stays Unknown.
        /// </summary>
        private static void ReadWebGLCompression(out WebGLCompressionState state)
        {
            state = WebGLCompressionState.Unknown;

            try
            {
                var webglType = typeof(PlayerSettings).GetNestedType("WebGL", BindingFlags.Public);
                if (webglType == null) return;

                var prop = webglType.GetProperty("compressionFormat", BindingFlags.Public | BindingFlags.Static);
                if (prop == null) return;

                object v = prop.GetValue(null, null);
                if (v == null) return;

                string s = v.ToString().ToLowerInvariant();

                if (s.Contains("disabled") || s.Contains("none")) state = WebGLCompressionState.Disabled;
                else if (s.Contains("brotli")) state = WebGLCompressionState.Enabled_Brotli;
                else if (s.Contains("gzip")) state = WebGLCompressionState.Enabled_Gzip;
                else state = WebGLCompressionState.Enabled_Other;
            }
            catch
            {
                state = WebGLCompressionState.Unknown;
            }
        }

        /// <summary>
        /// Attempts to set WebGL compression via reflection (best-effort).
        /// Returns false if the API is not available or the enum cannot be mapped.
        /// </summary>
        private static bool TrySetWebGLCompression(WebGLCompressionState desired)
        {
            try
            {
                var webglType = typeof(PlayerSettings).GetNestedType("WebGL", BindingFlags.Public);
                if (webglType == null) return false;

                var prop = webglType.GetProperty("compressionFormat", BindingFlags.Public | BindingFlags.Static);
                if (prop == null || !prop.CanWrite) return false;

                Type enumType = prop.PropertyType;
                if (!enumType.IsEnum) return false;

                object value = null;

                var names = Enum.GetNames(enumType);
                for (int i = 0; i < names.Length; i++)
                {
                    string n = names[i].ToLowerInvariant();

                    if (desired == WebGLCompressionState.Disabled && (n.Contains("disabled") || n.Contains("none")))
                        value = Enum.Parse(enumType, names[i]);
                    else if (desired == WebGLCompressionState.Enabled_Brotli && n.Contains("brotli"))
                        value = Enum.Parse(enumType, names[i]);
                    else if (desired == WebGLCompressionState.Enabled_Gzip && n.Contains("gzip"))
                        value = Enum.Parse(enumType, names[i]);

                    if (value != null) break;
                }

                if (value == null) return false;

                prop.SetValue(null, value, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Invokes the build/analyze pipeline without hard-binding to a specific signature.
        /// Tries:
        /// 1) BuildAnalyzer.BuildAndAnalyze(string outputPath)
        /// 2) BuildAnalyzer.BuildAndAnalyze()
        /// </summary>
        private static void InvokeBuildAnalyzer(string outputPath)
        {
            try
            {
                var asm = typeof(BuildSettingsTab).Assembly;
                Type t = asm.GetType("Playgama.Suit.BuildAnalyzer") ?? asm.GetType("Playgama.Suit.Utils.BuildAnalyzer");
                if (t == null)
                {
                    UnityEngine.Debug.LogError("Suit: BuildAnalyzer type not found.");
                    return;
                }

                MethodInfo m1 = t.GetMethod("BuildAndAnalyze", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (m1 != null)
                {
                    m1.Invoke(null, new object[] { outputPath });
                    return;
                }

                MethodInfo m0 = t.GetMethod("BuildAndAnalyze", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (m0 != null)
                {
                    m0.Invoke(null, null);
                    return;
                }

                UnityEngine.Debug.LogError("Suit: BuildAnalyzer.BuildAndAnalyze method not found.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }
}
