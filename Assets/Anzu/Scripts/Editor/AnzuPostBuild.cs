#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Anzu.Editor
{
    public static class AnzuPostBuild
    {
        private const string _sdkFileName = "anzu.js";
        private const string _logHeader = "ANZU - Post Build: ";

        // Update this path if the default location of 'AnzuSDK' changes.
        private const string _standardPluginkPath = "Anzu/Plugins/Native/WebGL/";

        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target == BuildTarget.WebGL)
            {
                var sdkPath = Path.Combine(Application.dataPath, _standardPluginkPath + _sdkFileName);

                if (File.Exists(sdkPath))
                {
                    var output = Path.Combine(pathToBuiltProject, _sdkFileName);

                    File.Copy(sdkPath, output, true);

                    var indexPath = Path.Combine(pathToBuiltProject, "index.html");
                    
                    if (File.Exists(indexPath))
                    {
                        var html = File.ReadAllText(indexPath);
                        var scriptTag = $"<script src=\"{_sdkFileName}\"></script>\n";

                        if (html.Contains(scriptTag) == false)
                        {
                            html = html.Replace("</body>", scriptTag + "</body>");
                            File.WriteAllText(indexPath, html);
                        }
                    }
                    else
                    {
                        Debug.LogError(_logHeader + "WebGL index.html not found at: " + indexPath);
                    }
                }
                else
                {
                    Debug.LogError(_logHeader + "Custom JS file not found at: " + sdkPath);
                }
            }
        }
    }
}

#endif
