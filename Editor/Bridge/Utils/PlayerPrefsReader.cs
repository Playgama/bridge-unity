using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR_OSX
using System.Diagnostics;
using System.Xml;
#endif

#if UNITY_EDITOR_WIN
using Microsoft.Win32;
#endif

#if UNITY_EDITOR_LINUX
using System.IO;
using System.Xml;
#endif

namespace Playgama.Editor
{
    public enum PlayerPrefType
    {
        String,
        Int,
        Float
    }

    public struct PlayerPrefEntry
    {
        public string Key;
        public PlayerPrefType Type;
    }

    public static class PlayerPrefsReader
    {
        private static readonly Regex HashSuffixRegex = new Regex(@"_h\d+$", RegexOptions.Compiled);

        public static List<PlayerPrefEntry> ReadAllEntries()
        {
#if UNITY_EDITOR_OSX
            return ReadEntriesFromPlist();
#elif UNITY_EDITOR_WIN
            return ReadEntriesFromRegistry();
#elif UNITY_EDITOR_LINUX
            return ReadEntriesFromLinuxPrefs();
#else
            return new List<PlayerPrefEntry>();
#endif
        }

        private static string StripHashSuffix(string rawKey)
        {
            return HashSuffixRegex.Replace(rawKey, "");
        }

#if UNITY_EDITOR_OSX
        private static string GetPlistPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            var company = PlayerSettings.companyName;
            var product = PlayerSettings.productName;
            return $"{home}/Library/Preferences/unity.{company}.{product}.plist";
        }

