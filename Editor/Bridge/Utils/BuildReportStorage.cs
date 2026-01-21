using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge
{
    /// <summary>
    /// Handles saving and loading build reports to/from disk.
    /// Reports are stored as JSON files in the BuildReports folder.
    /// </summary>
    public static class BuildReportStorage
    {
        private const string ReportsFolderName = "BuildReports";
        private const string ReportFileExtension = ".buildreport";

        /// <summary>
        /// Gets the path to the BuildReports folder (creates if doesn't exist).
        /// </summary>
        public static string GetReportsFolderPath()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string folderPath = Path.Combine(projectRoot, ReportsFolderName);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            return folderPath;
        }

        /// <summary>
        /// Saves a BuildInfo to a JSON file with timestamp.
        /// </summary>
        public static string SaveReport(BuildInfo info)
        {
            if (info == null) return null;

            try
            {
                string folderPath = GetReportsFolderPath();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"BuildReport_{timestamp}{ReportFileExtension}";
                string filePath = Path.Combine(folderPath, fileName);

                var data = new BuildReportData(info);
                string json = JsonUtility.ToJson(data, true);

                File.WriteAllText(filePath, json);

                Debug.Log($"[Playgama Bridge] Build report saved: {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Playgama Bridge] Failed to save build report: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Loads a BuildInfo from a JSON file.
        /// </summary>
        public static BuildInfo LoadReport(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                string json = File.ReadAllText(filePath);
                var data = JsonUtility.FromJson<BuildReportData>(json);
                return data?.ToBuildInfo();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Playgama Bridge] Failed to load build report: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets a list of all saved report files, sorted by date (newest first).
        /// </summary>
        public static List<ReportFileInfo> GetSavedReports()
        {
            var reports = new List<ReportFileInfo>();

            try
            {
                string folderPath = GetReportsFolderPath();
                var files = Directory.GetFiles(folderPath, $"*{ReportFileExtension}");

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    reports.Add(new ReportFileInfo
                    {
                        FilePath = file,
                        FileName = Path.GetFileNameWithoutExtension(file),
                        CreatedTime = fileInfo.CreationTime,
                        FileSize = fileInfo.Length
                    });
                }

                // Sort by creation time, newest first
                reports.Sort((a, b) => b.CreatedTime.CompareTo(a.CreatedTime));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Playgama Bridge] Failed to get saved reports: {ex.Message}");
            }

            return reports;
        }

        /// <summary>
        /// Deletes a saved report file.
        /// </summary>
        public static bool DeleteReport(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Debug.Log($"[Playgama Bridge] Build report deleted: {filePath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Playgama Bridge] Failed to delete build report: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// Loads the most recent report if available.
        /// </summary>
        public static BuildInfo LoadMostRecentReport()
        {
            var reports = GetSavedReports();
            if (reports.Count > 0)
            {
                return LoadReport(reports[0].FilePath);
            }
            return null;
        }
    }

    /// <summary>
    /// Info about a saved report file.
    /// </summary>
    public class ReportFileInfo
    {
        public string FilePath;
        public string FileName;
        public DateTime CreatedTime;
        public long FileSize;

        public string GetDisplayName()
        {
            return CreatedTime.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    /// <summary>
    /// Serializable data structure for BuildInfo.
    /// </summary>
    [Serializable]
    public class BuildReportData
    {
        public long TotalBuildSizeBytes;
        public string DataMode;
        public bool HasData;
        public int TrackedAssetCount;
        public long TrackedBytes;
        public string StatusMessage;
        public string BuildTargetName;
        public double BuildTimeSeconds;
        public bool BuildSucceeded;
        public bool UsedBuildReport;
        public int PackedGroupsCount;
        public int EmptyPathsCount;
        public string ModeDiagnostics;
        public string SavedAt;
        public List<AssetInfoData> Assets = new List<AssetInfoData>();

        public BuildReportData() { }

        public BuildReportData(BuildInfo info)
        {
            TotalBuildSizeBytes = info.TotalBuildSizeBytes;
            DataMode = info.DataMode.ToString();
            HasData = info.HasData;
            TrackedAssetCount = info.TrackedAssetCount;
            TrackedBytes = info.TrackedBytes;
            StatusMessage = info.StatusMessage;
            BuildTargetName = info.BuildTargetName;
            BuildTimeSeconds = info.BuildTime.TotalSeconds;
            BuildSucceeded = info.BuildSucceeded;
            UsedBuildReport = info.UsedBuildReport;
            PackedGroupsCount = info.PackedGroupsCount;
            EmptyPathsCount = info.EmptyPathsCount;
            ModeDiagnostics = info.ModeDiagnostics;
            SavedAt = DateTime.Now.ToString("o");

            if (info.Assets != null)
            {
                foreach (var asset in info.Assets)
                {
                    if (asset != null)
                    {
                        Assets.Add(new AssetInfoData(asset));
                    }
                }
            }
        }

        public BuildInfo ToBuildInfo()
        {
            var info = new BuildInfo
            {
                TotalBuildSizeBytes = TotalBuildSizeBytes,
                DataMode = (BuildDataMode)Enum.Parse(typeof(BuildDataMode), DataMode),
                HasData = HasData,
                TrackedAssetCount = TrackedAssetCount,
                TrackedBytes = TrackedBytes,
                StatusMessage = StatusMessage,
                BuildTargetName = BuildTargetName,
                BuildTime = TimeSpan.FromSeconds(BuildTimeSeconds),
                BuildSucceeded = BuildSucceeded,
                UsedBuildReport = UsedBuildReport,
                PackedGroupsCount = PackedGroupsCount,
                EmptyPathsCount = EmptyPathsCount,
                ModeDiagnostics = ModeDiagnostics,
                Assets = new List<AssetInfo>()
            };

            foreach (var assetData in Assets)
            {
                info.Assets.Add(assetData.ToAssetInfo());
            }

            return info;
        }
    }

    /// <summary>
    /// Serializable data structure for AssetInfo.
    /// </summary>
    [Serializable]
    public class AssetInfoData
    {
        public string Path;
        public long SizeBytes;
        public string TypeName;
        public string Category;
        public bool IsSizeEstimated;

        public AssetInfoData() { }

        public AssetInfoData(AssetInfo asset)
        {
            Path = asset.Path;
            SizeBytes = asset.SizeBytes;
            TypeName = asset.TypeName;
            Category = asset.Category.ToString();
            IsSizeEstimated = asset.IsSizeEstimated;
        }

        public AssetInfo ToAssetInfo()
        {
            return new AssetInfo
            {
                Path = Path,
                SizeBytes = SizeBytes,
                TypeName = TypeName,
                Category = (AssetCategory)Enum.Parse(typeof(AssetCategory), Category),
                IsSizeEstimated = IsSizeEstimated
            };
        }
    }
}
