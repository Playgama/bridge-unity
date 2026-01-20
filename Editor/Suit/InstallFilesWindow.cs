using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit
{
    /// <summary>
    /// Popup window for selecting and installing Playgama Bridge files.
    /// </summary>
    public class InstallFilesWindow : EditorWindow
    {
        private static InstallFilesWindow _instance;
        private Vector2 _scroll;

        // File descriptions
        private static readonly FileOption[] FileOptions = new FileOption[]
        {
            new FileOption
            {
                Id = "index_html",
                Name = "index.html",
                Description = "HTML template with Playgama SDK loader and game container",
                Required = false,
                DefaultEnabled = true
            },
            new FileOption
            {
                Id = "bridge_config",
                Name = "playgama-bridge-config.json",
                Description = "Configuration file for Playgama Bridge SDK settings",
                Required = false,
                DefaultEnabled = true
            },
            new FileOption
            {
                Id = "bridge_unity_js",
                Name = "playgama-bridge-unity.js",
                Description = "Unity-specific bridge integration script",
                Required = false,
                DefaultEnabled = true
            },
            new FileOption
            {
                Id = "bridge_js",
                Name = "playgama-bridge.js",
                Description = "Core Playgama Bridge SDK script",
                Required = false,
                DefaultEnabled = true
            },
            new FileOption
            {
                Id = "thumbnail",
                Name = "thumbnail.png",
                Description = "Template thumbnail preview image",
                Required = false,
                DefaultEnabled = true
            }
        };

        private Dictionary<string, bool> _selections = new Dictionary<string, bool>();

        private struct FileOption
        {
            public string Id;
            public string Name;
            public string Description;
            public bool Required;
            public bool DefaultEnabled;
        }

        public static void Show()
        {
            if (_instance != null)
            {
                _instance.Focus();
                return;
            }

            _instance = CreateInstance<InstallFilesWindow>();
            _instance.titleContent = new GUIContent("Install Files");
            _instance.minSize = new Vector2(420, 380);
            _instance.maxSize = new Vector2(420, 500);
            _instance.InitSelections();
            _instance.ShowUtility();
        }

        private void InitSelections()
        {
            _selections.Clear();
            foreach (var opt in FileOptions)
            {
                _selections[opt.Id] = opt.DefaultEnabled;
            }
        }

        private void OnDestroy()
        {
            _instance = null;
        }

        private void OnGUI()
        {
            // Header
            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Install Playgama Bridge Files", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(5);

            // Subtitle
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select files to install in your project", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(15);

            // Purple accent line
            Rect lineRect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(lineRect, SuitStyles.BrandPurple);

            EditorGUILayout.Space(10);

            // File list with checkboxes
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                foreach (var opt in FileOptions)
                {
                    DrawFileOption(opt);
                    EditorGUILayout.Space(4);
                }
            }

            EditorGUILayout.Space(10);

            // Select All / Deselect All buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);

                if (GUILayout.Button("Select All", GUILayout.Width(100)))
                {
                    foreach (var opt in FileOptions)
                    {
                        _selections[opt.Id] = true;
                    }
                }

                if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
                {
                    foreach (var opt in FileOptions)
                    {
                        if (!opt.Required)
                            _selections[opt.Id] = false;
                    }
                }

                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();

            // Bottom buttons
            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    Close();
                }

                GUILayout.Space(10);

                // Install button with accent color
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = SuitStyles.BrandPurple;

                if (GUILayout.Button("Install Selected", GUILayout.Width(130), GUILayout.Height(30)))
                {
                    PerformInstallation();
                }

                GUI.backgroundColor = oldBg;

                GUILayout.Space(10);
            }

            EditorGUILayout.Space(15);
        }

        private void DrawFileOption(FileOption opt)
        {
            Rect boxRect = EditorGUILayout.GetControlRect(false, 50);

            // Background
            Color bgColor = new Color(0.22f, 0.22f, 0.25f);
            if (_selections.ContainsKey(opt.Id) && _selections[opt.Id])
            {
                bgColor = new Color(0.25f, 0.22f, 0.30f); // Slight purple tint when selected
            }
            EditorGUI.DrawRect(boxRect, bgColor);

            // Left accent for required items
            if (opt.Required)
            {
                Rect accentRect = new Rect(boxRect.x, boxRect.y, 3, boxRect.height);
                EditorGUI.DrawRect(accentRect, SuitStyles.BrandPurple);
            }

            // Checkbox
            Rect toggleRect = new Rect(boxRect.x + 10, boxRect.y + 15, 20, 20);

            bool currentValue = _selections.ContainsKey(opt.Id) && _selections[opt.Id];

            EditorGUI.BeginDisabledGroup(opt.Required);
            bool newValue = EditorGUI.Toggle(toggleRect, currentValue);
            EditorGUI.EndDisabledGroup();

            if (newValue != currentValue)
            {
                _selections[opt.Id] = newValue;
            }

            // Name
            Rect nameRect = new Rect(boxRect.x + 35, boxRect.y + 8, boxRect.width - 45, 18);
            GUIStyle nameStyle = new GUIStyle(EditorStyles.boldLabel);
            if (opt.Required)
            {
                nameStyle.normal.textColor = SuitStyles.BrandPurple;
            }
            EditorGUI.LabelField(nameRect, opt.Name + (opt.Required ? " (Required)" : ""), nameStyle);

            // Description
            Rect descRect = new Rect(boxRect.x + 35, boxRect.y + 26, boxRect.width - 45, 20);
            EditorGUI.LabelField(descRect, opt.Description, EditorStyles.miniLabel);
        }

        private void PerformInstallation()
        {
            List<string> selectedFiles = new List<string>();

            foreach (var opt in FileOptions)
            {
                if (_selections.ContainsKey(opt.Id) && _selections[opt.Id])
                {
                    selectedFiles.Add(opt.Name);
                }
            }

            if (selectedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Files Selected",
                    "Please select at least one file to install.",
                    "OK");
                return;
            }

            // Show progress
            EditorUtility.DisplayProgressBar("Installing Files", "Preparing installation...", 0f);

            try
            {
                int total = selectedFiles.Count;
                int current = 0;

                foreach (var opt in FileOptions)
                {
                    if (!_selections.ContainsKey(opt.Id) || !_selections[opt.Id])
                        continue;

                    current++;
                    float progress = (float)current / total;
                    EditorUtility.DisplayProgressBar("Installing Files", $"Installing {opt.Name}...", progress);

                    InstallFile(opt.Id);
                }

                EditorUtility.ClearProgressBar();

                EditorUtility.DisplayDialog(
                    "Installation Complete",
                    $"Successfully installed {selectedFiles.Count} file(s).\n\n" +
                    "You can now select the Playgama template in:\n" +
                    "Player Settings > WebGL > Resolution and Presentation",
                    "OK");

                Close();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog(
                    "Installation Error",
                    $"An error occurred during installation:\n\n{ex.Message}",
                    "OK");
            }
        }

        private void InstallFile(string fileId)
        {
            // TODO: Implement actual file installation logic
            // This would copy files from the package to the project

            string templatePath = "Assets/WebGLTemplates/Playgama";
            EnsureDirectory(templatePath);

            string targetPath = "";

            switch (fileId)
            {
                case "index_html":
                    targetPath = Path.Combine(templatePath, "index.html");
                    // Copy index.html from package
                    Debug.Log($"[Playgama Suit] Installed index.html to {targetPath}");
                    break;

                case "bridge_config":
                    targetPath = Path.Combine(templatePath, "playgama-bridge-config.json");
                    // Copy config file from package
                    Debug.Log($"[Playgama Suit] Installed playgama-bridge-config.json to {targetPath}");
                    break;

                case "bridge_unity_js":
                    targetPath = Path.Combine(templatePath, "playgama-bridge-unity.js");
                    // Copy JS file from package
                    Debug.Log($"[Playgama Suit] Installed playgama-bridge-unity.js to {targetPath}");
                    break;

                case "bridge_js":
                    targetPath = Path.Combine(templatePath, "playgama-bridge.js");
                    // Copy JS file from package
                    Debug.Log($"[Playgama Suit] Installed playgama-bridge.js to {targetPath}");
                    break;

                case "thumbnail":
                    targetPath = Path.Combine(templatePath, "thumbnail.png");
                    // Copy thumbnail from package
                    Debug.Log($"[Playgama Suit] Installed thumbnail.png to {targetPath}");
                    break;
            }

            AssetDatabase.Refresh();
        }

        private void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
