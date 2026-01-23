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

        private enum WebGLCompressionState { Unknown, Disabled, Enabled_Brotli, Enabled_Gzip, Enabled_Other }
        internal enum CodeOptimizationState { Unknown, None, Size, Speed, ShorterBuildTime, RuntimeSpeedLTO, DiskSize, DiskSizeLTO }

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
            _outputPath = EditorPrefs.GetString(Pref_OutputPath, "");
            if (string.IsNullOrEmpty(_outputPath))
                _outputPath = Path.Combine(GetProjectRoot(), "Builds", "WebGL").Replace('\\', '/');
            _devBuild = EditorUserBuildSettings.development;
            _nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            ReadWebGLCompression(out _compressionState);
            ReadCodeOptimization(out _codeOptimizationState);
        }

        public void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;
                DrawHeader();
                DrawOutputBlock();
                DrawScenesBlock();
                DrawWebGLBlock();
                DrawBuildBlock();
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
                GUILayout.Label("Folder", GUILayout.Width(50));
                _outputPath = EditorGUILayout.TextField(_outputPath);
                if (GUILayout.Button("Browse", GUILayout.Width(80)))
                {
                    string c = EditorUtility.SaveFolderPanel("Choose Build Output", _outputPath, "");
                    if (!string.IsNullOrEmpty(c)) { _outputPath = c.Replace('\\', '/'); EditorPrefs.SetString(Pref_OutputPath, _outputPath); }
                }
                if (GUILayout.Button("Reset", GUILayout.Width(70)))
                {
                    _outputPath = Path.Combine(GetProjectRoot(), "Builds", "WebGL").Replace('\\', '/');
                    EditorPrefs.SetString(Pref_OutputPath, _outputPath);
                }
            }
            EditorGUILayout.LabelField("Keep build output outside Assets/ to avoid imports.", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();
        }

        private void DrawScenesBlock()
        {
            var scenes = EditorBuildSettings.scenes;
            int en = 0; foreach (var s in scenes) if (s != null && s.enabled) en++;
            _foldScenes = BridgeStyles.DrawSectionHeader($"Scenes ({en}/{scenes.Length} enabled)", _foldScenes, "\u2302");
            if (!_foldScenes) return;
            BridgeStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Enable All", GUILayout.Width(90))) SetAllScenes(true);
                if (GUILayout.Button("Disable All", GUILayout.Width(90))) SetAllScenes(false);
                if (GUILayout.Button("Add Open Scenes", GUILayout.Width(120))) AddOpenScenes();
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(4);
            for (int i = 0; i < scenes.Length; i++)
            {
                var s = scenes[i]; if (s == null) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool ne = EditorGUILayout.Toggle(s.enabled, GUILayout.Width(18));
                    if (ne != s.enabled) { s.enabled = ne; scenes[i] = s; EditorBuildSettings.scenes = scenes; }
                    EditorGUILayout.LabelField(s.path);
                    if (GUILayout.Button("Ping", GUILayout.Width(50)))
                    { var o = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(s.path); if (o) EditorGUIUtility.PingObject(o); }
                }
            }
            BridgeStyles.EndCard();
        }

        private void DrawWebGLBlock()
        {
            _foldWebGL = BridgeStyles.DrawSectionHeader("WebGL Build Size Toggles", _foldWebGL, "\u2699");
            if (!_foldWebGL) return;
            BridgeStyles.BeginCard();

            // Status summary
            bool optimal = !_devBuild && (_compressionState == WebGLCompressionState.Enabled_Brotli || _compressionState == WebGLCompressionState.Enabled_Gzip);
            Rect sr = EditorGUILayout.GetControlRect(false, 24);
            EditorGUI.DrawRect(sr, optimal ? new Color(0.2f, 0.5f, 0.2f, 0.3f) : new Color(0.5f, 0.4f, 0.1f, 0.3f));
            GUI.Label(sr, optimal ? "  ✓ Settings optimized for small build" : "  ⚠ Settings can be optimized", EditorStyles.boldLabel);

            GUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool nd = EditorGUILayout.ToggleLeft("Development Build", _devBuild, GUILayout.Width(150));
                if (nd != _devBuild) { _devBuild = nd; EditorUserBuildSettings.development = _devBuild; }
                if (_devBuild) GUILayout.Label("⚠ Larger size", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.yellow } });
                GUILayout.FlexibleSpace();
                bool nh = EditorGUILayout.ToggleLeft("Name Files As Hashes", _nameFilesAsHashes, GUILayout.Width(160));
                if (nh != _nameFilesAsHashes) { _nameFilesAsHashes = nh; PlayerSettings.WebGL.nameFilesAsHashes = _nameFilesAsHashes; }
            }

            GUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Compression", GUILayout.Width(85));
                if (GUILayout.Button(CompressionLabel(_compressionState), GUILayout.Width(120))) ShowCompressionMenu();
                GUILayout.Space(15);
                GUILayout.Label("Code Opt", GUILayout.Width(60));
                if (GUILayout.Button(CodeOptLabel(_codeOptimizationState), GUILayout.Width(140))) ShowCodeOptMenu();
                GUILayout.FlexibleSpace();
            }

            GUILayout.Space(6);
            GUIStyle info = new GUIStyle(EditorStyles.miniLabel) { richText = true, wordWrap = true };
            if (_compressionState == WebGLCompressionState.Disabled)
                EditorGUILayout.LabelField("<color=#ff9966>Compression OFF:</color> ~3-4x larger download. Enable for production.", info);
            else if (_compressionState == WebGLCompressionState.Enabled_Brotli)
                EditorGUILayout.LabelField("<color=#66ff66>Brotli:</color> Best compression. Requires server support.", info);
            else if (_compressionState == WebGLCompressionState.Enabled_Gzip)
                EditorGUILayout.LabelField("<color=#99ff99>Gzip:</color> Good compression, widely supported.", info);

            if (_codeOptimizationState == CodeOptimizationState.DiskSizeLTO)
                EditorGUILayout.LabelField("⏱ LTO: 3-5x longer build, 10-30% smaller output", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.8f, 0.4f) } });

            BridgeStyles.EndCard();
        }

        private void DrawBuildBlock()
        {
            _foldBuild = BridgeStyles.DrawSectionHeader("Build & Analyze", _foldBuild, "\u26A1");
            if (!_foldBuild) return;

            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Quick Analysis Build", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Fast build for testing. Uses 'Shorter Build Time'.", BridgeStyles.SubtitleStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = HasEnabledScenes();
                if (BridgeStyles.DrawAccentButton(new GUIContent("Build & Analyze (WebGL)"), GUILayout.Height(30)))
                    EditorApplication.delayCall += () => { EnsureDir(_outputPath); InvokeBuildAnalyzer(_outputPath); };
                GUI.enabled = true;
                if (GUILayout.Button("Open Folder", GUILayout.Height(30), GUILayout.Width(100)))
                    if (Directory.Exists(_outputPath)) EditorUtility.RevealInFinder(_outputPath);
            }
            EditorGUILayout.LabelField("⏱ ~1-3 min", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.8f, 1f) } });
            BridgeStyles.EndCard();

            GUILayout.Space(8);

            BridgeStyles.BeginCard();
            Rect hr = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(hr, new Color(0.15f, 0.4f, 0.15f, 0.5f));
            GUI.Label(new Rect(hr.x + 8, hr.y, hr.width, hr.height), "Build for Release", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Smallest build with 'Disk Size LTO'. Takes longer.", BridgeStyles.SubtitleStyle);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = HasEnabledScenes();
                Color ob = GUI.backgroundColor; GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f);
                if (GUILayout.Button("Build for Release", GUILayout.Height(30)))
                    EditorApplication.delayCall += () => { EnsureDir(_outputPath); BuildAnalyzer.BuildForRelease(); };
                GUI.backgroundColor = ob; GUI.enabled = true;
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.LabelField("⏱ ~5-15 min (LTO)", new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.8f, 0.4f) } });
            BridgeStyles.EndCard();
        }

        private void DrawLastBuildBlock()
        {
            bool has = _buildInfo != null && (_buildInfo.TotalBuildSizeBytes > 0 || _buildInfo.HasData);
            string hdr = has ? (_buildInfo.BuildSucceeded ? "Last Build ✓" : "Last Build ✗") : "Last Build";
            _foldLastBuild = BridgeStyles.DrawSectionHeader(hdr, _foldLastBuild, "\u2139");
            if (!_foldLastBuild) return;
            BridgeStyles.BeginCard();
            if (!has) { EditorGUILayout.LabelField("No build analyzed yet.", BridgeStyles.SubtitleStyle); BridgeStyles.EndCard(); return; }

            Rect rr = EditorGUILayout.GetControlRect(false, 22);
            EditorGUI.DrawRect(rr, _buildInfo.BuildSucceeded ? new Color(0.2f, 0.5f, 0.2f, 0.5f) : new Color(0.5f, 0.2f, 0.2f, 0.5f));
            GUI.Label(rr, _buildInfo.BuildSucceeded ? "  ✓ SUCCESS" : "  ✗ FAILED", EditorStyles.boldLabel);

            GUILayout.Space(4);
            LV("Total Size", SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes));
            if (_buildInfo.BuildTime.TotalSeconds > 0)
                LV("Build Time", _buildInfo.BuildTime.TotalMinutes >= 1 ? $"{(int)_buildInfo.BuildTime.TotalMinutes}m {_buildInfo.BuildTime.Seconds}s" : $"{_buildInfo.BuildTime.TotalSeconds:0.0}s");
            LV("Assets", _buildInfo.TrackedAssetCount.ToString());
            LV("Tracked", SharedTypes.FormatBytes(_buildInfo.TrackedBytes));

            if (GUILayout.Button("Copy to Clipboard", GUILayout.Width(150)))
                EditorGUIUtility.systemCopyBuffer = $"Build: {(_buildInfo.BuildSucceeded ? "OK" : "FAIL")}\nSize: {SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes)}\nAssets: {_buildInfo.TrackedAssetCount}";
            BridgeStyles.EndCard();
        }

        private void LV(string l, string v) { using (new EditorGUILayout.HorizontalScope()) { GUILayout.Label(l, GUILayout.Width(100)); GUILayout.FlexibleSpace(); GUILayout.Label(v); } }

        private bool HasEnabledScenes() { foreach (var s in EditorBuildSettings.scenes) if (s != null && s.enabled) return true; return false; }
        private void SetAllScenes(bool e) { var sc = EditorBuildSettings.scenes; for (int i = 0; i < sc.Length; i++) if (sc[i] != null) sc[i].enabled = e; EditorBuildSettings.scenes = sc; }
        private void AddOpenScenes() { var l = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes); for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++) { var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i); if (s.IsValid() && !string.IsNullOrEmpty(s.path) && s.path.StartsWith("Assets/")) { bool ex = false; foreach (var x in l) if (x.path == s.path) { ex = true; break; } if (!ex) l.Add(new EditorBuildSettingsScene(s.path, true)); } } EditorBuildSettings.scenes = l.ToArray(); }
        private static string GetProjectRoot() { var a = Application.dataPath.Replace('\\', '/'); return a.EndsWith("/Assets") ? a.Substring(0, a.Length - 7) : Directory.GetParent(Application.dataPath).FullName; }
        private static void EnsureDir(string p) { if (!string.IsNullOrEmpty(p) && !Directory.Exists(p)) Directory.CreateDirectory(p); }

        private string CompressionLabel(WebGLCompressionState s) { switch (s) { case WebGLCompressionState.Disabled: return "Disabled"; case WebGLCompressionState.Enabled_Brotli: return "Brotli"; case WebGLCompressionState.Enabled_Gzip: return "Gzip"; default: return "Unknown"; } }
        private string CodeOptLabel(CodeOptimizationState s) { switch (s) { case CodeOptimizationState.DiskSizeLTO: return "Disk Size (LTO)"; case CodeOptimizationState.DiskSize: return "Disk Size"; case CodeOptimizationState.RuntimeSpeedLTO: return "Speed (LTO)"; case CodeOptimizationState.Speed: return "Speed"; case CodeOptimizationState.ShorterBuildTime: return "Shorter Build"; case CodeOptimizationState.None: return "None"; default: return "Unknown"; } }

        private void ShowCompressionMenu()
        {
            var m = new GenericMenu();
            m.AddItem(new GUIContent("Disabled"), _compressionState == WebGLCompressionState.Disabled, () => TrySetCompression(WebGLCompressionState.Disabled));
            m.AddItem(new GUIContent("Brotli (Recommended)"), _compressionState == WebGLCompressionState.Enabled_Brotli, () => TrySetCompression(WebGLCompressionState.Enabled_Brotli));
            m.AddItem(new GUIContent("Gzip"), _compressionState == WebGLCompressionState.Enabled_Gzip, () => TrySetCompression(WebGLCompressionState.Enabled_Gzip));
            m.ShowAsContext();
        }

        private void ShowCodeOptMenu()
        {
            var m = new GenericMenu();
            m.AddItem(new GUIContent("Disk Size (LTO) - Smallest"), _codeOptimizationState == CodeOptimizationState.DiskSizeLTO, () => TrySetCodeOptimization(CodeOptimizationState.DiskSizeLTO));
            m.AddItem(new GUIContent("Disk Size"), _codeOptimizationState == CodeOptimizationState.DiskSize, () => TrySetCodeOptimization(CodeOptimizationState.DiskSize));
            m.AddSeparator("");
            m.AddItem(new GUIContent("Shorter Build Time"), _codeOptimizationState == CodeOptimizationState.ShorterBuildTime, () => TrySetCodeOptimization(CodeOptimizationState.ShorterBuildTime));
            m.ShowAsContext();
        }

        private static void ReadWebGLCompression(out WebGLCompressionState state)
        {
            state = WebGLCompressionState.Unknown;
            try { var t = typeof(PlayerSettings).GetNestedType("WebGL", BindingFlags.Public); var p = t?.GetProperty("compressionFormat", BindingFlags.Public | BindingFlags.Static); var v = p?.GetValue(null, null)?.ToString().ToLowerInvariant(); if (v != null) { if (v.Contains("disabled") || v.Contains("none")) state = WebGLCompressionState.Disabled; else if (v.Contains("brotli")) state = WebGLCompressionState.Enabled_Brotli; else if (v.Contains("gzip")) state = WebGLCompressionState.Enabled_Gzip; } } catch { }
        }

        private void TrySetCompression(WebGLCompressionState d)
        {
            try { var t = typeof(PlayerSettings).GetNestedType("WebGL", BindingFlags.Public); var p = t?.GetProperty("compressionFormat", BindingFlags.Public | BindingFlags.Static); if (p == null || !p.CanWrite) return; var et = p.PropertyType; foreach (var n in Enum.GetNames(et)) { var nl = n.ToLowerInvariant(); if ((d == WebGLCompressionState.Disabled && (nl.Contains("disabled") || nl.Contains("none"))) || (d == WebGLCompressionState.Enabled_Brotli && nl.Contains("brotli")) || (d == WebGLCompressionState.Enabled_Gzip && nl.Contains("gzip"))) { p.SetValue(null, Enum.Parse(et, n), null); _compressionState = d; return; } } } catch { }
        }

        private static void ReadCodeOptimization(out CodeOptimizationState state)
        {
            state = CodeOptimizationState.Unknown;
            try { var m = typeof(EditorUserBuildSettings).GetMethod("GetPlatformSettings", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null); if (m != null) { var r = m.Invoke(null, new object[] { "WebGL", "CodeOptimization" })?.ToString().ToLowerInvariant(); if (!string.IsNullOrEmpty(r)) { if (r.Contains("disksizelto") || (r.Contains("disk") && r.Contains("lto"))) state = CodeOptimizationState.DiskSizeLTO; else if (r.Contains("disksize")) state = CodeOptimizationState.DiskSize; else if (r.Contains("shorterbuildtime")) state = CodeOptimizationState.ShorterBuildTime; else if (r.Contains("speed") && r.Contains("lto")) state = CodeOptimizationState.RuntimeSpeedLTO; else if (r.Contains("speed")) state = CodeOptimizationState.Speed; } } } catch { }
        }

        internal static bool TrySetCodeOptimization(CodeOptimizationState d)
        {
            try { var m = typeof(EditorUserBuildSettings).GetMethod("SetPlatformSettings", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string), typeof(string) }, null); if (m != null) { string v = d == CodeOptimizationState.DiskSizeLTO ? "diskSizeLto" : d == CodeOptimizationState.DiskSize ? "diskSize" : d == CodeOptimizationState.ShorterBuildTime ? "shorterBuildTime" : d == CodeOptimizationState.RuntimeSpeedLTO ? "runtimeSpeedLto" : "speed"; m.Invoke(null, new object[] { "WebGL", "CodeOptimization", v }); return true; } } catch { } return false;
        }

        private static void InvokeBuildAnalyzer(string p)
        {
            try { var t = typeof(BuildSettingsTab).Assembly.GetType("Playgama.Bridge.BuildAnalyzer"); var m = t?.GetMethod("BuildAndAnalyze", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null) ?? t?.GetMethod("BuildAndAnalyze", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null); m?.Invoke(null, m.GetParameters().Length > 0 ? new object[] { p } : null); } catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
