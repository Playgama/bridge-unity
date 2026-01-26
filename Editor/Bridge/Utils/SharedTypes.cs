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
        public string Path;
        public long SizeBytes;
        public string TypeName;
        public AssetCategory Category;
        public bool IsSizeEstimated;
    }

    public sealed class BuildInfo
    {
        public long TotalBuildSizeBytes;
        public BuildDataMode DataMode;
        public List<AssetInfo> Assets = new List<AssetInfo>();
        public bool HasData;
        public int TrackedAssetCount;
        public long TrackedBytes;
        public string StatusMessage;
        public string BuildTargetName;
        public TimeSpan BuildTime;
        public bool BuildSucceeded;
        public bool UsedBuildReport;
        public int PackedGroupsCount;
        public int EmptyPathsCount;
        public string ModeDiagnostics;
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
