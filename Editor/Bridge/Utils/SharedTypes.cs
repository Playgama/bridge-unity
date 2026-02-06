using System;
using System.Collections.Generic;
using System.IO;

namespace Playgama.Editor
{
    public enum BuildDataMode
    {
        PackedAssets,
        DependenciesFallback
    }

    public enum AssetCategory
    {
        Unknown,
        Textures,
        Audio,
        Meshes,
        Models,
        Shaders,
        Fonts,
        Other
    }

    public sealed class AssetInfo
    {
        public string path;
        public long sizeBytes;
        public string typeName;
        public AssetCategory category;
        public bool isSizeEstimated;
    }

    public sealed class BuildInfo
    {
        public long totalBuildSizeBytes;
        public BuildDataMode dataMode;
        public List<AssetInfo> assets = new List<AssetInfo>();
        public bool hasData;
        public int trackedAssetCount;
        public long trackedBytes;
        public string statusMessage;
        public string buildTargetName;
        public TimeSpan buildTime;
        public bool buildSucceeded;
        public bool usedBuildReport;
        public int packedGroupsCount;
        public int emptyPathsCount;
        public string modeDiagnostics;
    }

    public static class SharedTypes
    {
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "—";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.00") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") + " GB";
        }

        public static long GetFileSizeForAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return 0;

            try
            {
                string fullPath = Path.GetFullPath(assetPath);
                if (File.Exists(fullPath))
                {
                    var fi = new FileInfo(fullPath);
                    return fi.Length;
                }
            }
            catch { }

            return 0;
        }
    }
}
