using System.IO;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor
{
    /// <summary>
    /// Provides menu items for Playgama Bridge setup.
    /// </summary>
    public static class PlaygamaBridgeSetup
    {
        /// <summary>
        /// Opens the Playgama Bridge window.
        /// </summary>
        [MenuItem("Playgama/Bridge", priority = 0)]
        public static void ShowBridge()
        {
            BridgeWindow.ShowWindow();
        }

        /// <summary>
        /// Opens the Install Template Files window.
        /// </summary>
        [MenuItem("Playgama/Install Template Files", priority = 100)]
        public static void ShowBridgeSetup()
        {
            InstallFilesWindow.Show();
        }

    }

    /// <summary>
    /// Shows the Bridge window and Install Files popup on new install or update.
    /// Tracks package version per-project using a file in Library folder.
    /// Cleans up when package is removed.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaygamaBridgeFirstRun
    {
        private const string SESSION_KEY = "PlaygamaBridge_SessionChecked";
        private const string PACKAGE_NAME = "com.playgama.bridge";

        // Project-specific version tracking file (Library folder is per-project and not in version control)
        private static readonly string VERSION_FILE_PATH = Path.Combine(Application.dataPath, "../Library/PlaygamaBridge.version");

        private static bool _eventsSubscribed = false;

        static PlaygamaBridgeFirstRun()
        {
            // Defer event subscription to avoid ScriptableSingleton errors
            EditorApplication.delayCall += SubscribeToPackageEvents;

            // Only check once per editor session
            if (SessionState.GetBool(SESSION_KEY, false))
                return;

            SessionState.SetBool(SESSION_KEY, true);

            // Get current package version
            string currentVersion = GetPackageVersion();
            if (string.IsNullOrEmpty(currentVersion))
                return;

            // Get previously installed version (project-specific)
            string installedVersion = GetInstalledVersion();

            // Check if new install or update
            if (currentVersion == installedVersion)
                return;

            // Detect fresh install (no previous version)
            bool isFreshInstall = string.IsNullOrEmpty(installedVersion);

            // Save current version (project-specific)
            SaveInstalledVersion(currentVersion);

            if (isFreshInstall)
            {
                // Fresh install: auto-install files silently and show Bridge window
                EditorApplication.delayCall += OnFreshInstall;
            }
            else
            {
                // Update: show Bridge window and InstallFilesWindow
                EditorApplication.delayCall += OnUpdate;
            }
        }

        private static string GetInstalledVersion()
        {
            try
            {
                string fullPath = Path.GetFullPath(VERSION_FILE_PATH);
                if (File.Exists(fullPath))
                {
                    return File.ReadAllText(fullPath).Trim();
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        private static void SaveInstalledVersion(string version)
        {
            try
            {
                string fullPath = Path.GetFullPath(VERSION_FILE_PATH);
                File.WriteAllText(fullPath, version);
            }
            catch
            {
                // Ignore errors
            }
        }

        private static void DeleteVersionFile()
        {
            try
            {
                string fullPath = Path.GetFullPath(VERSION_FILE_PATH);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        private static void SubscribeToPackageEvents()
        {
            if (_eventsSubscribed)
                return;

            _eventsSubscribed = true;

            try
            {
                UnityEditor.PackageManager.Events.registeringPackages += OnPackagesChanging;
            }
            catch
            {
                // Ignore errors
            }
        }

        private static void OnPackagesChanging(UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            // Check if our package is being removed
            foreach (var packageInfo in args.removed)
            {
                if (packageInfo.name == PACKAGE_NAME)
                {
                    CleanupOnUninstall();
                    break;
                }
            }
        }

        private static void CleanupOnUninstall()
        {
            // Remove project-specific version file
            DeleteVersionFile();

            // Remove install file preferences (they start with this prefix)
            // Note: EditorPrefs doesn't have a way to enumerate keys with a prefix,
            // so we delete the known keys
            string[] knownFiles = new[]
            {
                "index.html",
                "playgama-bridge-config.json",
                "playgama-bridge-unity.js",
                "playgama-bridge.js",
                "thumbnail.png"
            };

            foreach (var file in knownFiles)
            {
                EditorPrefs.DeleteKey("PlaygamaBridge_InstallFile_" + file);
            }
        }

        private static string GetPackageVersion()
        {
            // Method 1: Use Unity's PackageInfo API (most reliable)
            try
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.playgama.bridge/package.json");
                if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.version))
                    return packageInfo.version;
            }
            catch
            {
                // PackageManager API might not be available
            }

            // Method 2: Try known file paths
            string[] possiblePaths = new[]
            {
                "Packages/com.playgama.bridge/package.json",
                Path.Combine(Application.dataPath, "../Packages/com.playgama.bridge/package.json")
            };

            foreach (var path in possiblePaths)
            {
                string version = TryReadVersionFromPath(path);
                if (!string.IsNullOrEmpty(version))
                    return version;
            }

            // Method 3: Find package.json relative to this script's location
            // This handles local development setups
            try
            {
                var scriptPath = GetScriptPath();
                if (!string.IsNullOrEmpty(scriptPath))
                {
                    string editorDir = Path.GetDirectoryName(scriptPath);
                    string packageRoot = Path.GetDirectoryName(editorDir);
                    string packageJsonPath = Path.Combine(packageRoot, "package.json");

                    string version = TryReadVersionFromPath(packageJsonPath);
                    if (!string.IsNullOrEmpty(version))
                        return version;
                }
            }
            catch
            {
                // Ignore errors
            }

            return null;
        }

        private static string GetScriptPath()
        {
            // Find this script's path using the MonoScript API
            var guids = AssetDatabase.FindAssets("t:MonoScript PlaygamaBridgeSetup");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("PlaygamaBridgeSetup.cs"))
                    return Path.GetFullPath(path);
            }
            return null;
        }

        private static string TryReadVersionFromPath(string path)
        {
            try
            {
                string fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                {
                    string json = File.ReadAllText(fullPath);
                    // Simple parsing - find "version": "x.x.x"
                    int versionIndex = json.IndexOf("\"version\"");
                    if (versionIndex >= 0)
                    {
                        int colonIndex = json.IndexOf(":", versionIndex);
                        int startQuote = json.IndexOf("\"", colonIndex + 1);
                        int endQuote = json.IndexOf("\"", startQuote + 1);
                        if (startQuote >= 0 && endQuote > startQuote)
                        {
                            return json.Substring(startQuote + 1, endQuote - startQuote - 1);
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        }

        private static void OnFreshInstall()
        {
            // Auto-install template files silently
            InstallFilesWindow.InstallAllSilently();

            // Set WebGL template to Bridge
            EditorApplication.delayCall += () =>
            {
                SetWebGLTemplate();

                // Show Bridge window
                EditorApplication.delayCall += () =>
                {
                    BridgeWindow.ShowWindow();
                };
            };
        }

        private static void SetWebGLTemplate()
        {
            try
            {
                // Check if Bridge template was installed
                string templatePath = Path.Combine(Application.dataPath, "WebGLTemplates/Bridge/index.html");
                if (!File.Exists(templatePath))
                    return;

                // Set the WebGL template to Bridge
                PlayerSettings.WebGL.template = "PROJECT:Bridge";
            }
            catch
            {
                // Ignore errors
            }
        }

        private static void OnUpdate()
        {
            // Show Bridge window and InstallFilesWindow on update
            EditorApplication.delayCall += () =>
            {
                BridgeWindow.ShowWindow();

                EditorApplication.delayCall += () =>
                {
                    InstallFilesWindow.Show();
                };
            };
        }
    }
}
