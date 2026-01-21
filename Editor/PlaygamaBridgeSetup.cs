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
        /// Opens the Bridge Setup window (Install Files).
        /// </summary>
        [MenuItem("Playgama/Bridge Setup", priority = 0)]
        public static void ShowBridgeSetup()
        {
            Suit.InstallFilesWindow.Show();
        }

        /// <summary>
        /// Opens the Playgama Suit window.
        /// </summary>
        [MenuItem("Playgama/Suit", priority = 100)]
        public static void ShowSuit()
        {
            Suit.SuitWindow.ShowWindow();
        }
    }

    /// <summary>
    /// Shows the Suit window and Install Files popup on new install or update.
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
                return;

            // Get previously installed version
            string installedVersion = EditorPrefs.GetString(VERSION_PREF_KEY, "");

            // Check if new install or update
            if (currentVersion == installedVersion)
                return;

            // Save current version
            EditorPrefs.SetString(VERSION_PREF_KEY, currentVersion);

            // Show windows
            EditorApplication.delayCall += ShowWindows;
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

        private static void ShowWindows()
        {
            EditorApplication.delayCall += () =>
            {
                Suit.SuitWindow.ShowWindow();

                EditorApplication.delayCall += () =>
                {
                    Suit.InstallFilesWindow.Show();
                };
            };
        }
    }
}
