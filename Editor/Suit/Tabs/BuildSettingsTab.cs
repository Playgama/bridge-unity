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
        private CodeOptimizationState _codeOptimizationState;

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
        /// Code optimization setting for IL2CPP code generation.
        /// Controls the tradeoff between build size, build time, and runtime performance.
        /// Maps to Unity's WebGLCodeOptimization / Il2CppCodeGeneration enums.
        /// </summary>
        private enum CodeOptimizationState
        {
            Unknown,
            None,               // No optimization - fastest build time
            Size,               // Optimize for size - smallest build
            Speed,              // Optimize for runtime speed - fastest execution
            ShorterBuildTime,   // Faster incremental builds (Unity 2022+)
            RuntimeSpeedLTO,    // Link Time Optimization for max speed (Unity 2022+)
            DiskSize,           // Optimize for disk size (Unity 2022+)
            DiskSizeLTO         // Disk size with Link Time Optimization (Unity 2022+)
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

            public static readonly GUIContent CodeOptimizationLabel = new GUIContent(
                "Code Optimization",
                "IL2CPP code generation optimization mode.\n" +
                "Controls the tradeoff between build size, build time, and runtime performance.");

            public static readonly GUIContent CodeOptimizationButton = new GUIContent(
                "Unknown",
                "Current code optimization setting.\n" +
                "Click to choose optimization mode.");

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
            ReadCodeOptimization(out _codeOptimizationState);

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

            // First row: Development Build toggle
            using (new EditorGUILayout.HorizontalScope())
            {
                bool newDev = EditorGUILayout.ToggleLeft(UI.DevelopmentBuild, _devBuild, GUILayout.Width(160));
                if (newDev != _devBuild)
                {
                    _devBuild = newDev;
                    EditorUserBuildSettings.development = _devBuild;
                    _status = "Development Build updated.";
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6);

            // Second row: Compression and Code Optimization dropdowns
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(UI.CompressionLabel, GUILayout.Width(90));
                DrawCompressionDropdown();

                GUILayout.Space(20);

                GUILayout.Label(UI.CodeOptimizationLabel, GUILayout.Width(110));
                DrawCodeOptimizationDropdown();

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(4);
            EditorGUILayout.LabelField("Development Build OFF + Compression ON + 'Disk Size with LTO' for smallest build.", SuitStyles.SubtitleStyle);
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

            // Check if we have actual build data (not just a status message from "Analyzing...")
            bool hasBuildData = _buildInfo != null && (_buildInfo.TotalBuildSizeBytes > 0 || _buildInfo.HasData);

            if (!hasBuildData)
            {
                // Check if build is in progress
                if (_buildInfo != null && !string.IsNullOrEmpty(_buildInfo.StatusMessage) &&
                    _buildInfo.StatusMessage.Contains("Analyzing"))
                {
                    EditorGUILayout.LabelField("Build in progress...", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(_buildInfo.StatusMessage, SuitStyles.SubtitleStyle);
                }
                else
                {
                    EditorGUILayout.LabelField(UI.NoSnapshot.text, SuitStyles.SubtitleStyle);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Click 'Build & Analyze' to create a snapshot.", EditorStyles.miniLabel);
                }
                SuitStyles.EndCard();
                return;
            }

            // Show build result with color
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent("Build Result", "Whether the last build step reported success."), GUILayout.Width(130));
                GUILayout.FlexibleSpace();

                Color prevColor = GUI.color;
                GUI.color = _buildInfo.BuildSucceeded ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.4f);
                GUILayout.Label(_buildInfo.BuildSucceeded ? "Success" : "Failed", EditorStyles.boldLabel);
                GUI.color = prevColor;
            }

            DrawLabelValue("Target", string.IsNullOrEmpty(_buildInfo.BuildTargetName) ? "—" : _buildInfo.BuildTargetName);
            DrawLabelValue("Total Build Size", SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes));

            if (_buildInfo.BuildTime.TotalSeconds > 0)
            {
                string timeStr = _buildInfo.BuildTime.TotalMinutes >= 1
                    ? $"{(int)_buildInfo.BuildTime.TotalMinutes}m {_buildInfo.BuildTime.Seconds}s"
                    : $"{_buildInfo.BuildTime.TotalSeconds:0.0}s";
                DrawLabelValue("Build Time", timeStr);
            }

            DrawLabelValue("Analysis Mode", _buildInfo.DataMode.ToString());
            DrawLabelValue("Tracked Assets", _buildInfo.TrackedAssetCount.ToString());

            string tracked = SharedTypes.FormatBytes(_buildInfo.TrackedBytes);
            if (_buildInfo.DataMode == BuildDataMode.DependenciesFallback) tracked += " (estimated)";
            DrawLabelValue("Tracked Bytes", tracked);

            GUILayout.Space(4);

            if (GUILayout.Button(UI.CopySnapshot, GUILayout.Width(220)))
            {
                string txt =
                    "Playgama Suit Build Snapshot\n" +
                    "-------------------\n" +
                    $"Build Result: {(_buildInfo.BuildSucceeded ? "Success" : "Failed")}\n" +
                    $"Target: {_buildInfo.BuildTargetName}\n" +
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
        /// Helper to draw a label-value pair consistently.
        /// </summary>
        private static void DrawLabelValue(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(130));
                GUILayout.FlexibleSpace();
                GUILayout.Label(value);
            }
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
        /// Draws a dropdown-like button for Code Optimization setting.
        /// Reads current state best-effort and offers a context menu to set a desired mode.
        /// </summary>
        private void DrawCodeOptimizationDropdown()
        {
            ReadCodeOptimization(out _codeOptimizationState);

            // Default to Disk Size with LTO if Unknown (best for WebGL size optimization)
            if (_codeOptimizationState == CodeOptimizationState.Unknown)
            {
                _codeOptimizationState = CodeOptimizationState.DiskSizeLTO;
            }

            UI.CodeOptimizationButton.text = CodeOptimizationLabel(_codeOptimizationState);

            if (GUILayout.Button(UI.CodeOptimizationButton, GUILayout.Width(160)))
            {
                var menu = new GenericMenu();

                // Size optimization options
                AddCodeOptimizationItem(menu, "Disk Size with LTO", CodeOptimizationState.DiskSizeLTO,
                    "Disk size optimization with Link Time Optimization. Smallest build, longer build time.");

                AddCodeOptimizationItem(menu, "Disk Size", CodeOptimizationState.DiskSize,
                    "Optimize for disk size. Reduces file size on disk.");

                menu.AddSeparator("");

                // Speed optimization options
                AddCodeOptimizationItem(menu, "Runtime Speed with LTO", CodeOptimizationState.RuntimeSpeedLTO,
                    "Maximum runtime speed with Link Time Optimization. Longest build time.");

                AddCodeOptimizationItem(menu, "Speed", CodeOptimizationState.Speed,
                    "Faster runtime performance. Larger build size.");

                menu.AddSeparator("");

                // Build time options
                AddCodeOptimizationItem(menu, "Shorter Build Time", CodeOptimizationState.ShorterBuildTime,
                    "Faster incremental builds. Less optimized code.");

                AddCodeOptimizationItem(menu, "None", CodeOptimizationState.None,
                    "No optimization. Fastest build, largest size, slowest runtime.");

                menu.ShowAsContext();
            }
        }

        /// <summary>
        /// Adds an item to the code optimization menu.
        /// </summary>
        private void AddCodeOptimizationItem(GenericMenu menu, string title, CodeOptimizationState state, string tooltip)
        {
            bool on = _codeOptimizationState == state;
            menu.AddItem(new GUIContent(title, tooltip), on, () =>
            {
                bool ok = TrySetCodeOptimization(state);
                if (ok)
                {
                    _codeOptimizationState = state;
                    _status = $"Code optimization set to '{title}'.";
                }
                else
                {
                    _status = $"Failed to set '{title}' - option may not be available in this Unity version.";
                }
            });
        }

        /// <summary>
        /// Converts code optimization state to a short label for the dropdown button.
        /// </summary>
        private static string CodeOptimizationLabel(CodeOptimizationState s)
        {
            switch (s)
            {
                case CodeOptimizationState.Size: return "Size";
                case CodeOptimizationState.Speed: return "Speed";
                case CodeOptimizationState.None: return "None";
                case CodeOptimizationState.ShorterBuildTime: return "Shorter Build Time";
                case CodeOptimizationState.RuntimeSpeedLTO: return "Runtime Speed (LTO)";
                case CodeOptimizationState.DiskSize: return "Disk Size";
                case CodeOptimizationState.DiskSizeLTO: return "Disk Size (LTO)";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Gets the raw Code Optimization value from Unity for debugging purposes.
        /// </summary>
        private static string GetRawCodeOptimizationValue()
        {
            try
            {
                // Try GetPlatformSettings first
                var getPlatformSettings = typeof(EditorUserBuildSettings).GetMethod(
                    "GetPlatformSettings",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null);

                if (getPlatformSettings != null)
                {
                    object result = getPlatformSettings.Invoke(null, new object[] { "WebGL", "CodeOptimization" });
                    if (result != null && !string.IsNullOrEmpty(result.ToString()))
                    {
                        return result.ToString();
                    }
                }

                // Try il2CppCodeGeneration
                var il2cppProp = typeof(EditorUserBuildSettings).GetProperty("il2CppCodeGeneration", BindingFlags.Public | BindingFlags.Static);
                if (il2cppProp != null)
                {
                    object v = il2cppProp.GetValue(null, null);
                    if (v != null) return v.ToString();
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Reads Code Optimization setting via reflection (best-effort).
        /// Tries multiple API locations across Unity versions.
        /// </summary>
        private static void ReadCodeOptimization(out CodeOptimizationState state)
        {
            state = CodeOptimizationState.Unknown;

            try
            {
                // Method 1: EditorUserBuildSettings.GetPlatformSettings (Unity 2022+ Build Profiles)
                // This is the primary API used by Build Profiles for Code Optimization
                var getPlatformSettings = typeof(EditorUserBuildSettings).GetMethod(
                    "GetPlatformSettings",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null);

                if (getPlatformSettings != null)
                {
                    // Try with "WebGL" as platform name
                    object result = getPlatformSettings.Invoke(null, new object[] { "WebGL", "CodeOptimization" });
                    if (result != null && !string.IsNullOrEmpty(result.ToString()))
                    {
                        state = ParseCodeOptimizationValue(result.ToString());
                        if (state != CodeOptimizationState.Unknown) return;
                    }

                    // Try with BuildTargetGroup name
                    result = getPlatformSettings.Invoke(null, new object[] { BuildTargetGroup.WebGL.ToString(), "CodeOptimization" });
                    if (result != null && !string.IsNullOrEmpty(result.ToString()))
                    {
                        state = ParseCodeOptimizationValue(result.ToString());
                        if (state != CodeOptimizationState.Unknown) return;
                    }
                }

                // Method 2: PlayerSettings.GetIl2CppCodeGeneration (Unity 2022+)
                var getIl2CppCodeGen = typeof(PlayerSettings).GetMethod(
                    "GetIl2CppCodeGeneration",
                    BindingFlags.Public | BindingFlags.Static);

                if (getIl2CppCodeGen != null)
                {
                    // Try to get NamedBuildTarget.WebGL via reflection (safe for older Unity versions)
                    var namedBuildTargetType = Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
                    if (namedBuildTargetType != null)
                    {
                        var webglTarget = namedBuildTargetType.GetProperty("WebGL", BindingFlags.Public | BindingFlags.Static);
                        if (webglTarget != null)
                        {
                            object target = webglTarget.GetValue(null);
                            object result = getIl2CppCodeGen.Invoke(null, new[] { target });
                            if (result != null)
                            {
                                state = ParseCodeOptimizationValue(result.ToString());
                                if (state != CodeOptimizationState.Unknown) return;
                            }
                        }
                    }
                }

                // Method 3: EditorUserBuildSettings.il2CppCodeGeneration (legacy)
                var il2cppProp = typeof(EditorUserBuildSettings).GetProperty("il2CppCodeGeneration", BindingFlags.Public | BindingFlags.Static);
                if (il2cppProp != null)
                {
                    object v = il2cppProp.GetValue(null, null);
                    if (v != null)
                    {
                        state = ParseCodeOptimizationValue(v.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Playgama Suit] Failed to read Code Optimization: {ex.Message}");
                state = CodeOptimizationState.Unknown;
            }
        }

        /// <summary>
        /// Parses a string value from Unity's code optimization enums or platform settings into our state enum.
        /// Handles both enum names (OptimizeSize) and setting values (diskSizeLto).
        /// </summary>
        private static CodeOptimizationState ParseCodeOptimizationValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return CodeOptimizationState.Unknown;

            string s = value.ToLowerInvariant();

            // Exact matches first (platform settings values)
            if (s == "disksizelto") return CodeOptimizationState.DiskSizeLTO;
            if (s == "runtimespeedlto") return CodeOptimizationState.RuntimeSpeedLTO;
            if (s == "disksize") return CodeOptimizationState.DiskSize;
            if (s == "shorterbuildtime") return CodeOptimizationState.ShorterBuildTime;
            if (s == "size") return CodeOptimizationState.Size;
            if (s == "speed") return CodeOptimizationState.Speed;
            if (s == "none") return CodeOptimizationState.None;

            // Check for Disk Size with LTO (pattern matching for enum names)
            if ((s.Contains("disk") && s.Contains("lto")) || s.Contains("disksizewithlto"))
                return CodeOptimizationState.DiskSizeLTO;

            // Check for Runtime Speed with LTO
            if ((s.Contains("speed") && s.Contains("lto")) || s.Contains("runtimespeedwithlto"))
                return CodeOptimizationState.RuntimeSpeedLTO;

            // Check for Disk Size (without LTO)
            if (s.Contains("disksize") || (s.Contains("disk") && s.Contains("size") && !s.Contains("lto")))
                return CodeOptimizationState.DiskSize;

            // Check for shorter build time / faster build
            if (s.Contains("shorterbuildtime") || s.Contains("fasterwithoutlto") || s.Contains("fasterbuilds"))
                return CodeOptimizationState.ShorterBuildTime;

            // Standard options - check for enum names like "OptimizeSize", "OptimizeSpeed"
            if (s.Contains("optimizesize") || s.Contains("optforsize") || (s.Contains("size") && !s.Contains("disk")))
                return CodeOptimizationState.Size;

            if (s.Contains("optimizespeed") || s.Contains("optforspeed") || (s.Contains("speed") && !s.Contains("lto")))
                return CodeOptimizationState.Speed;

            if (s.Contains("disabled"))
                return CodeOptimizationState.None;

            return CodeOptimizationState.Unknown;
        }

        /// <summary>
        /// Attempts to set Code Optimization via reflection (best-effort).
        /// Tries multiple API locations across Unity versions.
        /// Returns false if the API is not available or the enum cannot be mapped.
        /// </summary>
        private static bool TrySetCodeOptimization(CodeOptimizationState desired)
        {
            try
            {
                bool anySuccess = false;

                // Get the string value for the desired state (used by SetPlatformSettings)
                string settingValue = GetCodeOptimizationSettingValue(desired);

                // Method 1: EditorUserBuildSettings.SetPlatformSettings (Unity 2022+ Build Profiles)
                // This is the primary API used by Build Profiles for Code Optimization
                var setPlatformSettings = typeof(EditorUserBuildSettings).GetMethod(
                    "SetPlatformSettings",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string), typeof(string) },
                    null);

                if (setPlatformSettings != null && !string.IsNullOrEmpty(settingValue))
                {
                    // Set for WebGL platform
                    setPlatformSettings.Invoke(null, new object[] { "WebGL", "CodeOptimization", settingValue });
                    anySuccess = true;
                }

                // Method 2: PlayerSettings.SetIl2CppCodeGeneration (Unity 2022+)
                var setIl2CppCodeGen = typeof(PlayerSettings).GetMethod(
                    "SetIl2CppCodeGeneration",
                    BindingFlags.Public | BindingFlags.Static);

                if (setIl2CppCodeGen != null)
                {
                    // Try to get NamedBuildTarget.WebGL via reflection (safe for older Unity versions)
                    var namedBuildTargetType = Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
                    if (namedBuildTargetType != null)
                    {
                        var webglTarget = namedBuildTargetType.GetProperty("WebGL", BindingFlags.Public | BindingFlags.Static);

                        if (webglTarget != null)
                        {
                            object target = webglTarget.GetValue(null);

                            // Find the Il2CppCodeGeneration enum type via reflection
                            var il2cppEnumType = Type.GetType("UnityEditor.Build.Il2CppCodeGeneration, UnityEditor");
                            if (il2cppEnumType != null)
                            {
                                object enumValue = FindEnumValue(il2cppEnumType, desired);

                                if (enumValue != null)
                                {
                                    setIl2CppCodeGen.Invoke(null, new[] { target, enumValue });
                                    anySuccess = true;
                                }
                            }
                        }
                    }
                }

                // Method 3: EditorUserBuildSettings.il2CppCodeGeneration (legacy)
                var il2cppProp = typeof(EditorUserBuildSettings).GetProperty("il2CppCodeGeneration", BindingFlags.Public | BindingFlags.Static);
                if (il2cppProp != null && il2cppProp.CanWrite)
                {
                    Type enumType = il2cppProp.PropertyType;
                    if (enumType.IsEnum)
                    {
                        object value = FindEnumValue(enumType, desired);
                        if (value != null)
                        {
                            il2cppProp.SetValue(null, value, null);
                            anySuccess = true;
                        }
                    }
                }

                return anySuccess;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Playgama Suit] Failed to set Code Optimization: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Converts our CodeOptimizationState to the string value used by SetPlatformSettings.
        /// These values match what Unity's Build Profiles use internally.
        /// </summary>
        private static string GetCodeOptimizationSettingValue(CodeOptimizationState state)
        {
            switch (state)
            {
                case CodeOptimizationState.Size: return "size";
                case CodeOptimizationState.Speed: return "speed";
                case CodeOptimizationState.None: return "none";
                case CodeOptimizationState.ShorterBuildTime: return "shorterBuildTime";
                case CodeOptimizationState.RuntimeSpeedLTO: return "runtimeSpeedLto";
                case CodeOptimizationState.DiskSize: return "diskSize";
                case CodeOptimizationState.DiskSizeLTO: return "diskSizeLto";
                default: return null;
            }
        }

        /// <summary>
        /// Finds an enum value that matches the desired code optimization state.
        /// Handles multiple naming conventions across Unity versions.
        /// </summary>
        private static object FindEnumValue(Type enumType, CodeOptimizationState desired)
        {
            var names = Enum.GetNames(enumType);

            // For LTO variants, we need to check for combined patterns first
            if (desired == CodeOptimizationState.DiskSizeLTO)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    string n = names[i].ToLowerInvariant();
                    if ((n.Contains("disk") && n.Contains("lto")) || n.Contains("disksizewithlto") || n.Contains("disksizelto"))
                        return Enum.Parse(enumType, names[i]);
                }
                return null;
            }

            if (desired == CodeOptimizationState.RuntimeSpeedLTO)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    string n = names[i].ToLowerInvariant();
                    if ((n.Contains("speed") && n.Contains("lto")) || n.Contains("runtimespeedwithlto") || n.Contains("runtimespeedlto"))
                        return Enum.Parse(enumType, names[i]);
                }
                return null;
            }

            if (desired == CodeOptimizationState.DiskSize)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    string n = names[i].ToLowerInvariant();
                    // Match disk size but NOT disk size with LTO
                    if ((n.Contains("disksize") || (n.Contains("disk") && n.Contains("size"))) && !n.Contains("lto"))
                        return Enum.Parse(enumType, names[i]);
                }
                return null;
            }

            // Build search patterns based on desired state
            string[] patterns;
            string[] excludePatterns = null;

            switch (desired)
            {
                case CodeOptimizationState.Size:
                    patterns = new[] { "size", "optforsize", "optimizesize" };
                    excludePatterns = new[] { "disk", "lto" }; // Exclude disk size variants
                    break;
                case CodeOptimizationState.Speed:
                    patterns = new[] { "speed", "runtime", "optforspeed", "optimizespeed" };
                    excludePatterns = new[] { "lto" }; // Exclude LTO variants
                    break;
                case CodeOptimizationState.None:
                    patterns = new[] { "none", "disabled", "off" };
                    break;
                case CodeOptimizationState.ShorterBuildTime:
                    patterns = new[] { "shorterbuildtime", "fasterwithoutlto", "fasterbuilds", "fasterbuild" };
                    break;
                default:
                    return null;
            }

            // Search for matching enum value
            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i].ToLowerInvariant();

                // Check exclusion patterns first
                if (excludePatterns != null)
                {
                    bool excluded = false;
                    foreach (var ex in excludePatterns)
                    {
                        if (n.Contains(ex))
                        {
                            excluded = true;
                            break;
                        }
                    }
                    if (excluded) continue;
                }

                foreach (var pattern in patterns)
                {
                    if (n.Contains(pattern))
                        return Enum.Parse(enumType, names[i]);
                }
            }

            return null;
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
