using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor
{
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

        private static readonly Dictionary<string, string> FileDescriptions = new Dictionary<string, string>
        {
            { "index.html", "HTML template with Playgama SDK loader and game container" },
            { "playgama-bridge-config.json", "Configuration file for Playgama Bridge SDK settings" },
            { "playgama-bridge-unity.js", "Unity-specific bridge integration script" },
            { "playgama-bridge.js", "Core Playgama Bridge SDK script" },
            { "thumbnail.png", "Template thumbnail preview image" }
        };

        public static new void Show()
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

        /// <summary>
        /// Silently installs all template files without showing the window.
        /// Used for first-time fresh installs.
        /// </summary>
        public static void InstallAllSilently()
        {
            var sourcePath = GetSourcePathStatic();
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                Debug.LogWarning("[Playgama Bridge] Template source path not found, skipping silent install.");
                return;
            }

            var destinationPath = Path.GetFullPath(DESTINATION_TEMPLATE_PATH);

            try
            {
                if (!Directory.Exists(destinationPath))
                {
                    Directory.CreateDirectory(destinationPath);
                }

                int successCount = 0;
                InstallFilesRecursive(sourcePath, sourcePath, destinationPath, ref successCount);

                AssetDatabase.Refresh();

                if (successCount > 0)
                {
                    Debug.Log($"[Playgama Bridge] Auto-installed {successCount} template file(s) to {DESTINATION_TEMPLATE_PATH}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Playgama Bridge] Silent installation failed: {ex.Message}");
            }
        }

        private static string GetSourcePathStatic()
        {
            var sourcePath = Path.GetFullPath(SOURCE_TEMPLATE_PATH);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            sourcePath = Path.Combine(Application.dataPath, "../Packages/com.playgama.bridge/Runtime/WebGLTemplates/Bridge");
            sourcePath = Path.GetFullPath(sourcePath);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            sourcePath = Path.Combine(Application.dataPath, "../../bridge-unity/Runtime/WebGLTemplates/Bridge");
            sourcePath = Path.GetFullPath(sourcePath);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            return null;
        }

        private static void InstallFilesRecursive(string currentPath, string rootPath, string destRoot, ref int successCount)
        {
            foreach (string file in Directory.GetFiles(currentPath))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta") || fileName == ".DS_Store")
                    continue;

                var relativePath = file.Substring(rootPath.Length + 1).Replace("\\", "/");

                try
                {
                    var destFile = Path.Combine(destRoot, relativePath);
                    var destDir = Path.GetDirectoryName(destFile);

                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    // Don't overwrite existing files during silent install
                    if (!File.Exists(destFile))
                    {
                        File.Copy(file, destFile);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Playgama Bridge] Failed to install {relativePath}: {ex.Message}");
                }
            }

            foreach (string directory in Directory.GetDirectories(currentPath))
            {
                InstallFilesRecursive(directory, rootPath, destRoot, ref successCount);
            }
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
            foreach (string file in Directory.GetFiles(currentPath))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta") || fileName == ".DS_Store")
                    continue;

                var relativePath = file.Substring(rootPath.Length + 1).Replace("\\", "/");
                var prefKey = PREFS_PREFIX + relativePath;

                // Config file defaults to unchecked to protect user customizations during updates
                bool defaultEnabled = fileName != "playgama-bridge-config.json";
                var enabled = EditorPrefs.GetBool(prefKey, defaultEnabled);

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

            foreach (string directory in Directory.GetDirectories(currentPath))
            {
                LoadFilesRecursive(directory, rootPath);
            }
        }

        private string GetSourcePath()
        {
            var sourcePath = Path.GetFullPath(SOURCE_TEMPLATE_PATH);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            sourcePath = Path.Combine(Application.dataPath, "../Packages/com.playgama.bridge/Runtime/WebGLTemplates/Bridge");
            sourcePath = Path.GetFullPath(sourcePath);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            sourcePath = Path.Combine(Application.dataPath, "../../bridge-unity/Runtime/WebGLTemplates/Bridge");
            sourcePath = Path.GetFullPath(sourcePath);
            if (Directory.Exists(sourcePath))
                return sourcePath;

            return null;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Install Playgama Bridge Files", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(5);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Select WebGL template files to install", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(10);

            Rect lineRect = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(lineRect, BridgeStyles.brandPurple);

            EditorGUILayout.Space(10);

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

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Destination:", EditorStyles.miniLabel, GUILayout.Width(70));
                GUILayout.Label(DESTINATION_TEMPLATE_PATH, EditorStyles.miniLabel);
            }

            EditorGUILayout.Space(10);

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

            int selectedCount = _templateFiles.Count(f => f.Enabled);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{selectedCount} of {_templateFiles.Count} files selected", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(10);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(30)))
                {
                    Close();
                }

                GUILayout.Space(10);

                GUI.enabled = selectedCount > 0;
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = BridgeStyles.brandPurple;

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

            Color bgColor = file.Enabled
                ? new Color(0.25f, 0.22f, 0.30f)
                : new Color(0.22f, 0.22f, 0.25f);
            EditorGUI.DrawRect(boxRect, bgColor);

            if (file.Enabled)
            {
                Rect accentRect = new Rect(boxRect.x, boxRect.y, 3, boxRect.height);
                EditorGUI.DrawRect(accentRect, BridgeStyles.brandPurple);
            }

            Rect toggleRect = new Rect(boxRect.x + 10, boxRect.y + 12, 20, 20);
            EditorGUI.BeginChangeCheck();
            bool newEnabled = EditorGUI.Toggle(toggleRect, file.Enabled);
            if (EditorGUI.EndChangeCheck())
            {
                file.Enabled = newEnabled;
                EditorPrefs.SetBool(PREFS_PREFIX + file.RelativePath, newEnabled);
            }

            string fileName = Path.GetFileName(file.RelativePath);
            Rect nameRect = new Rect(boxRect.x + 35, boxRect.y + 6, boxRect.width - 45, 18);
            EditorGUI.LabelField(nameRect, fileName, EditorStyles.boldLabel);

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

                        if (!Directory.Exists(destDir))
                        {
                            Directory.CreateDirectory(destDir);
                        }

                        // Backup existing config file before overwriting
                        if (fileName == "playgama-bridge-config.json" && File.Exists(destFile))
                        {
                            var backupFile = Path.Combine(destDir, "playgama-bridge-config_backup.json");

                            if (File.Exists(backupFile))
                            {
                                File.Delete(backupFile);
                            }

                            File.Move(destFile, backupFile);
                            Debug.Log($"[Playgama Bridge] Backed up existing config to: playgama-bridge-config_backup.json");
                        }
                        else if (File.Exists(destFile))
                        {
                            File.Delete(destFile);
                        }

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
