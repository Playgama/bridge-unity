using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge
{
    /// <summary>
    /// Step-by-step optimization wizard that guides users through the WebGL build optimization process.
    /// </summary>
    public class OptimizationWizard : EditorWindow
    {
        private static OptimizationWizard _instance;

        private int _currentStep = 0;
        private int _highestStepReached = 0; // Track the furthest step user has reached
        private Vector2 _scroll;
        private BuildInfo _buildInfo;

        // Step definitions
        private readonly string[] _stepNames = new[]
        {
            "Welcome",
            "Build & Analyze",
            "Textures",
            "Audio",
            "Meshes",
            "Build Settings",
            "Complete"
        };

        private readonly string[] _stepIcons = new[]
        {
            "👋",
            "🔨",
            "🖼",
            "🔊",
            "📐",
            "⚙",
            "✅"
        };

        // Texture optimization state
        private List<TextureAssetInfo> _textures = new List<TextureAssetInfo>();
        private int _batchMaxSize = 1024;
        private bool _batchCrunch = true;
        private int _batchCrunchQuality = 50;

        // Audio optimization state
        private List<AudioAssetInfo> _audioClips = new List<AudioAssetInfo>();
        private AudioClipLoadType _audioLoadType = AudioClipLoadType.CompressedInMemory;
        private float _audioQuality = 0.5f;
        private bool _audioForceToMono = true;

        // Mesh optimization state
        private List<MeshAssetInfo> _meshes = new List<MeshAssetInfo>();
        private ModelImporterMeshCompression _meshCompression = ModelImporterMeshCompression.Medium;

        private class TextureAssetInfo
        {
            public string Path;
            public long SizeBytes;
            public int MaxSize;
            public bool Selected;
        }

        private class AudioAssetInfo
        {
            public string Path;
            public long SizeBytes;
            public AudioClipLoadType LoadType;
            public bool Selected;
        }

        private class MeshAssetInfo
        {
            public string Path;
            public long SizeBytes;
            public ModelImporterMeshCompression Compression;
            public bool Selected;
        }

        [MenuItem("Playgama/Optimization Wizard", priority = 50)]
        public static void ShowWizard()
        {
            if (_instance != null)
            {
                _instance.Focus();
                return;
            }

            _instance = GetWindow<OptimizationWizard>();
            _instance.titleContent = new GUIContent("Optimization Wizard");
            _instance.minSize = new Vector2(600, 500);
            _instance.maxSize = new Vector2(800, 700);
            _instance.Show();
        }

        private void OnEnable()
        {
            // Enable mouse move events for responsive hover effects
            wantsMouseMove = true;

            _buildInfo = new BuildInfo();

            // Try to load most recent report
            var savedReport = BuildReportStorage.LoadMostRecentReport();
            if (savedReport != null)
            {
                _buildInfo = savedReport;
                RefreshAssetLists();
            }
        }

        private void OnDestroy()
        {
            _instance = null;
        }

        private void OnGUI()
        {
            // Repaint on mouse move for responsive hover effects
            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }

            // Header with step indicator
            DrawHeader();

            // Scrollable content area
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                EditorGUILayout.Space(10);

                // Draw current step content
                switch (_currentStep)
                {
                    case 0: DrawWelcomeStep(); break;
                    case 1: DrawBuildStep(); break;
                    case 2: DrawTexturesStep(); break;
                    case 3: DrawAudioStep(); break;
                    case 4: DrawMeshesStep(); break;
                    case 5: DrawBuildSettingsStep(); break;
                    case 6: DrawCompleteStep(); break;
                }
            }

            // Navigation buttons
            DrawNavigation();
        }

        private void DrawHeader()
        {
            // Purple header bar
            Rect headerRect = EditorGUILayout.GetControlRect(false, 60);
            EditorGUI.DrawRect(headerRect, new Color(0.15f, 0.12f, 0.2f));

            // Title
            Rect titleRect = new Rect(headerRect.x + 20, headerRect.y + 8, headerRect.width - 40, 24);
            GUI.Label(titleRect, "WebGL Optimization Wizard", EditorStyles.whiteLargeLabel);

            // Step indicator
            float stepWidth = (headerRect.width - 40) / _stepNames.Length;
            float stepY = headerRect.y + 35;

            for (int i = 0; i < _stepNames.Length; i++)
            {
                float stepX = headerRect.x + 20 + (i * stepWidth);
                Rect stepRect = new Rect(stepX, stepY, stepWidth - 4, 20);

                // Determine if this step is clickable (already visited)
                bool isClickable = i <= _highestStepReached && i != _currentStep;
                bool isHovered = isClickable && stepRect.Contains(Event.current.mousePosition);

                // Step background
                Color bgColor;
                if (i < _currentStep)
                {
                    // Completed - green (brighter on hover)
                    bgColor = isHovered
                        ? new Color(0.3f, 0.75f, 0.4f, 0.95f)
                        : new Color(0.2f, 0.6f, 0.3f, 0.8f);
                }
                else if (i == _currentStep)
                {
                    bgColor = BridgeStyles.BrandPurple; // Current - purple
                }
                else if (i <= _highestStepReached)
                {
                    // Previously visited but ahead of current - lighter purple (brighter on hover)
                    bgColor = isHovered
                        ? new Color(0.5f, 0.35f, 0.6f, 0.95f)
                        : new Color(0.4f, 0.28f, 0.5f, 0.8f);
                }
                else
                {
                    bgColor = new Color(0.3f, 0.3f, 0.35f, 0.8f); // Pending - gray
                }

                EditorGUI.DrawRect(stepRect, bgColor);

                // Show hand cursor for clickable steps
                if (isClickable)
                {
                    EditorGUIUtility.AddCursorRect(stepRect, MouseCursor.Link);
                }

                // Handle click on completed/visited steps
                if (isClickable && Event.current.type == EventType.MouseDown && stepRect.Contains(Event.current.mousePosition))
                {
                    _currentStep = i;
                    Event.current.Use();
                    Repaint();
                }

                // Step label with step name tooltip
                string label = $"{i + 1}";
                string tooltip = isClickable ? $"Go to: {_stepNames[i]}" : _stepNames[i];
                GUI.Label(stepRect, new GUIContent(label, tooltip), GetStepLabelStyle(i == _currentStep));
            }
        }

        private GUIStyle GetStepLabelStyle(bool isCurrent)
        {
            var style = new GUIStyle(EditorStyles.miniLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = Color.white;
            if (isCurrent)
                style.fontStyle = FontStyle.Bold;
            return style;
        }

        private void DrawNavigation()
        {
            EditorGUILayout.Space(10);

            Rect navRect = EditorGUILayout.GetControlRect(false, 50);
            EditorGUI.DrawRect(navRect, new Color(0.18f, 0.18f, 0.2f));

            float buttonWidth = 120;
            float buttonHeight = 30;
            float buttonY = navRect.y + 10;

            // Back button
            if (_currentStep > 0)
            {
                Rect backRect = new Rect(navRect.x + 20, buttonY, buttonWidth, buttonHeight);
                if (GUI.Button(backRect, "← Back"))
                {
                    _currentStep--;
                }
            }

            // Step name in center
            Rect centerRect = new Rect(navRect.x + navRect.width / 2 - 100, buttonY, 200, buttonHeight);
            GUI.Label(centerRect, $"{_stepIcons[_currentStep]} {_stepNames[_currentStep]}", GetCenterLabelStyle());

            // Next/Finish button
            Rect nextRect = new Rect(navRect.x + navRect.width - buttonWidth - 20, buttonY, buttonWidth, buttonHeight);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = BridgeStyles.BrandPurple;

            string nextLabel = _currentStep == _stepNames.Length - 1 ? "Finish ✓" : "Next →";
            if (GUI.Button(nextRect, nextLabel))
            {
                if (_currentStep < _stepNames.Length - 1)
                {
                    _currentStep++;
                    // Track the highest step reached
                    if (_currentStep > _highestStepReached)
                        _highestStepReached = _currentStep;
                }
                else
                {
                    Close();
                }
            }

            GUI.backgroundColor = oldBg;
        }

        private GUIStyle GetCenterLabelStyle()
        {
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 14;
            return style;
        }

        #region Step: Welcome

        private void DrawWelcomeStep()
        {
            DrawStepTitle("Welcome to the Optimization Wizard",
                "This wizard will guide you through optimizing your WebGL build step by step.");

            EditorGUILayout.Space(20);

            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("What we'll do:", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            DrawChecklistItem("1. Build & Analyze", "Run a WebGL build to analyze your project");
            DrawChecklistItem("2. Optimize Textures", "Reduce texture sizes with compression");
            DrawChecklistItem("3. Optimize Audio", "Configure audio for smaller builds");
            DrawChecklistItem("4. Optimize Meshes", "Apply mesh compression");
            DrawChecklistItem("5. Build Settings", "Configure optimal WebGL settings");

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Click 'Next' to begin!", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();
        }

        private void DrawChecklistItem(string title, string description)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("☐", GUILayout.Width(20));
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(description, BridgeStyles.SubtitleStyle);
                }
            }
            EditorGUILayout.Space(5);
        }

        #endregion

        #region Step: Build & Analyze

        private void DrawBuildStep()
        {
            DrawStepTitle("Build & Analyze",
                "First, let's run a WebGL build to analyze your project's assets.");

            EditorGUILayout.Space(20);

            BridgeStyles.BeginCard();

            if (_buildInfo != null && _buildInfo.HasData)
            {
                // Show existing build info
                EditorGUILayout.LabelField("✓ Build Analysis Available", EditorStyles.boldLabel);
                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Total Build Size:", SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes));
                EditorGUILayout.LabelField("Tracked Assets:", _buildInfo.TrackedAssetCount.ToString());
                EditorGUILayout.LabelField("Analysis Mode:", _buildInfo.DataMode.ToString());

                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox("You can proceed with the existing analysis or run a new build.", MessageType.Info);

                EditorGUILayout.Space(10);
                if (GUILayout.Button("Run New Build & Analyze", GUILayout.Height(35)))
                {
                    RunBuildAndAnalyze();
                }
            }
            else
            {
                // No build info
                EditorGUILayout.LabelField("No build analysis found.", EditorStyles.boldLabel);
                EditorGUILayout.Space(10);
                EditorGUILayout.HelpBox(
                    "To optimize your build, we first need to analyze it.\n\n" +
                    "This will create a WebGL build and analyze all included assets.",
                    MessageType.Info);

                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "Note: This performs a clean build to get accurate asset sizes.\n" +
                    "Clean builds take longer than incremental builds, but provide precise data for optimization.",
                    MessageType.Warning);

                EditorGUILayout.Space(10);

                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = BridgeStyles.BrandPurple;
                if (GUILayout.Button("Build & Analyze Now", GUILayout.Height(40)))
                {
                    RunBuildAndAnalyze();
                }
                GUI.backgroundColor = oldBg;

                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Or skip this step to continue with manual optimization.", BridgeStyles.SubtitleStyle);
            }

            BridgeStyles.EndCard();
        }

        private void RunBuildAndAnalyze()
        {
            BuildAnalyzer.OnBuildInfoChanged += OnBuildInfoChanged;
            BuildAnalyzer.BuildAndAnalyze();
        }

        private void OnBuildInfoChanged(BuildInfo info)
        {
            _buildInfo = info;
            RefreshAssetLists();
            Repaint();
        }

        private void RefreshAssetLists()
        {
            if (_buildInfo == null || !_buildInfo.HasData)
                return;

            // Refresh texture list
            _textures.Clear();
            foreach (var asset in _buildInfo.Assets.Where(a => a.Category == AssetCategory.Textures))
            {
                var importer = AssetImporter.GetAtPath(asset.Path) as TextureImporter;
                _textures.Add(new TextureAssetInfo
                {
                    Path = asset.Path,
                    SizeBytes = asset.SizeBytes,
                    MaxSize = importer?.maxTextureSize ?? 2048,
                    Selected = true
                });
            }
            _textures = _textures.OrderByDescending(t => t.SizeBytes).ToList();

            // Refresh audio list
            _audioClips.Clear();
            foreach (var asset in _buildInfo.Assets.Where(a => a.Category == AssetCategory.Audio))
            {
                var importer = AssetImporter.GetAtPath(asset.Path) as AudioImporter;
                _audioClips.Add(new AudioAssetInfo
                {
                    Path = asset.Path,
                    SizeBytes = asset.SizeBytes,
                    LoadType = importer?.defaultSampleSettings.loadType ?? AudioClipLoadType.DecompressOnLoad,
                    Selected = true
                });
            }
            _audioClips = _audioClips.OrderByDescending(a => a.SizeBytes).ToList();

            // Refresh mesh list
            _meshes.Clear();
            foreach (var asset in _buildInfo.Assets.Where(a => a.Category == AssetCategory.Meshes || a.Category == AssetCategory.Models))
            {
                var importer = AssetImporter.GetAtPath(asset.Path) as ModelImporter;
                _meshes.Add(new MeshAssetInfo
                {
                    Path = asset.Path,
                    SizeBytes = asset.SizeBytes,
                    Compression = importer?.meshCompression ?? ModelImporterMeshCompression.Off,
                    Selected = true
                });
            }
            _meshes = _meshes.OrderByDescending(m => m.SizeBytes).ToList();
        }

        #endregion

        #region Step: Textures

        private Vector2 _texturesScroll;

        private void DrawTexturesStep()
        {
            DrawStepTitle("Optimize Textures",
                "Textures are often the largest assets. Let's optimize them for WebGL.");

            EditorGUILayout.Space(10);

            // Quick stats
            BridgeStyles.BeginCard();
            int textureCount = _textures.Count;
            long totalSize = _textures.Sum(t => t.SizeBytes);
            int selectedCount = _textures.Count(t => t.Selected);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Found: {textureCount} textures", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total: {SharedTypes.FormatBytes(totalSize)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Selected: {selectedCount}", EditorStyles.boldLabel);
            }
            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            // Batch settings
            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Max Size:", GUILayout.Width(70));
                _batchMaxSize = EditorGUILayout.IntPopup(_batchMaxSize,
                    new[] { "256", "512", "1024", "2048" },
                    new[] { 256, 512, 1024, 2048 },
                    GUILayout.Width(100));

                GUILayout.Space(20);

                _batchCrunch = EditorGUILayout.ToggleLeft("Crunch Compression", _batchCrunch, GUILayout.Width(140));

                if (_batchCrunch)
                {
                    EditorGUILayout.LabelField("Quality:", GUILayout.Width(50));
                    _batchCrunchQuality = EditorGUILayout.IntSlider(_batchCrunchQuality, 0, 100, GUILayout.Width(150));
                }
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All", GUILayout.Width(100)))
                    _textures.ForEach(t => t.Selected = true);
                if (GUILayout.Button("Select None", GUILayout.Width(100)))
                    _textures.ForEach(t => t.Selected = false);
                if (GUILayout.Button("Select Large (>100KB)", GUILayout.Width(150)))
                {
                    _textures.ForEach(t => t.Selected = false);
                    _textures.Where(t => t.SizeBytes > 100 * 1024).ToList().ForEach(t => t.Selected = true);
                }

                GUILayout.FlexibleSpace();

                GUI.enabled = selectedCount > 0;
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = BridgeStyles.BrandPurple;
                if (GUILayout.Button($"Apply to {selectedCount} Textures", GUILayout.Height(25), GUILayout.Width(180)))
                {
                    ApplyTextureOptimization();
                }
                GUI.backgroundColor = oldBg;
                GUI.enabled = true;
            }
            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            // Texture list
            if (_textures.Count == 0)
            {
                EditorGUILayout.HelpBox("No textures found. Run Build & Analyze first.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"All Textures by Size ({_textures.Count} total):", EditorStyles.boldLabel);

                using (var scroll = new EditorGUILayout.ScrollViewScope(_texturesScroll, GUILayout.Height(250)))
                {
                    _texturesScroll = scroll.scrollPosition;

                    for (int i = 0; i < _textures.Count; i++)
                    {
                        var tex = _textures[i];
                        DrawAssetRow(tex.Path, tex.SizeBytes, ref tex.Selected, $"Max: {tex.MaxSize}");
                    }
                }
            }
        }

        private void ApplyTextureOptimization()
        {
            var selected = _textures.Where(t => t.Selected).ToList();
            if (selected.Count == 0) return;

            try
            {
                var paths = selected.Select(t => t.Path).ToList();
                var settings = new TextureBatchSettings
                {
                    MaxSizeWebGL = _batchMaxSize,
                    CompressionWebGL = TextureImporterCompression.Compressed,
                    CrunchWebGL = _batchCrunch,
                    CrunchQualityWebGL = _batchCrunchQuality,
                    DisableReadWrite = false,
                    OverrideWebGL = true
                };

                TextureOptimizationUtility.ApplyBatch(paths, settings, (progress, path) =>
                {
                    EditorUtility.DisplayProgressBar("Optimizing Textures",
                        $"Processing {Path.GetFileName(path)}...",
                        progress);
                });

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Success", $"Optimized {selected.Count} textures!", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Failed to optimize textures: {ex.Message}", "OK");
            }
        }

        #endregion

        #region Step: Audio

        private Vector2 _audioScroll;

        private void DrawAudioStep()
        {
            DrawStepTitle("Optimize Audio",
                "Audio files can add significant size. Let's compress them for WebGL.");

            EditorGUILayout.Space(10);

            // Quick stats
            BridgeStyles.BeginCard();
            int audioCount = _audioClips.Count;
            long totalSize = _audioClips.Sum(a => a.SizeBytes);
            int selectedCount = _audioClips.Count(a => a.Selected);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Found: {audioCount} audio clips", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total: {SharedTypes.FormatBytes(totalSize)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Selected: {selectedCount}", EditorStyles.boldLabel);
            }
            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            // Batch settings
            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Load Type:", GUILayout.Width(70));
                _audioLoadType = (AudioClipLoadType)EditorGUILayout.EnumPopup(_audioLoadType, GUILayout.Width(160));

                GUILayout.Space(20);

                _audioForceToMono = EditorGUILayout.ToggleLeft("Force Mono", _audioForceToMono, GUILayout.Width(100));

                EditorGUILayout.LabelField("Quality:", GUILayout.Width(50));
                _audioQuality = EditorGUILayout.Slider(_audioQuality, 0f, 1f, GUILayout.Width(150));
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All", GUILayout.Width(100)))
                    _audioClips.ForEach(a => a.Selected = true);
                if (GUILayout.Button("Select None", GUILayout.Width(100)))
                    _audioClips.ForEach(a => a.Selected = false);

                GUILayout.FlexibleSpace();

                GUI.enabled = selectedCount > 0;
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = BridgeStyles.BrandPurple;
                if (GUILayout.Button($"Apply to {selectedCount} Audio Clips", GUILayout.Height(25), GUILayout.Width(180)))
                {
                    ApplyAudioOptimization();
                }
                GUI.backgroundColor = oldBg;
                GUI.enabled = true;
            }
            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            // Audio list
            if (_audioClips.Count == 0)
            {
                EditorGUILayout.HelpBox("No audio clips found. Run Build & Analyze first, or skip this step.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"All Audio Clips by Size ({_audioClips.Count} total):", EditorStyles.boldLabel);

                using (var scroll = new EditorGUILayout.ScrollViewScope(_audioScroll, GUILayout.Height(250)))
                {
                    _audioScroll = scroll.scrollPosition;

                    for (int i = 0; i < _audioClips.Count; i++)
                    {
                        var audio = _audioClips[i];
                        DrawAssetRow(audio.Path, audio.SizeBytes, ref audio.Selected, audio.LoadType.ToString());
                    }
                }
            }
        }

        private void ApplyAudioOptimization()
        {
            var selected = _audioClips.Where(a => a.Selected).ToList();
            if (selected.Count == 0) return;

            try
            {
                EditorUtility.DisplayProgressBar("Optimizing Audio", "Applying settings...", 0f);

                var options = new AudioOptimizationUtility.ApplyOptions
                {
                    LoadType = _audioLoadType,
                    CompressionFormat = AudioCompressionFormat.Vorbis,
                    Quality = _audioQuality,
                    ForceToMono = _audioForceToMono,
                    PreloadAudioData = true
                };

                int successCount = 0;
                for (int i = 0; i < selected.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Optimizing Audio",
                        $"Processing {Path.GetFileName(selected[i].Path)}...",
                        (float)i / selected.Count);

                    var importer = AssetImporter.GetAtPath(selected[i].Path) as AudioImporter;
                    if (importer != null)
                    {
                        if (AudioOptimizationUtility.ApplyToImporter(importer, options, out _))
                        {
                            importer.SaveAndReimport();
                            successCount++;
                        }
                    }
                }

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Success", $"Optimized {successCount} audio clips!", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Failed to optimize audio: {ex.Message}", "OK");
            }
        }

        #endregion

        #region Step: Meshes

        private Vector2 _meshesScroll;

        private void DrawMeshesStep()
        {
            DrawStepTitle("Optimize Meshes",
                "Mesh compression can reduce build size without visible quality loss.");

            EditorGUILayout.Space(10);

            // Quick stats
            BridgeStyles.BeginCard();
            int meshCount = _meshes.Count;
            long totalSize = _meshes.Sum(m => m.SizeBytes);
            int selectedCount = _meshes.Count(m => m.Selected);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Found: {meshCount} models", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Total: {SharedTypes.FormatBytes(totalSize)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"Selected: {selectedCount}", EditorStyles.boldLabel);
            }
            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            // Batch settings
            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Compression:", GUILayout.Width(80));
                _meshCompression = (ModelImporterMeshCompression)EditorGUILayout.EnumPopup(_meshCompression, GUILayout.Width(120));

                GUILayout.FlexibleSpace();

                EditorGUILayout.HelpBox("Medium recommended for most cases", MessageType.None);
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Select All", GUILayout.Width(100)))
                    _meshes.ForEach(m => m.Selected = true);
                if (GUILayout.Button("Select None", GUILayout.Width(100)))
                    _meshes.ForEach(m => m.Selected = false);

                GUILayout.FlexibleSpace();

                GUI.enabled = selectedCount > 0;
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = BridgeStyles.BrandPurple;
                if (GUILayout.Button($"Apply to {selectedCount} Models", GUILayout.Height(25), GUILayout.Width(180)))
                {
                    ApplyMeshOptimization();
                }
                GUI.backgroundColor = oldBg;
                GUI.enabled = true;
            }
            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            // Mesh list
            if (_meshes.Count == 0)
            {
                EditorGUILayout.HelpBox("No models found. Run Build & Analyze first, or skip this step.", MessageType.Info);
            }
            else
            {
                EditorGUILayout.LabelField($"All Models by Size ({_meshes.Count} total):", EditorStyles.boldLabel);

                using (var scroll = new EditorGUILayout.ScrollViewScope(_meshesScroll, GUILayout.Height(250)))
                {
                    _meshesScroll = scroll.scrollPosition;

                    for (int i = 0; i < _meshes.Count; i++)
                    {
                        var mesh = _meshes[i];
                        DrawAssetRow(mesh.Path, mesh.SizeBytes, ref mesh.Selected, mesh.Compression.ToString());
                    }
                }
            }
        }

        private void ApplyMeshOptimization()
        {
            var selected = _meshes.Where(m => m.Selected).ToList();
            if (selected.Count == 0) return;

            try
            {
                EditorUtility.DisplayProgressBar("Optimizing Meshes", "Applying settings...", 0f);

                var settings = new MeshOptimizationUtility.ModelBatchSettings
                {
                    MeshCompression = _meshCompression,
                    DisableReadWrite = false,
                    OptimizeMesh = true
                };

                int successCount = 0;
                for (int i = 0; i < selected.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Optimizing Meshes",
                        $"Processing {Path.GetFileName(selected[i].Path)}...",
                        (float)i / selected.Count);

                    if (MeshOptimizationUtility.Apply(selected[i].Path, settings, out bool changed, out _))
                    {
                        if (changed) successCount++;
                    }
                }

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Success", $"Optimized {successCount} models!", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Error", $"Failed to optimize meshes: {ex.Message}", "OK");
            }
        }

        #endregion

        #region Step: Build Settings

        private void DrawBuildSettingsStep()
        {
            DrawStepTitle("Build Settings",
                "Configure WebGL build settings for optimal size and performance.");

            EditorGUILayout.Space(10);

            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Recommended Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Development Build
            bool devBuild = EditorUserBuildSettings.development;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Development Build:", GUILayout.Width(150));
            bool newDevBuild = EditorGUILayout.Toggle(devBuild, GUILayout.Width(30));
            EditorGUILayout.LabelField(devBuild ? "ON (larger build)" : "OFF (recommended)", BridgeStyles.SubtitleStyle);
            EditorGUILayout.EndHorizontal();
            if (newDevBuild != devBuild)
                EditorUserBuildSettings.development = newDevBuild;

            EditorGUILayout.Space(5);

            // Compression
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Compression:", GUILayout.Width(150));
            string compression = PlayerSettings.WebGL.compressionFormat.ToString();
            EditorGUILayout.LabelField(compression, EditorStyles.boldLabel);
            if (compression == "Disabled")
                EditorGUILayout.LabelField("(Enable Brotli for best results)", BridgeStyles.SubtitleStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Name Files As Hashes
            bool nameAsHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Name Files As Hashes:", GUILayout.Width(150));
            bool newNameAsHashes = EditorGUILayout.Toggle(nameAsHashes, GUILayout.Width(30));
            EditorGUILayout.LabelField(nameAsHashes ? "ON (better caching)" : "OFF", BridgeStyles.SubtitleStyle);
            EditorGUILayout.EndHorizontal();
            if (newNameAsHashes != nameAsHashes)
                PlayerSettings.WebGL.nameFilesAsHashes = newNameAsHashes;

            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Quick Actions", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Open Player Settings", GUILayout.Height(30)))
            {
                SettingsService.OpenProjectSettings("Project/Player");
            }

            EditorGUILayout.Space(5);

            if (GUILayout.Button("Open Build Settings", GUILayout.Height(30)))
            {
                EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"));
            }

            BridgeStyles.EndCard();

            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Tip: For smallest builds, disable Development Build and enable Brotli compression with 'Disk Size with LTO' code optimization.",
                MessageType.Info);
        }

        #endregion

        #region Step: Complete

        private void DrawCompleteStep()
        {
            DrawStepTitle("Optimization Complete!",
                "You've completed the optimization wizard.");

            EditorGUILayout.Space(20);

            // Summary card
            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            if (_buildInfo != null && _buildInfo.HasData)
            {
                EditorGUILayout.LabelField($"Build Size: {SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes)}");
                EditorGUILayout.LabelField($"Textures: {_textures.Count} assets");
                EditorGUILayout.LabelField($"Audio: {_audioClips.Count} assets");
                EditorGUILayout.LabelField($"Meshes: {_meshes.Count} assets");
            }
            BridgeStyles.EndCard();

            EditorGUILayout.Space(20);

            // Build for Release section - MOVED UP
            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Ready for Release?", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Create the smallest possible build for publishing.",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Build for Release uses 'Disk Size with LTO' optimization.\n" +
                "This creates the smallest build but takes significantly longer to compile.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.2f, 0.7f, 0.3f); // Green for release
            if (GUILayout.Button(new GUIContent("  Build for Release  ", "Creates smallest possible WebGL build using Disk Size with LTO optimization"), GUILayout.Height(40)))
            {
                EditorApplication.delayCall += () => BuildAnalyzer.BuildForRelease();
            }
            GUI.backgroundColor = oldBg;

            BridgeStyles.EndCard();

            EditorGUILayout.Space(20);

            // Other actions - MOVED DOWN
            BridgeStyles.BeginCard();
            EditorGUILayout.LabelField("Other Actions", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Run another Build & Analyze to see your new build size after optimizations.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build & Analyze Again", GUILayout.Height(30)))
                {
                    RunBuildAndAnalyze();
                }

                if (GUILayout.Button("Open Playgama Bridge", GUILayout.Height(30)))
                {
                    BridgeWindow.ShowWindow();
                }
            }

            BridgeStyles.EndCard();
        }

        #endregion

        #region Helpers

        private void DrawStepTitle(string title, string subtitle)
        {
            EditorGUILayout.LabelField(title, EditorStyles.whiteLargeLabel);
            EditorGUILayout.LabelField(subtitle, BridgeStyles.SubtitleStyle);
        }

        private void DrawAssetRow(string path, long sizeBytes, ref bool selected, string info)
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 24);

            // Background with hover effect
            bool isHovered = rect.Contains(Event.current.mousePosition);
            Color bgColor = isHovered
                ? new Color(0.28f, 0.28f, 0.32f)  // Lighter on hover
                : new Color(0.22f, 0.22f, 0.25f); // Default grey
            EditorGUI.DrawRect(rect, bgColor);

            // Calculate layout - reserve space for buttons at the end
            float buttonWidth = 90; // Ping + Select buttons
            float checkboxWidth = 22;
            float availableWidth = rect.width - buttonWidth - checkboxWidth - 16;

            float x = rect.x + 8;

            // Clickable area for toggling (excluding buttons area)
            Rect clickableRect = new Rect(rect.x, rect.y, rect.width - buttonWidth - 8, rect.height);
            if (Event.current.type == EventType.MouseDown && clickableRect.Contains(Event.current.mousePosition))
            {
                selected = !selected;
                Event.current.Use();
            }

            // Checkbox (visual only, clicking handled above)
            EditorGUI.Toggle(new Rect(x, rect.y + 3, 18, 18), selected);
            x += checkboxWidth;

            // File name (45% of available)
            string fileName = Path.GetFileName(path);
            float nameWidth = availableWidth * 0.45f;
            EditorGUI.LabelField(new Rect(x, rect.y + 3, nameWidth, 18),
                new GUIContent(fileName, path), EditorStyles.miniLabel);
            x += nameWidth;

            // Size (fixed width)
            float sizeWidth = 70;
            EditorGUI.LabelField(new Rect(x, rect.y + 3, sizeWidth, 18),
                SharedTypes.FormatBytes(sizeBytes), EditorStyles.miniLabel);
            x += sizeWidth;

            // Info (remaining space before buttons)
            float infoWidth = availableWidth - nameWidth - sizeWidth;
            if (infoWidth > 20)
            {
                EditorGUI.LabelField(new Rect(x, rect.y + 3, infoWidth, 18), info, EditorStyles.miniLabel);
            }

            // Ping button
            Rect pingRect = new Rect(rect.x + rect.width - buttonWidth - 4, rect.y + 2, 42, rect.height - 4);
            if (GUI.Button(pingRect, new GUIContent("Ping", "Highlight asset in Project window")))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj != null) EditorGUIUtility.PingObject(obj);
            }

            // Select button
            Rect selectRect = new Rect(rect.x + rect.width - 46, rect.y + 2, 44, rect.height - 4);
            if (GUI.Button(selectRect, new GUIContent("Select", "Select asset in Project window")))
            {
                var obj = AssetDatabase.LoadMainAssetAtPath(path);
                if (obj != null) Selection.activeObject = obj;
            }
        }

        #endregion
    }
}
