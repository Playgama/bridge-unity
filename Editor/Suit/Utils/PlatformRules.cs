using System;
using System.Collections.Generic;

namespace Playgama.Suit
{
    public enum RuleSeverity
    {
        Info = 0,
        Warning = 1,
        Critical = 2
    }

    public enum TargetPlatform
    {
        Playgama,
        Y8,
        Lagged,
        Facebook,
        MSN,
        GameDistribution,
        Xiaomi,
        YouTubePlayables,
        Amazon,
        Discord,
        YandexGames,
        PlayDeck
    }

    public sealed class PlatformRule
    {
        public string Id;
        public string Title;
        public RuleSeverity Severity;
        public Func<BuildInfo, bool> Check;
        public Func<BuildInfo, string> GetMessage;

        public PlatformRule(string id, string title, RuleSeverity severity, Func<BuildInfo, bool> check, Func<BuildInfo, string> msg)
        {
            Id = id;
            Title = title;
            Severity = severity;
            Check = check ?? (_ => true);
            GetMessage = msg ?? (_ => "");
        }
    }

    public static class PlatformRules
    {
        public static List<PlatformRule> GetRules(TargetPlatform platform)
        {
            var rules = new List<PlatformRule>(16);

            long soft = GetSoftLimit(platform);
            long hard = GetHardLimit(platform);

            rules.Add(new PlatformRule(
                id: "build.total.soft",
                title: $"Total build size ≤ {FormatMB(soft)} (recommended)",
                severity: RuleSeverity.Warning,
                check: bi => HasBuild(bi) && bi.TotalBuildSizeBytes <= soft,
                msg: bi => HasBuild(bi)
                    ? $"Current: {FormatMB(bi.TotalBuildSizeBytes)}. Aim under {FormatMB(soft)} for faster initial load and better conversion."
                    : "No build analyzed yet. Run Build & Analyze to measure real size."
            ));

            rules.Add(new PlatformRule(
                id: "build.total.hard",
                title: $"Total build size ≤ {FormatMB(hard)} (upper bound)",
                severity: RuleSeverity.Critical,
                check: bi => HasBuild(bi) && bi.TotalBuildSizeBytes <= hard,
                msg: bi => HasBuild(bi)
                    ? $"Current: {FormatMB(bi.TotalBuildSizeBytes)}. Above {FormatMB(hard)} is a serious risk for Web distribution and drop-off."
                    : "No build analyzed yet. Run Build & Analyze to measure real size."
            ));

            rules.Add(new PlatformRule(
                id: "build.mode",
                title: "Analysis mode is not empty",
                severity: RuleSeverity.Info,
                check: bi => bi != null && bi.HasData,
                msg: bi => bi != null && bi.HasData
                    ? $"Mode: {bi.DataMode}. Tracked assets: {bi.TrackedAssetCount}. Tracked bytes: {FormatMB(bi.TrackedBytes)}" +
                      (bi.DataMode == BuildDataMode.DependenciesFallback ? " (estimated)" : "")
                    : "No analysis data. Build first."
            ));

            rules.Add(new PlatformRule(
                id: "build.fallback.note",
                title: "If using DependenciesFallback, treat per-asset sizes as estimates",
                severity: RuleSeverity.Info,
                check: bi => bi != null && bi.HasData,
                msg: bi =>
                {
                    if (bi == null || !bi.HasData) return "No analysis data.";
                    if (bi.DataMode == BuildDataMode.DependenciesFallback)
                        return "Per-asset sizes are estimated (file size on disk). Total build size is still real.";
                    return "PackedAssets mode: per-asset mapping is more accurate when paths are available.";
                }
            ));

            return rules;
        }

        private static bool HasBuild(BuildInfo bi)
        {
            return bi != null && bi.HasData && bi.TotalBuildSizeBytes > 0;
        }

        private static long GetSoftLimit(TargetPlatform p)
        {
            switch (p)
            {
                case TargetPlatform.YouTubePlayables: return MB(20);
                case TargetPlatform.Facebook:         return MB(25);
                case TargetPlatform.YandexGames:      return MB(30);
                case TargetPlatform.MSN:              return MB(30);
                case TargetPlatform.Amazon:           return MB(30);
                case TargetPlatform.GameDistribution: return MB(30);
                case TargetPlatform.Xiaomi:           return MB(30);
                case TargetPlatform.PlayDeck:         return MB(30);
                case TargetPlatform.Y8:               return MB(40);
                case TargetPlatform.Lagged:           return MB(40);
                case TargetPlatform.Discord:          return MB(40);
                case TargetPlatform.Playgama:         return MB(40);
                default:                              return MB(30);
            }
        }

        private static long GetHardLimit(TargetPlatform p)
        {
            switch (p)
            {
                case TargetPlatform.YouTubePlayables: return MB(30);
                case TargetPlatform.Facebook:         return MB(40);
                case TargetPlatform.YandexGames:      return MB(50);
                case TargetPlatform.MSN:              return MB(50);
                case TargetPlatform.Amazon:           return MB(50);
                case TargetPlatform.GameDistribution: return MB(60);
                case TargetPlatform.Xiaomi:           return MB(60);
                case TargetPlatform.PlayDeck:         return MB(60);
                case TargetPlatform.Y8:               return MB(70);
                case TargetPlatform.Lagged:           return MB(70);
                case TargetPlatform.Discord:          return MB(70);
                case TargetPlatform.Playgama:         return MB(70);
                default:                              return MB(60);
            }
        }

        private static long MB(long mb) => mb * 1024L * 1024L;

        private static string FormatMB(long bytes)
        {
            double mb = bytes / (1024.0 * 1024.0);
            return mb.ToString("0.#") + " MB";
        }
    }
}
