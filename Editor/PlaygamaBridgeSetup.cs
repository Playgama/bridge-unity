using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor
{
    public class PlaygamaBridgeSetup : EditorWindow, IHasCustomMenu
    {
        private const string SOURCE_TEMPLATE_PATH = "Packages/com.playgama.bridge/Runtime/WebGLTemplates/Bridge";
        private const string DESTINATION_TEMPLATE_PATH = "Assets/WebGLTemplates/Bridge";
        private const string PREFS_PREFIX = "PlaygamaBridgeSetup_AddWebGLTemplate_FileEnabled_";

        private List<TemplateFile> templateFiles = new List<TemplateFile>();
        private Vector2 scrollPosition;

        private class TemplateFile
        {
            public string relativePath;
            public string fullPath;
            public bool enabled;
        }

        [MenuItem("Playgama/Bridge Setup")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlaygamaBridgeSetup>("Playgama Bridge Setup");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnEnable()
        {
            LoadTemplateFiles();
        }

        private void LoadTemplateFiles()
        {
            templateFiles.Clear();

            var sourcePath = GetSourcePath();
            if (string.IsNullOrEmpty(sourcePath) || !Directory.Exists(sourcePath))
            {
                return;
            }

            LoadFilesRecursive(sourcePath, sourcePath);
        }

        private void LoadFilesRecursive(string currentPath, string rootPath)
        {
            foreach (string file in Directory.GetFiles(currentPath))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.EndsWith(".meta") || fileName == ".DS_Store")
                {
                    continue;
                }

                var relativePath = file.Substring(rootPath.Length + 1).Replace("\\", "/");
                var prefKey = PREFS_PREFIX + relativePath;
                var enabled = EditorPrefs.GetBool(prefKey, true);

                templateFiles.Add(new TemplateFile
                {
                    relativePath = relativePath,
                    fullPath = file,
                    enabled = enabled
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
            if (!Directory.Exists(sourcePath))
            {
                sourcePath = Path.Combine(Application.dataPath, "../Runtime/WebGLTemplates/Bridge");
                sourcePath = Path.GetFullPath(sourcePath);
            }
            return sourcePath;
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            DrawWebGLTemplateSection();
        }

        private void DrawWebGLTemplateSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("Add Bridge WebGL Template", EditorStyles.boldLabel);
            GUILayout.Space(5);

            if (templateFiles.Count == 0)
            {
                EditorGUILayout.HelpBox("Template files not found. Make sure the plugin is installed correctly.", MessageType.Warning);
                if (GUILayout.Button("Refresh"))
                {
                    LoadTemplateFiles();
                }
                EditorGUILayout.EndVertical();
                return;
            }

            var height = Mathf.Min(templateFiles.Count * 20 + 5, 150);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUI.skin.box, GUILayout.Height(height));

            foreach (var file in templateFiles)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                file.enabled = EditorGUILayout.Toggle(file.enabled, GUILayout.Width(20));
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetBool(PREFS_PREFIX + file.relativePath, file.enabled);
                }
                GUILayout.Label(file.relativePath);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            GUILayout.Space(5);

            if (GUILayout.Button("Add", GUILayout.Height(30)))
            {
                CopySelectedFiles();
            }

            EditorGUILayout.EndVertical();
        }

        private void CopySelectedFiles()
        {
            var sourcePath = GetSourcePath();
            var destinationPath = Path.GetFullPath(DESTINATION_TEMPLATE_PATH);

            if (!Directory.Exists(sourcePath))
            {
                Debug.LogError($"[Playgama Bridge] Source template directory not found: {sourcePath}");
                return;
            }

            try
            {
                var enabledFiles = templateFiles.Where(f => f.enabled).ToList();
                if (enabledFiles.Count == 0)
                {
                    EditorUtility.DisplayDialog("Playgama Bridge", "No files selected for copying.", "OK");
                    return;
                }

                if (Directory.Exists(destinationPath))
                {
                    Directory.Delete(destinationPath, true);
                }

                Directory.CreateDirectory(destinationPath);

                foreach (var file in enabledFiles)
                {
                    var destFile = Path.Combine(destinationPath, file.relativePath);
                    var destDir = Path.GetDirectoryName(destFile);

                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(file.fullPath, destFile, true);
                }

                AssetDatabase.Refresh();

                Debug.Log($"[Playgama Bridge] {enabledFiles.Count} file(s) copied successfully to: {DESTINATION_TEMPLATE_PATH}");
                EditorUtility.DisplayDialog("Playgama Bridge", $"WebGL Template copied successfully!\n{enabledFiles.Count} file(s) copied.", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Playgama Bridge] Failed to copy WebGL Template: {ex.Message}");
                EditorUtility.DisplayDialog("Error", $"Failed to copy WebGL Template:\n{ex.Message}", "OK");
            }
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Refresh"), false, LoadTemplateFiles);
        }
    }

    public class PlaygamaBridgePostprocessor : AssetPostprocessor
    {
        private const string SOURCE_TEMPLATE_PATH = "Packages/com.playgama.bridge/Runtime/WebGLTemplates/Bridge";

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (string asset in importedAssets)
            {
                if (asset.StartsWith(SOURCE_TEMPLATE_PATH))
                {
                    EditorApplication.delayCall += () =>
                    {
                        PlaygamaBridgeSetup.ShowWindow();
                    };
                    break;
                }
            }
        }
    }
}
