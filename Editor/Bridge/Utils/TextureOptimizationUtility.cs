using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor
{
    public enum RowQuality
    {
        Green,
        Yellow,
        Red
    }

    public struct TextureRowData
    {
        public string Path;
        public long SizeBytes;
        public bool IsEstimated;

        public int SrcWidth;
        public int SrcHeight;

        public int MaxSizeWebGL;
        public TextureImporterCompression CompressionWebGL;
        public bool CrunchWebGL;
        public int CompressionQualityWebGL;

        public bool ReadWrite;
    }

    public struct TextureBatchSettings
    {
        public int MaxSizeWebGL;
        public TextureImporterCompression CompressionWebGL;
        public bool CrunchWebGL;
        public int CrunchQualityWebGL;
        public bool DisableReadWrite;
        public bool OverrideWebGL;
    }

    public static class TextureOptimizationUtility
    {
        public const long CriticalSizeBytes = 5L * 1024L * 1024L; // 5MB
        public const int LargeMaxSize = 2048;

        public static bool TryBuildRow(string assetPath, long sizeBytes, bool isEstimated, out TextureRowData row)
        {
            row = default;

            if (string.IsNullOrEmpty(assetPath))
                return false;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return false;

            row.Path = assetPath;
            row.SizeBytes = sizeBytes;
            row.IsEstimated = isEstimated;

            int w = 0, h = 0;
            try { importer.GetSourceTextureWidthAndHeight(out w, out h); } catch { }
            row.SrcWidth = w;
            row.SrcHeight = h;

            row.ReadWrite = importer.isReadable;

            var ps = importer.GetPlatformTextureSettings("WebGL");
            row.MaxSizeWebGL = ps != null && ps.maxTextureSize > 0 ? ps.maxTextureSize : 0;
            row.CompressionWebGL = ps != null ? ps.textureCompression : importer.textureCompression;
            row.CrunchWebGL = ps != null && ps.crunchedCompression;
            row.CompressionQualityWebGL = ps != null ? ps.compressionQuality : 50;

            return true;
        }

        public static RowQuality Evaluate(TextureRowData row)
        {
            bool uncompressed = row.CompressionWebGL == TextureImporterCompression.Uncompressed;
            bool tooBigMax = row.MaxSizeWebGL > LargeMaxSize;
            bool rwOn = row.ReadWrite;

            if (uncompressed)
                return RowQuality.Red;

            if (rwOn && row.SizeBytes >= CriticalSizeBytes)
                return RowQuality.Red;

            if (tooBigMax && row.SizeBytes >= CriticalSizeBytes)
                return RowQuality.Red;

            if (rwOn || tooBigMax)
                return RowQuality.Yellow;

            return RowQuality.Green;
        }

        public static void ApplyBatch(IReadOnlyList<string> texturePaths, TextureBatchSettings settings, Action<float, string> onProgress)
        {
            if (texturePaths == null || texturePaths.Count == 0)
                return;

            try
            {
                AssetDatabase.StartAssetEditing();

                int total = texturePaths.Count;
                for (int i = 0; i < total; i++)
                {
                    string path = texturePaths[i];
                    float p = total <= 1 ? 1f : (i / (float)(total - 1));
                    onProgress?.Invoke(p, path);

                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null)
                        continue;

                    Undo.RecordObject(importer, "Bridge Texture Batch Apply");

                    if (settings.DisableReadWrite)
                        importer.isReadable = false;

                    if (settings.OverrideWebGL)
                    {
                        var ps = importer.GetPlatformTextureSettings("WebGL");
                        if (ps == null) ps = new TextureImporterPlatformSettings();

                        ps.name = "WebGL";
                        ps.overridden = true;
                        ps.maxTextureSize = settings.MaxSizeWebGL;
                        ps.textureCompression = settings.CompressionWebGL;
                        ps.crunchedCompression = settings.CrunchWebGL;
                        ps.compressionQuality = Mathf.Clamp(settings.CrunchQualityWebGL, 0, 100);

                        importer.SetPlatformTextureSettings(ps);
                    }
                    else
                    {
                        importer.textureCompression = settings.CompressionWebGL;
                    }

                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.Refresh();
            }
        }
    }
}
