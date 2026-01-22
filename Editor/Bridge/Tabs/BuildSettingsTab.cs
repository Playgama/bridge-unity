using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
{
    public sealed class BuildSettingsTab : ITab
    {
        public string TabName { get { return "Build Settings"; } }

        private const string Pref_OutputPath = "BRIDGE_BUILD_OUTPUT_PATH";

        private BuildInfo _buildInfo;

        private Vector2 _scroll;
        private string _status = "";

        private bool _foldHeader = false;
        private bool _foldOutput = true;
        private bool _foldScenes = true;
        private bool _foldWebGL = true;
        private bool _foldBuild = true;
        private bool _foldLastBuild = true;

        private string _outputPath;
        private bool _devBuild;
        private bool _nameFilesAsHashes;
        private WebGLCompressionState _compressionState;
        private CodeOptimizationState _codeOptimizationState;

        private enum WebGLCompressionState
        {
            Unknown,
            Disabled,
            Enabled_Brotli,
            Enabled_Gzip,
            Enabled_Other
        }

        internal enum CodeOptimizationState
        {
            Unknown,
            None,
            Size,
            Speed,
            ShorterBuildTime,
            RuntimeSpeedLTO,
            DiskSize,
            DiskSizeLTO
        }

        private static class UI
        {
            public static readonly GUIContent HeaderInfo = new GUIContent(
                "Build Settings",
                "Workflow hub for build-size related settings and the Build & Analyze entry point.");

            public static readonly GUIContent HeaderHelp = new GUIContent(
                "This tab is focused on build size workflow: selecting scenes, WebGL build-size toggles, and a single Build & Analyze entry point.\n" +
                "Build is always triggered via EditorApplication.delayCall (never inside OnGUI).",
                "General notes about how Playgama Bridge triggers builds and why it avoids running build logic inside OnGUI.");

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
                "Playgama Bridge uses only enabled scenes when triggering a build.");

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
                "Only enabled scenes are included when Playgama Bridge triggers the build.");

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

            public static readonly GUIContent NameFilesAsHashes = new GUIContent(
                "Name Files As Hashes",
                "If enabled: output files use content hashes as filenames instead of human-readable names.\n" +
                "This improves browser caching when content changes, as only modified files get new names.");

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
                "Trigger a WebGL build and run Playgama Bridge analysis.\n" +
                "The build is scheduled via delayCall to keep IMGUI safe and responsive.");

            public static readonly GUIContent BuildAnalyzeButton = new GUIContent(
                "Build & Analyze (WebGL)",
                "Starts a WebGL build to the selected output path, then runs Playgama Bridge analysis.\n" +
                "Only enabled Build Settings scenes are included.\n" +
                "Note: does not silently switch Build Target.");

            public static readonly GUIContent OpenOutput = new GUIContent(
                "Open Output Folder",
                "Reveal the build output folder in your file explorer.");

            public static readonly GUIContent BuildAnalyzeHelp = new GUIContent(
                "Build runs with DetailedBuildReport. Analysis mode is chosen automatically:\n" +
                "• Packed Assets (if BuildReport provides usable mapping)\n" +
                "• Dependencies Fallback (guaranteed)\n",
                "Playgama Bridge chooses the strongest analysis mode available for the current Unity build pipeline output.");

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
            _nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes;

            ReadWebGLCompression(out _compressionState);
            ReadCodeOptimization(out _codeOptimizationState);

            _status = "";
        }

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

        private void DrawHeader()
        {
            _foldHeader = BridgeStyles.DrawSectionHeader("About Build Settings", _foldHeader, "\u2139");
            if (_foldHeader)
            {
                BridgeStyles.BeginCard();
                EditorGUILayout.LabelField("Build-size workflow: scenes, WebGL toggles, Build & Analyze.", BridgeStyles.SubtitleStyle);
                BridgeStyles.EndCard();
            }
        }

        private void DrawOutputBlock()
        {
            _foldOutput = BridgeStyles.DrawSectionHeader("Output Path", _foldOutput, "\u2301");
            if (!_foldOutput) return;

            BridgeStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(UI.OutputFolder, GUILayout.Width(50));
                _outputPath = EditorGUILayout.TextField(_outputPath);

                if (GUILayout.Button(UI.Browse, GUILayout.Width(80)))
                {
                    string chosen = EditorUtility.SaveFolderPanel("Playgama Bridge - Choose Build Output Folder", _outputPath, "");
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

            EditorGUILayout.LabelField("Tip: keep build output outside Assets/ to avoid accidental imports.", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();
        }

        private void DrawScenesBlock()
        {
            int sceneCount = EditorBuildSettings.scenes?.Length ?? 0;
            _foldScenes = BridgeStyles.DrawSectionHeader($"Scenes ({sceneCount} in Build Settings)", _foldScenes, "\u2302");
            if (!_foldScenes) return;

            BridgeStyles.BeginCard();
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

            EditorGUILayout.LabelField("Playgama Bridge uses enabled scenes from Build Settings for the build.", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();
        }

        private void DrawWebGLBuildSizeBlock()
        {
            _foldWebGL = BridgeStyles.DrawSectionHeader("WebGL Build Size Toggles", _foldWebGL, "\u2699");
            if (!_foldWebGL) return;

            BridgeStyles.BeginCard();

            using (new EditorGUILayout.HorizontalScope())
            {
                bool newDev = EditorGUILayout.ToggleLeft(UI.DevelopmentBuild, _devBuild, GUILayout.Width(160));
                if (newDev != _devBuild)
                {
                    _devBuild = newDev;
                    EditorUserBuildSettings.development = _devBuild;
                    _status = "Development Build updated.";
                }

                GUILayout.Space(20);

                bool newNameFilesAsHashes = EditorGUILayout.ToggleLeft(UI.NameFilesAsHashes, _nameFilesAsHashes, GUILayout.Width(160));
                if (newNameFilesAsHashes != _nameFilesAsHashes)
                {
                    _nameFilesAsHashes = newNameFilesAsHashes;
                    PlayerSettings.WebGL.nameFilesAsHashes = _nameFilesAsHashes;
                    _status = "Name Files As Hashes updated.";
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6);

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
            EditorGUILayout.LabelField("Development Build OFF + Compression ON + 'Disk Size with LTO' for smallest build. Name Files As Hashes improves caching.", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();
        }

        private void DrawBuildAndAnalyzeBlock()
        {
            _foldBuild = BridgeStyles.DrawSectionHeader("Build & Analyze", _foldBuild, "\u26A1");
            if (!_foldBuild) return;

            BridgeStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = HasEnabledScenes();

                if (BridgeStyles.DrawAccentButton(UI.BuildAnalyzeButton, GUILayout.Height(32)))
                {
                    // Build must never run inside OnGUI; delayCall is safe for long operations
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            EnsureDirectory(_outputPath);

                            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                            {
                                UnityEngine.Debug.LogWarning("Bridge: Active build target is not WebGL. Build size for WebGL may be invalid unless you switch target.");
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

            EditorGUILayout.LabelField("Uses 'Shorter Build Time' optimization for faster analysis.", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Build for Release", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.LabelField(
                "Create the smallest possible build for publishing. Uses 'Disk Size with LTO' optimization.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = HasEnabledScenes();

                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);

                if (GUILayout.Button(new GUIContent("  Build for Release  ", "Creates smallest possible WebGL build. Takes longer to compile."), GUILayout.Height(32)))
                {
                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            EnsureDirectory(_outputPath);
                            BuildAnalyzer.BuildForRelease();
                            _status = "Release build started (this may take a while)...";
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogException(ex);
                            _status = "Build failed to start: " + ex.Message;
                        }
                    };
                }

                GUI.backgroundColor = oldBg;
                GUI.enabled = true;

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "Release builds use Link Time Optimization (LTO) for smallest file size.\nThis takes significantly longer than a regular build.",
                MessageType.Info);
            BridgeStyles.EndCard();
        }

        private void DrawLastBuildBlock()
        {
            _foldLastBuild = BridgeStyles.DrawSectionHeader("Last Build Snapshot", _foldLastBuild, "\u2139");
            if (!_foldLastBuild) return;

            BridgeStyles.BeginCard();

            bool hasBuildData = _buildInfo != null && (_buildInfo.TotalBuildSizeBytes > 0 || _buildInfo.HasData);

            if (!hasBuildData)
            {
                if (_buildInfo != null && !string.IsNullOrEmpty(_buildInfo.StatusMessage) &&
                    _buildInfo.StatusMessage.Contains("Analyzing"))
                {
                    EditorGUILayout.LabelField("Build in progress...", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(_buildInfo.StatusMessage, BridgeStyles.SubtitleStyle);
                }
                else
                {
                    EditorGUILayout.LabelField(UI.NoSnapshot.text, BridgeStyles.SubtitleStyle);
                    EditorGUILayout.Space(4);
                    EditorGUILayout.LabelField("Click 'Build & Analyze' to create a snapshot.", EditorStyles.miniLabel);
                }
                BridgeStyles.EndCard();
                return;
            }

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
                    "Playgama Bridge Build Snapshot\n" +
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
            BridgeStyles.EndCard();
        }

        private static void DrawLabelValue(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(130));
                GUILayout.FlexibleSpace();
                GUILayout.Label(value);
            }
        }

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

        private static string GetProjectRoot()
        {
            var assets = Application.dataPath.Replace('\\', '/');
            if (assets.EndsWith("/Assets"))
                return assets.Substring(0, assets.Length - "/Assets".Length);
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new Exception("Output path is empty.");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

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

        private void DrawCodeOptimizationDropdown()
        {
            ReadCodeOptimization(out _codeOptimizationState);

            if (_codeOptimizationState == CodeOptimizationState.Unknown)
            {
                _codeOptimizationState = CodeOptimizationState.DiskSizeLTO;
            }

            UI.CodeOptimizationButton.text = CodeOptimizationLabel(_codeOptimizationState);

            if (GUILayout.Button(UI.CodeOptimizationButton, GUILayout.Width(160)))
            {
                var menu = new GenericMenu();

                AddCodeOptimizationItem(menu, "Disk Size with LTO", CodeOptimizationState.DiskSizeLTO,
                    "Disk size optimization with Link Time Optimization. Smallest build, longer build time.");

                AddCodeOptimizationItem(menu, "Disk Size", CodeOptimizationState.DiskSize,
                    "Optimize for disk size. Reduces file size on disk.");

                menu.AddSeparator("");

                AddCodeOptimizationItem(menu, "Runtime Speed with LTO", CodeOptimizationState.RuntimeSpeedLTO,
                    "Maximum runtime speed with Link Time Optimization. Longest build time.");

                AddCodeOptimizationItem(menu, "Speed", CodeOptimizationState.Speed,
                    "Faster runtime performance. Larger build size.");

                menu.AddSeparator("");

                AddCodeOptimizationItem(menu, "Shorter Build Time", CodeOptimizationState.ShorterBuildTime,
                    "Faster incremental builds. Less optimized code.");

                AddCodeOptimizationItem(menu, "None", CodeOptimizationState.None,
                    "No optimization. Fastest build, largest size, slowest runtime.");

                menu.ShowAsContext();
            }
        }

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

        private static string GetRawCodeOptimizationValue()
        {
            try
            {
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

        private static void ReadCodeOptimization(out CodeOptimizationState state)
        {
            state = CodeOptimizationState.Unknown;

            try
            {
                // Try EditorUserBuildSettings.GetPlatformSettings (Unity 2022+ Build Profiles)
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
                        state = ParseCodeOptimizationValue(result.ToString());
                        if (state != CodeOptimizationState.Unknown) return;
                    }

                    result = getPlatformSettings.Invoke(null, new object[] { BuildTargetGroup.WebGL.ToString(), "CodeOptimization" });
                    if (result != null && !string.IsNullOrEmpty(result.ToString()))
                    {
                        state = ParseCodeOptimizationValue(result.ToString());
                        if (state != CodeOptimizationState.Unknown) return;
                    }
                }

                // Try PlayerSettings.GetIl2CppCodeGeneration (Unity 2022+)
                var getIl2CppCodeGen = typeof(PlayerSettings).GetMethod(
                    "GetIl2CppCodeGeneration",
                    BindingFlags.Public | BindingFlags.Static);

                if (getIl2CppCodeGen != null)
                {
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

                // Try EditorUserBuildSettings.il2CppCodeGeneration (legacy)
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
                UnityEngine.Debug.LogWarning($"[Playgama Bridge] Failed to read Code Optimization: {ex.Message}");
                state = CodeOptimizationState.Unknown;
            }
        }

        private static CodeOptimizationState ParseCodeOptimizationValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return CodeOptimizationState.Unknown;

            string s = value.ToLowerInvariant();

            // Exact matches first
            if (s == "disksizelto") return CodeOptimizationState.DiskSizeLTO;
            if (s == "runtimespeedlto") return CodeOptimizationState.RuntimeSpeedLTO;
            if (s == "disksize") return CodeOptimizationState.DiskSize;
            if (s == "shorterbuildtime") return CodeOptimizationState.ShorterBuildTime;
            if (s == "size") return CodeOptimizationState.Size;
            if (s == "speed") return CodeOptimizationState.Speed;
            if (s == "none") return CodeOptimizationState.None;

            // Pattern matching for enum names
            if ((s.Contains("disk") && s.Contains("lto")) || s.Contains("disksizewithlto"))
                return CodeOptimizationState.DiskSizeLTO;

            if ((s.Contains("speed") && s.Contains("lto")) || s.Contains("runtimespeedwithlto"))
                return CodeOptimizationState.RuntimeSpeedLTO;

            if (s.Contains("disksize") || (s.Contains("disk") && s.Contains("size") && !s.Contains("lto")))
                return CodeOptimizationState.DiskSize;

            if (s.Contains("shorterbuildtime") || s.Contains("fasterwithoutlto") || s.Contains("fasterbuilds"))
                return CodeOptimizationState.ShorterBuildTime;

            if (s.Contains("optimizesize") || s.Contains("optforsize") || (s.Contains("size") && !s.Contains("disk")))
                return CodeOptimizationState.Size;

            if (s.Contains("optimizespeed") || s.Contains("optforspeed") || (s.Contains("speed") && !s.Contains("lto")))
                return CodeOptimizationState.Speed;

            if (s.Contains("disabled"))
                return CodeOptimizationState.None;

            return CodeOptimizationState.Unknown;
        }

        internal static bool TrySetCodeOptimization(CodeOptimizationState desired)
        {
            try
            {
                bool anySuccess = false;

                string settingValue = GetCodeOptimizationSettingValue(desired);

                // Try EditorUserBuildSettings.SetPlatformSettings (Unity 2022+ Build Profiles)
                var setPlatformSettings = typeof(EditorUserBuildSettings).GetMethod(
                    "SetPlatformSettings",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string), typeof(string), typeof(string) },
                    null);

                if (setPlatformSettings != null && !string.IsNullOrEmpty(settingValue))
                {
                    setPlatformSettings.Invoke(null, new object[] { "WebGL", "CodeOptimization", settingValue });
                    anySuccess = true;
                }

                // Try PlayerSettings.SetIl2CppCodeGeneration (Unity 2022+)
                var setIl2CppCodeGen = typeof(PlayerSettings).GetMethod(
                    "SetIl2CppCodeGeneration",
                    BindingFlags.Public | BindingFlags.Static);

                if (setIl2CppCodeGen != null)
                {
                    var namedBuildTargetType = Type.GetType("UnityEditor.Build.NamedBuildTarget, UnityEditor");
                    if (namedBuildTargetType != null)
                    {
                        var webglTarget = namedBuildTargetType.GetProperty("WebGL", BindingFlags.Public | BindingFlags.Static);

                        if (webglTarget != null)
                        {
                            object target = webglTarget.GetValue(null);

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

                // Try EditorUserBuildSettings.il2CppCodeGeneration (legacy)
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
                UnityEngine.Debug.LogWarning($"[Playgama Bridge] Failed to set Code Optimization: {ex.Message}");
                return false;
            }
        }

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

        private static object FindEnumValue(Type enumType, CodeOptimizationState desired)
        {
            var names = Enum.GetNames(enumType);

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
                    if ((n.Contains("disksize") || (n.Contains("disk") && n.Contains("size"))) && !n.Contains("lto"))
                        return Enum.Parse(enumType, names[i]);
                }
                return null;
            }

            string[] patterns;
            string[] excludePatterns = null;

            switch (desired)
            {
                case CodeOptimizationState.Size:
                    patterns = new[] { "size", "optforsize", "optimizesize" };
                    excludePatterns = new[] { "disk", "lto" };
                    break;
                case CodeOptimizationState.Speed:
                    patterns = new[] { "speed", "runtime", "optforspeed", "optimizespeed" };
                    excludePatterns = new[] { "lto" };
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

            for (int i = 0; i < names.Length; i++)
            {
                string n = names[i].ToLowerInvariant();

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

        private static void InvokeBuildAnalyzer(string outputPath)
        {
            try
            {
                var asm = typeof(BuildSettingsTab).Assembly;
                Type t = asm.GetType("Playgama.Bridge.BuildAnalyzer") ?? asm.GetType("Playgama.Bridge.Utils.BuildAnalyzer");
                if (t == null)
                {
                    UnityEngine.Debug.LogError("Bridge: BuildAnalyzer type not found.");
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

                UnityEngine.Debug.LogError("Bridge: BuildAnalyzer.BuildAndAnalyze method not found.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }
}
