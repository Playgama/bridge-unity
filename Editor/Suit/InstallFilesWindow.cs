using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit
{
    /// <summary>
    /// Popup window for selecting and installing Playgama Bridge WebGL template files.
    /// Copies files from the package to Assets/WebGLTemplates/Bridge.
    /// </summary>
    public class InstallFilesWindow : EditorWindow
    {
        private const string SOURCE_TEMPLATE_PATH = "Packages/com.playgama.bridge/Runtime/WebGLTemplates/Bridge";
        private const string DESTINATION_TEMPLATE_PATH = "Assets/WebGLTemplates/Bridge";
        private const string PREFS_PREFIX = "PlaygamaBridge_InstallFile_";

        private static InstallFilesWindow _instance;
        private Vector2 _scroll;
        private List<TemplateFile> _templateFiles = new List<TemplateFile>();
        private bool _filesLoaded = false;

        private class TemplateFile
        {
            public string RelativePath;
            public string FullPath;
            public string Description;
            public bool Enabled;
        }

        // Descriptions for known files
        private static readonly Dictionary<string, string> FileDescriptions = new Dictionary<string, string>
        {
            { "index.html", "HTML template with Playgama SDK loader and game container" },
            { "playgama-bridge-config.json", "Configuration file for Playgama Bridge SDK settings" },
            { "playgama-bridge-unity.js", "Unity-specific bridge integration script" },
            { "playgama-bridge.js", "Core Playgama Bridge SDK script" },
            { "thumbnail.png", "Template thumbnail preview image" }
        };

        public static void Show()
        {
            if (_instance != null)
            {
                _instance.Focus();
                return;
            }

            _instance = CreateInstance<InstallFilesWindow>();
            _instance.titleContent = new GUIContent("Install Bridge Files");
            _instance.minSize = new Vector2(450, 400);
            _instance.maxSize = new Vector2(500, 550);
            _instance.LoadTemplateFiles();
            _instance.ShowUtility();
        }

        private void OnDestroy()
        {
            _instance = null;
        }

        private void LoadTemplateFiles()
        {
            _templateFiles.Clear();
            _filesLoaded = false;

            var sourcePath = GetSourcePath();
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                Debug.LogWarning($"[Playgama Bridge] Template source path not found: {sourcePath}");
                return;
            }

            LoadFilesRecursive(sourcePath, sourcePath);
            _filesLoaded = true;
        }

        private void LoadFilesRecursive(string currentPath, string rootPath)
        {
            // Load files in current directory
            foreach (string file in Directory.GetFiles(currentPath))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta") || fileName == ".DS_Store")
                    continue;

                var relativePath = file.Substring(rootPath.Length + 1).Replace("\\", "/");
                var prefKey = PREFS_PREFIX + relativePath;
                var enabled = EditorPrefs.GetBool(prefKey, true);

                // Get description if available
                string description = "";
                if (FileDescriptions.TryGetValue(fileName, out string desc))
                    description = desc;
                else
                    description = $"Template file: {fileName}";

                _templateFiles.Add(new TemplateFile
                {
                    RelativePath = relativePath,
                    FullPath = file,
                    Description = description,
                    Enabled = enabled
                });
            }

            // Recurse into subdirectories
            foreach (string directory in Directory.GetDirectories(currentPath))
            {
                LoadFilesRecursive(directory, rootPath);
            }
        }

        private string GetSourcePath()
        {
            // Try package path first
            var sourcePath = Path.GetFullPath(SOURCE_TEMPLATE_PATH);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            // Fallback for local development
            sourcePath = Path.Combine(Application.dataPath, "../Packages/com.playgama.bridge/Runtime/WebGLTemplates/Bridge");
            sourcePath = Path.GetFullPath(sourcePath);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            // Another fallback
            sourcePath = Path.Combine(Application.dataPath, "../../bridge-unity/Runtime/WebGLTemplates/Bridge");
            sourcePath = Path.GetFullPath(sourcePath);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            return null;
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
                GUILayout.Label("Select WebGL template files to install", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(10);

            // Purple accent line
            Rect lineRect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(lineRect, SuitStyles.BrandPurple);

            EditorGUILayout.Space(10);

            // Check if files are loaded
            if (!_filesLoaded || _templateFiles.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Template files not found. Make sure the Playgama Bridge package is installed correctly.",
                    MessageType.Warning);

                EditorGUILayout.Space(10);

                if (GUILayout.Button("Refresh", GUILayout.Height(30)))
                {
                    LoadTemplateFiles();
                }

                return;
            }

            // Destination path info
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Destination:", EditorStyles.miniLabel, GUILayout.Width(70));
                GUILayout.Label(DESTINATION_TEMPLATE_PATH, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(10);

            // File list with checkboxes
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;

                foreach (var file in _templateFiles)
                {
                    DrawFileOption(file);
                    EditorGUILayout.Space(2);
                }
            }

            EditorGUILayout.Space(10);

            // Select All / Deselect All / Refresh buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(10);

                if (GUILayout.Button("Select All", GUILayout.Width(90)))
                {
                    foreach (var file in _templateFiles)
                    {
                        file.Enabled = true;
                        EditorPrefs.SetBool(PREFS_PREFIX + file.RelativePath, true);
                    }
                }

                if (GUILayout.Button("Deselect All", GUILayout.Width(90)))
                {
                    foreach (var file in _templateFiles)
                    {
                        file.Enabled = false;
                        EditorPrefs.SetBool(PREFS_PREFIX + file.RelativePath, false);
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                {
                    LoadTemplateFiles();
                }

                GUILayout.Space(10);
            }

            GUILayout.FlexibleSpace();

            // Status: how many files selected
            int selectedCount = _templateFiles.Count(f => f.Enabled);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{selectedCount} of {_templateFiles.Count} files selected", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(10);

            // Bottom buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    Close();
                }

                GUILayout.Space(10);

                // Install button with accent color
                GUI.enabled = selectedCount > 0;
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = SuitStyles.BrandPurple;

                if (GUILayout.Button("Install Selected", GUILayout.Width(130), GUILayout.Height(30)))
                {
                    PerformInstallation();
                }

                GUI.backgroundColor = oldBg;
                GUI.enabled = true;

                GUILayout.Space(10);
            }

            EditorGUILayout.Space(15);
        }

        private void DrawFileOption(TemplateFile file)
        {
            Rect boxRect = EditorGUILayout.GetControlRect(false, 44);

            // Background
            Color bgColor = file.Enabled
                ? new Color(0.25f, 0.22f, 0.30f)  // Slight purple tint when selected
                : new Color(0.22f, 0.22f, 0.25f);
            EditorGUI.DrawRect(boxRect, bgColor);

            // Left accent when enabled
            if (file.Enabled)
            {
                Rect accentRect = new Rect(boxRect.x, boxRect.y, 3, boxRect.height);
                EditorGUI.DrawRect(accentRect, SuitStyles.BrandPurple);
            }

            // Checkbox
            Rect toggleRect = new Rect(boxRect.x + 10, boxRect.y + 12, 20, 20);
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUI.Toggle(toggleRect, file.Enabled);
            if (EditorGUI.EndChangeCheck())
            {
                file.Enabled = newEnabled;
                EditorPrefs.SetBool(PREFS_PREFIX + file.RelativePath, newEnabled);
            }

            // File name
            string fileName = Path.GetFileName(file.RelativePath);
            Rect nameRect = new Rect(boxRect.x + 35, boxRect.y + 6, boxRect.width - 45, 18);
            EditorGUI.LabelField(nameRect, fileName, EditorStyles.boldLabel);

            // Description
            Rect descRect = new Rect(boxRect.x + 35, boxRect.y + 24, boxRect.width - 45, 16);
            EditorGUI.LabelField(descRect, file.Description, EditorStyles.miniLabel);
        }

        private void PerformInstallation()
        {
            var enabledFiles = _templateFiles.Where(f => f.Enabled).ToList();

            if (enabledFiles.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "No Files Selected",
                    "Please select at least one file to install.",
                    "OK");
                return;
            }

            var destinationPath = Path.GetFullPath(DESTINATION_TEMPLATE_PATH);

            EditorUtility.DisplayProgressBar("Installing Files", "Preparing installation...", 0f);

            try
            {
                // Create destination directory if needed
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                int total = enabledFiles.Count;
                int current = 0;
                int successCount = 0;

                foreach (var file in enabledFiles)
                {
                    current++;
                    float progress = (float)current / total;
                    string fileName = Path.GetFileName(file.RelativePath);
                    EditorUtility.DisplayProgressBar("Installing Files", $"Installing {fileName}...", progress);

                    try
                    {
                        var destFile = Path.Combine(destinationPath, file.RelativePath);
                        var destDir = Path.GetDirectoryName(destFile);

                        // Create subdirectory if needed
                        if (!Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        // Special handling for config file - create backup if exists
                        if (fileName == "playgama-bridge-config.json" && File.Exists(destFile))
                        {
                            var backupFile = Path.Combine(destDir, "playgama-bridge-config_backup.json");

                            // Delete old backup if exists
                            if (File.Exists(backupFile))
                            {
                                File.Delete(backupFile);
                            }

                            // Rename current config to backup
                            File.Move(destFile, backupFile);
                            Debug.Log($"[Playgama Bridge] Backed up existing config to: playgama-bridge-config_backup.json");
                        }
                        else if (File.Exists(destFile))
                        {
                            // Delete existing file for non-config files
                            File.Delete(destFile);
                        }

                        // Copy file
                        File.Copy(file.FullPath, destFile);
                        successCount++;

                        Debug.Log($"[Playgama Bridge] Installed: {file.RelativePath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Playgama Bridge] Failed to install {file.RelativePath}: {ex.Message}");
                    }
                }

                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();

                if (successCount == total)
                {
                    EditorUtility.DisplayDialog(
                        "Installation Complete",
                        $"Successfully installed {successCount} file(s) to:\n{DESTINATION_TEMPLATE_PATH}\n\n" +
                        "You can now select the Bridge template in:\n" +
                        "Player Settings → WebGL → Resolution and Presentation",
                        "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Installation Partially Complete",
                        $"Installed {successCount} of {total} file(s).\n" +
                        "Check the Console for error details.",
                        "OK");
                }

                Close();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Playgama Bridge] Installation failed: {ex.Message}");
                EditorUtility.DisplayDialog(
                    "Installation Error",
                    $"An error occurred during installation:\n\n{ex.Message}",
                    "OK");
            }
        }
    }
}
