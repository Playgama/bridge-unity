using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit
{
    /// <summary>
    /// Utility that applies a consistent set of AudioImporter settings to reduce WebGL build size.
    ///
    /// Design goals:
    /// - Best-effort compatibility across Unity versions (2019 LTS → Unity 6)
    /// - No direct references to APIs that became obsolete or moved (reflection is used where needed)
    /// - Minimal, predictable changes: only fields explicitly covered by ApplyOptions
    /// </summary>
    public static class AudioOptimizationUtility
    {
        /// <summary>
        /// Batch options for applying audio import settings.
        /// These options are applied to:
        /// - AudioImporter.forceToMono (importer-level, stable)
        /// - importer.defaultSampleSettings (if exposed)
        /// - WebGL override sample settings (if exposed)
        /// - preloadAudioData (via reflection only, because API placement differs by Unity version)
        /// </summary>
        public sealed class ApplyOptions
        {
            /// <summary>
            /// How the audio clip is loaded at runtime.
            /// Typical WebGL baseline: CompressedInMemory.
            /// </summary>
            public AudioClipLoadType LoadType = AudioClipLoadType.CompressedInMemory;

            /// <summary>
            /// If true: converts stereo to mono on import.
            /// Can reduce size; may change the feel for clips that rely on stereo.
            /// </summary>
            public bool ForceToMono = true;

            /// <summary>
            /// Compression codec used for the clip.
            /// Typical WebGL baseline: Vorbis.
            /// </summary>
            public AudioCompressionFormat CompressionFormat = AudioCompressionFormat.Vorbis;

            /// <summary>
            /// Compression quality in the Unity API range 0..1.
            /// Higher = better quality, usually larger size.
            /// </summary>
            public float Quality = 0.6f;

            /// <summary>
            /// Whether to preload audio data.
            /// This setting moved between Unity versions (importer-level vs sample settings),
            /// so it is applied via reflection only.
            /// </summary>
            public bool PreloadAudioData = true;

            /// <summary>
            /// If true: do not force sample rate changes.
            /// Currently informational: this utility does not resample.
            /// </summary>
            public bool KeepSampleRate = true;
        }

        /// <summary>
        /// Applies importer settings and platform sample settings (WebGL) best-effort.
        /// Returns true if any change was made.
        /// </summary>
        /// <param name="importer">Target audio importer to modify.</param>
        /// <param name="opt">Options to apply.</param>
        /// <param name="appliedDetails">Human-readable summary of what was requested.</param>
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

        /// <summary>
        /// Attempts to update importer.defaultSampleSettings (struct) if the property exists.
        /// Unity exposes this on many versions; reflection is used to keep compatibility.
        /// </summary>
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

        /// <summary>
        /// Attempts to update platform override sample settings for a given platform (e.g. "WebGL").
        ///
        /// Newer Unity versions provide:
        /// - GetOverrideSampleSettings(string)
        /// - SetOverrideSampleSettings(string, AudioImporterSampleSettings)
        /// and sometimes:
        /// - SetSampleSettingsOverride(string, bool)
        ///
        /// All calls are made via reflection to avoid compile errors on older versions.
        /// </summary>
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

        /// <summary>
        /// Writes the requested options into a boxed AudioImporterSampleSettings object (struct).
        ///
        /// Notes:
        /// - sampleSettingsBoxed is boxed (object) because reflection returns structs boxed.
        /// - We set fields/properties by name so we can support multiple Unity versions.
        /// - If a particular member doesn't exist, it is skipped silently.
        /// </summary>
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

        /// <summary>
        /// Applies preloadAudioData using reflection only:
        /// - Older Unity may expose AudioImporter.preloadAudioData directly
        /// - Newer Unity may store preloadAudioData on per-platform sample settings
        ///
        /// This method tries both paths and returns true if anything changed.
        /// </summary>
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

        /// <summary>
        /// Gets an instance property by name using reflection.
        /// Returns null if the property does not exist or throws.
        /// </summary>
        private static object GetProp(object obj, string name)
        {
            var p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null) return null;
            return p.GetValue(obj, null);
        }

        /// <summary>
        /// Sets an instance property by name using reflection.
        /// No-op if the property does not exist.
        /// </summary>
        private static void SetProp(object obj, string name, object value)
        {
            var p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null) return;
            p.SetValue(obj, value, null);
        }

        /// <summary>
        /// Returns true if a type has a field or property with the given name (any visibility).
        /// </summary>
        private static bool HasFieldOrProp(Type t, string name)
        {
            if (t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null) return true;
            if (t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null) return true;
            return false;
        }

        /// <summary>
        /// Sets a struct field or property by name on a boxed struct instance.
        /// Returns true if the value changed.
        ///
        /// Important: sample settings are structs, so they are boxed.
        /// Reflection writes into the boxed copy; the caller must assign it back to the importer.
        /// </summary>
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
