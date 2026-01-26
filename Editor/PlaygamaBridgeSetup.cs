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

        /// <summary>
        /// Resets the first-run state for testing purposes.
        /// </summary>
        [MenuItem("Playgama/Debug/Reset First-Run State", priority = 1000)]
        public static void ResetFirstRunState()
        {
            EditorPrefs.DeleteKey("PlaygamaBridge_InstalledVersion");
            SessionState.SetBool("PlaygamaBridge_SessionChecked", false);
            Debug.Log("[Playgama Bridge] First-run state reset. Restart Unity or recompile to trigger fresh install.");
        }

        /// <summary>
        /// Manually triggers fresh install behavior.
        /// </summary>
        [MenuItem("Playgama/Debug/Trigger Fresh Install", priority = 1001)]
        public static void TriggerFreshInstall()
        {
            Debug.Log("[Playgama Bridge] Manually triggering fresh install...");
            InstallFilesWindow.InstallAllSilently();

            // Set WebGL template
            try
            {
                string templatePath = System.IO.Path.Combine(Application.dataPath, "WebGLTemplates/Bridge/index.html");
                if (System.IO.File.Exists(templatePath))
                {
                    PlayerSettings.WebGL.template = "PROJECT:Bridge";
                    Debug.Log("[Playgama Bridge] WebGL template set to 'Bridge'");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Playgama Bridge] Failed to set template: {ex.Message}");
            }

            BridgeWindow.ShowWindow();
        }
    }

    /// <summary>
    /// Shows the Bridge window and Install Files popup on new install or update.
    /// Tracks package version in EditorPrefs to detect changes.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaygamaBridgeFirstRun
    {
        private const string VERSION_PREF_KEY = "PlaygamaBridge_InstalledVersion";
        private const string SESSION_KEY = "PlaygamaBridge_SessionChecked";

        static PlaygamaBridgeFirstRun()
        {
            // Only check once per editor session
            if (SessionState.GetBool(SESSION_KEY, false))
                return;

            SessionState.SetBool(SESSION_KEY, true);

            // Get current package version
            string currentVersion = GetPackageVersion();
            if (string.IsNullOrEmpty(currentVersion))
            {
                Debug.Log("[Playgama Bridge] Could not determine package version.");
                return;
            }

            // Get previously installed version
            string installedVersion = EditorPrefs.GetString(VERSION_PREF_KEY, "");

            Debug.Log($"[Playgama Bridge] Current version: {currentVersion}, Installed version: {(string.IsNullOrEmpty(installedVersion) ? "(none)" : installedVersion)}");

            // Check if new install or update
            if (currentVersion == installedVersion)
                return;

            // Detect fresh install (no previous version)
            bool isFreshInstall = string.IsNullOrEmpty(installedVersion);

            // Save current version
            EditorPrefs.SetString(VERSION_PREF_KEY, currentVersion);

            if (isFreshInstall)
            {
                Debug.Log("[Playgama Bridge] Fresh install detected, auto-installing templates...");
                // Fresh install: auto-install files silently and show Bridge window
                EditorApplication.delayCall += OnFreshInstall;
            }
            else
            {
                Debug.Log("[Playgama Bridge] Update detected, showing install window...");
                // Update: show Bridge window and InstallFilesWindow
                EditorApplication.delayCall += OnUpdate;
            }
        }

        private static string GetPackageVersion()
        {
            // Try to read version from package.json
            string[] possiblePaths = new[]
            {
                "Packages/com.playgama.bridge/package.json",
                Path.Combine(Application.dataPath, "../Packages/com.playgama.bridge/package.json")
            };

            foreach (var path in possiblePaths)
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
                    // Ignore errors, try next path
                }
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
                {
                    Debug.LogWarning("[Playgama Bridge] Bridge template not found, skipping template selection.");
                    return;
                }

                // Set the WebGL template to Bridge
                // Format is "PROJECT:TemplateName" for templates in Assets/WebGLTemplates/
                PlayerSettings.WebGL.template = "PROJECT:Bridge";
                Debug.Log("[Playgama Bridge] WebGL template set to 'Bridge'");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Playgama Bridge] Failed to set WebGL template: {ex.Message}");
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
