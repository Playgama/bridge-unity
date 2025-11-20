using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Playgama.Editor
{
    public class PostBuildProcessor
    {
        private const string CONFIG_FILENAME = "playgama-bridge-config.json";

        [PostProcessBuild(1)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.WebGL)
            {
                return;
            }

            var configPath = FindConfigInAssets();
            if (string.IsNullOrEmpty(configPath))
            {
                return;
            }

            var buildDirectory = Path.GetDirectoryName(pathToBuiltProject);
            var destinationPath = Path.Combine(buildDirectory, CONFIG_FILENAME);

            try
            {
                File.Copy(configPath, destinationPath, true);
                Debug.Log($"[Playgama Bridge] {CONFIG_FILENAME} copied to build directory: {destinationPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Playgama Bridge] Failed to copy {CONFIG_FILENAME}: {ex.Message}");
            }
        }

        private static string FindConfigInAssets()
        {
            var guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(CONFIG_FILENAME));

            foreach (string guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileName(assetPath) == CONFIG_FILENAME)
                {
                    return assetPath;
                }
            }

            return null;
        }
    }
}
