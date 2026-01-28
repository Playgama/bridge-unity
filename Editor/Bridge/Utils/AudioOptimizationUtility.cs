using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor
{
    // Applies AudioImporter settings to reduce WebGL build size using reflection for cross-version compatibility
    public static class AudioOptimizationUtility
    {
        public sealed class ApplyOptions
        {
            public AudioClipLoadType LoadType = AudioClipLoadType.CompressedInMemory;
            public bool ForceToMono = true;
            public AudioCompressionFormat CompressionFormat = AudioCompressionFormat.Vorbis;
            public float Quality = 0.6f;
            public bool PreloadAudioData = true;
            public bool KeepSampleRate = true;
        }

        public static bool ApplyToImporter(AudioImporter importer, ApplyOptions opt, out string appliedDetails)
        {
            appliedDetails = "";
            if (importer == null) return false;

            bool changed = false;

            if (importer.forceToMono != opt.ForceToMono)
            {
                importer.forceToMono = opt.ForceToMono;
                changed = true;
            }

            changed |= TryApplySampleSettings_Default(importer, opt);
            changed |= TryApplySampleSettings_Platform(importer, "WebGL", opt);
            changed |= TrySetPreloadAudioData_Reflection(importer, opt.PreloadAudioData, platform: "WebGL");

            appliedDetails =
                $"LoadType={opt.LoadType}, ForceToMono={opt.ForceToMono}, Format={opt.CompressionFormat}, Quality={opt.Quality:0.00}, Preload={opt.PreloadAudioData}";

            return changed;
        }

        private static bool TryApplySampleSettings_Default(AudioImporter importer, ApplyOptions opt)
        {
            try
            {
                object sampleSettings = GetProp(importer, "defaultSampleSettings");
                if (sampleSettings == null) return false;

                bool changed = ApplyToSampleSettingsObject(sampleSettings, opt);

                if (changed)
                {
                    SetProp(importer, "defaultSampleSettings", sampleSettings);
                    return true;
                }
            }
            catch { }

            return false;
        }

        private static bool TryApplySampleSettings_Platform(AudioImporter importer, string platform, ApplyOptions opt)
        {
            try
            {
                var t = importer.GetType();

                MethodInfo getOverride = t.GetMethod(
                    "GetOverrideSampleSettings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);

                MethodInfo setOverride = t.GetMethod(
                    "SetOverrideSampleSettings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string), GetAudioImporterSampleSettingsType() },
                    null);

                MethodInfo setOverrideFlag = t.GetMethod(
                    "SetSampleSettingsOverride",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string), typeof(bool) },
                    null);

                if (getOverride == null || setOverride == null)
                    return false;

                if (setOverrideFlag != null)
                {
                    try { setOverrideFlag.Invoke(importer, new object[] { platform, true }); }
                    catch { }
                }

                object sampleSettings = getOverride.Invoke(importer, new object[] { platform });
                if (sampleSettings == null) return false;

                bool changed = ApplyToSampleSettingsObject(sampleSettings, opt);

                if (changed)
                {
                    setOverride.Invoke(importer, new object[] { platform, sampleSettings });
                    return true;
                }
            }
            catch { }

            return false;
        }

        // Applies options to a boxed AudioImporterSampleSettings struct
        private static bool ApplyToSampleSettingsObject(object sampleSettingsBoxed, ApplyOptions opt)
        {
            if (sampleSettingsBoxed == null) return false;

            bool changed = false;
            Type ssType = sampleSettingsBoxed.GetType();

            changed |= SetFieldOrProp(ref sampleSettingsBoxed, ssType, "loadType", opt.LoadType);
            changed |= SetFieldOrProp(ref sampleSettingsBoxed, ssType, "compressionFormat", opt.CompressionFormat);
            changed |= SetFieldOrProp(ref sampleSettingsBoxed, ssType, "quality", opt.Quality);

            if (HasFieldOrProp(ssType, "preloadAudioData"))
                changed |= SetFieldOrProp(ref sampleSettingsBoxed, ssType, "preloadAudioData", opt.PreloadAudioData);

            return changed;
        }

        // Tries both importer-level and per-platform sample settings for preloadAudioData
        private static bool TrySetPreloadAudioData_Reflection(AudioImporter importer, bool value, string platform)
        {
            if (importer == null) return false;

            bool changed = false;

            try
            {
                var prop = importer.GetType().GetProperty("preloadAudioData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
                {
                    bool cur = (bool)prop.GetValue(importer, null);
                    if (cur != value)
                    {
                        prop.SetValue(importer, value, null);
                        changed = true;
                    }
                    return changed;
                }
            }
            catch { }

            try
            {
                var t = importer.GetType();

                MethodInfo getOverride = t.GetMethod(
                    "GetOverrideSampleSettings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string) },
                    null);

                MethodInfo setOverride = t.GetMethod(
                    "SetOverrideSampleSettings",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(string), GetAudioImporterSampleSettingsType() },
                    null);

                if (getOverride == null || setOverride == null) return changed;

                object ss = getOverride.Invoke(importer, new object[] { platform });
                if (ss == null) return changed;

                Type ssType = ss.GetType();
                if (!HasFieldOrProp(ssType, "preloadAudioData")) return changed;

                object boxed = ss;
                bool localChanged = SetFieldOrProp(ref boxed, ssType, "preloadAudioData", value);
                if (localChanged)
                {
                    setOverride.Invoke(importer, new object[] { platform, boxed });
                    changed = true;
                }
            }
            catch { }

            return changed;
        }

        private static Type GetAudioImporterSampleSettingsType()
        {
            var asm = typeof(AudioImporter).Assembly;
            return asm.GetType("UnityEditor.AudioImporterSampleSettings");
        }

        private static object GetProp(object obj, string name)
        {
            var p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null) return null;
            return p.GetValue(obj, null);
        }

        private static void SetProp(object obj, string name, object value)
        {
            var p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null) return;
            p.SetValue(obj, value, null);
        }

        private static bool HasFieldOrProp(Type t, string name)
        {
            if (t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null) return true;
            if (t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null) return true;
            return false;
        }

        // Sets a field or property on a boxed struct, returns true if value changed
        private static bool SetFieldOrProp<T>(ref object boxedStruct, Type structType, string name, T value)
        {
            try
            {
                var f = structType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    object cur = f.GetValue(boxedStruct);
                    if (!Equals(cur, value))
                    {
                        f.SetValue(boxedStruct, value);
                        return true;
                    }
                    return false;
                }

                var p = structType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.CanWrite)
                {
                    object cur = p.GetValue(boxedStruct, null);
                    if (!Equals(cur, value))
                    {
                        p.SetValue(boxedStruct, value, null);
                        return true;
                    }
                    return false;
                }
            }
            catch { }

            return false;
        }
    }
}
