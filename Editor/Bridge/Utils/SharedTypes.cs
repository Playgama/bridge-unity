using System;
using System.Collections.Generic;
using System.IO;

namespace Playgama.Bridge
{
    /// <summary>
    /// Identifies the source of asset size data:
    /// - PackedAssets: accurate sizes from Unity BuildReport.packedAssets
    /// - DependenciesFallback: file-based estimates when packed data is unavailable
    /// </summary>
    public enum BuildDataMode
    {
        PackedAssets,
        DependenciesFallback
    }

    /// <summary>
    /// Coarse category used to group assets for tab filtering.
    /// Categories are assigned based on file extension and/or importer type.
    /// </summary>
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

    /// <summary>
    /// Represents a single tracked asset from the analysis:
    /// - Path: AssetDatabase path (e.g., "Assets/Textures/icon.png")
    /// - SizeBytes: packed or estimated size in bytes
    /// - Category: coarse category for filtering
    /// - IsSizeEstimated: true if the size is an estimate rather than an exact value
    /// </summary>
    public sealed class AssetInfo
    {
        public string Path;
        public long SizeBytes;
        public string TypeName;
        public AssetCategory Category;
        public bool IsSizeEstimated;
    }

    /// <summary>
    /// Container for the analysis output shared across all tabs:
    /// - TotalBuildSizeBytes: measured from the actual build output
    /// - DataMode: indicates the source of per-asset size data
    /// - Assets: list of tracked assets with sizes and categories
    /// </summary>
    public sealed class BuildInfo
    {
        /// <summary>Total build size in bytes (measured from build output).</summary>
        public long TotalBuildSizeBytes;

        /// <summary>Which data source was used to map assets to sizes.</summary>
        public BuildDataMode DataMode;

        /// <summary>List of tracked assets with sizes and categories.</summary>
        public List<AssetInfo> Assets = new List<AssetInfo>();

        /// <summary>True if analysis data is available.</summary>
        public bool HasData;

        /// <summary>Number of tracked assets.</summary>
        public int TrackedAssetCount;

        /// <summary>Sum of tracked asset sizes (bytes).</summary>
        public long TrackedBytes;

        /// <summary>Status message for display.</summary>
        public string StatusMessage;

        /// <summary>Build target name.</summary>
        public string BuildTargetName;

        /// <summary>Build time duration.</summary>
        public TimeSpan BuildTime;

        /// <summary>Whether the build succeeded.</summary>
        public bool BuildSucceeded;

        /// <summary>Whether BuildReport was used.</summary>
        public bool UsedBuildReport;

        /// <summary>Number of packed asset groups.</summary>
        public int PackedGroupsCount;

        /// <summary>Number of empty paths encountered.</summary>
        public int EmptyPathsCount;

        /// <summary>Diagnostics message for the analysis mode.</summary>
        public string ModeDiagnostics;
    }

    /// <summary>
    /// Shared helper methods for formatting and common operations.
    /// </summary>
    public static class SharedTypes
    {
        /// <summary>
        /// Formats a byte count into a human-readable string (e.g., "12.3 MB").
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "—";
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024.0)).ToString("0.00") + " MB";
            return (bytes / (1024.0 * 1024.0 * 1024.0)).ToString("0.00") + " GB";
        }

        /// <summary>
        /// Returns the file size in bytes for a given asset path.
        /// Returns 0 if the file does not exist or cannot be read.
        /// </summary>
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