        private static List<PlayerPrefEntry> ReadEntriesFromPlist()
        {
            var entries = new List<PlayerPrefEntry>();
            var plistPath = GetPlistPath();

            if (!System.IO.File.Exists(plistPath))
                return entries;

            try
            {
                using (var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "plutil",
                        Arguments = $"-convert xml1 -o - \"{plistPath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                })
                {
                    // Async reads prevent ReadToEnd() from blocking the timeout
                    var stdoutBuilder = new System.Text.StringBuilder();
                    var stderrBuilder = new System.Text.StringBuilder();

                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                            stdoutBuilder.AppendLine(args.Data);
                    };
                    process.ErrorDataReceived += (sender, args) =>
                    {
                        if (args.Data != null)
                            stderrBuilder.AppendLine(args.Data);
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                        UnityEngine.Debug.LogWarning("[PlayerPrefsReader] plutil timed out.");
                        return entries;
                    }

                    // Parameterless overload flushes remaining async output handlers
                    process.WaitForExit();

                    var xml = stdoutBuilder.ToString();

                    if (string.IsNullOrEmpty(xml))
                        return entries;

                    var doc = new XmlDocument();
                    doc.XmlResolver = null;
                    doc.LoadXml(xml);

                    var dict = doc.SelectSingleNode("//dict");
                    if (dict == null)
                        return entries;

                    var children = dict.ChildNodes;
                    for (int i = 0; i < children.Count - 1; i++)
                    {
                        var node = children[i];
                        if (node.Name != "key")
                            continue;

                        var rawKey = node.InnerText;
                        var key = StripHashSuffix(rawKey);

                        if (string.IsNullOrEmpty(key) || key.StartsWith("unity."))
                            continue;

                        var valueNode = children[i + 1];
                        var type = PlayerPrefType.String;

                        switch (valueNode.Name)
                        {
                            case "integer":
                                type = PlayerPrefType.Int;
                                break;
                            case "real":
                                type = PlayerPrefType.Float;
                                break;
                            case "data":
                                type = DetectTypeHeuristic(key);
                                break;
                            case "string":
                                type = PlayerPrefType.String;
                                break;
                        }

                        entries.Add(new PlayerPrefEntry { Key = key, Type = type });
                        i++;
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[PlayerPrefsReader] Failed to read plist: {e.Message}");
            }

            return entries;
        }
#endif

#if UNITY_EDITOR_WIN
        private static string GetRegistryPath()
        {
            var company = PlayerSettings.companyName;
            var product = PlayerSettings.productName;
            return $"Software\\Unity\\UnityEditor\\{company}\\{product}";
        }

        private static List<PlayerPrefEntry> ReadEntriesFromRegistry()
        {
            var entries = new List<PlayerPrefEntry>();

            try
            {
                using (var regKey = Registry.CurrentUser.OpenSubKey(GetRegistryPath()))
                {
                    if (regKey == null)
                        return entries;

                    foreach (var valueName in regKey.GetValueNames())
                    {
                        var key = StripHashSuffix(valueName);

                        if (string.IsNullOrEmpty(key))
                            continue;

                        var kind = regKey.GetValueKind(valueName);
                        var type = kind == RegistryValueKind.String
                            ? PlayerPrefType.String
                            : DetectTypeHeuristic(key);

                        entries.Add(new PlayerPrefEntry { Key = key, Type = type });
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[PlayerPrefsReader] Failed to read registry: {e.Message}");
            }

            return entries;
        }
#endif

#if UNITY_EDITOR_LINUX
        private static string GetLinuxPrefsPath()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            var company = PlayerSettings.companyName;
            var product = PlayerSettings.productName;
            return Path.Combine(home, ".config", "unity3d", company, product, "prefs");
        }

        private static List<PlayerPrefEntry> ReadEntriesFromLinuxPrefs()
        {
            var entries = new List<PlayerPrefEntry>();
            var prefsPath = GetLinuxPrefsPath();

            if (!File.Exists(prefsPath))
                return entries;

            try
            {
                var doc = new XmlDocument();
                doc.XmlResolver = null;
                doc.Load(prefsPath);

                var prefNodes = doc.SelectNodes("//unity_prefs/pref");
                if (prefNodes == null)
                    return entries;

                foreach (XmlNode node in prefNodes)
                {
                    var nameAttr = node.Attributes?["name"];
                    var typeAttr = node.Attributes?["type"];

                    if (nameAttr == null)
                        continue;

                    var key = nameAttr.Value;
                    var type = PlayerPrefType.String;

                    if (typeAttr != null)
                    {
                        switch (typeAttr.Value.ToLowerInvariant())
                        {
                            case "int":
                                type = PlayerPrefType.Int;
                                break;
                            case "float":
                                type = PlayerPrefType.Float;
                                break;
                            case "string":
                                type = PlayerPrefType.String;
                                break;
                        }
                    }

                    entries.Add(new PlayerPrefEntry { Key = key, Type = type });
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[PlayerPrefsReader] Failed to read Linux prefs: {e.Message}");
            }

            return entries;
        }
#endif

        /// <summary>
        /// Distinguishes int from float when the backing store doesn't preserve
        /// the original type. Uses IEEE 754 exponent inspection; reliable for
        /// ints in roughly [-8M, +8M] but may misclassify larger integers.
        /// </summary>
        public static PlayerPrefType DetectTypeHeuristic(string key)
        {
            var strVal = PlayerPrefs.GetString(key, "\x01\x02");
            if (strVal != "\x01\x02")
                return PlayerPrefType.String;

            var rawBits = PlayerPrefs.GetInt(key, 0);

            if (rawBits == 0)
                return PlayerPrefType.Int;

            var exponent = (rawBits >> 23) & 0xFF;

            // Exponent 0 (denormalized) or 0xFF (Inf/NaN) → almost certainly int bits
            if (exponent == 0 || exponent == 0xFF)
                return PlayerPrefType.Int;

            // Normal IEEE 754 exponent → most likely a stored float
            return PlayerPrefType.Float;
        }

        public static string GetStoragePath()
        {
#if UNITY_EDITOR_OSX
            return GetPlistPath();
#elif UNITY_EDITOR_WIN
            return "Registry: HKCU\\" + GetRegistryPath();
#elif UNITY_EDITOR_LINUX
            return GetLinuxPrefsPath();
#else
            return "Unknown platform";
#endif
        }
    }
}
