using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Playgama.Bridge.Tabs
{
    public sealed class PlatformChecksTab : ITab
    {
        public string TabName { get { return "Platform Checks"; } }

        private const string Pref_Platform = "BRIDGE_PLATFORM_CHECKS_PLATFORM";

        private BuildInfo _buildInfo;
        private Vector2 _scroll;
        private string _status = "";
        private TargetPlatform _platform = TargetPlatform.Playgama;

        private bool _foldHeader = false;
        private bool _foldPlatform = true;
        private bool _foldMultiPlatform = true;
        private bool _foldInsights = true;
        private bool _foldRules = true;
        private bool _foldReport = false;

        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;
            string saved = EditorPrefs.GetString(Pref_Platform, TargetPlatform.Playgama.ToString());
            if (!Enum.TryParse(saved, out _platform)) _platform = TargetPlatform.Playgama;
        }

        public void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawHeader();
                DrawPlatformSelector();

                if (_buildInfo == null || !_buildInfo.HasData)
                {
                    EditorGUILayout.HelpBox("No analysis data yet. Run Build & Analyze first.", MessageType.Warning);
                    return;
                }

                var rules = PlatformRules.GetRules(_platform);
                var results = EvaluateRules(rules, _buildInfo);

                DrawMultiPlatformStatus();
                DrawInsights(results);
                DrawRules(results);
                DrawCopyReport(results);

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        private void DrawHeader()
        {
            _foldHeader = BridgeStyles.DrawSectionHeader("About Platform Checks", _foldHeader, "\u2139");
            if (_foldHeader)
            {
                BridgeStyles.BeginCard();
                EditorGUILayout.LabelField("Build-size-focused checks only. No auto-fixes.", BridgeStyles.SubtitleStyle);
                BridgeStyles.EndCard();
            }
        }

        private void DrawPlatformSelector()
        {
            _foldPlatform = BridgeStyles.DrawSectionHeader("Target Platform", _foldPlatform, "\u2316");
            if (!_foldPlatform) return;

            BridgeStyles.BeginCard();

            var newPlat = (TargetPlatform)EditorGUILayout.EnumPopup("Platform", _platform);
            if (newPlat != _platform)
            {
                _platform = newPlat;
                EditorPrefs.SetString(Pref_Platform, _platform.ToString());
            }

            if (_buildInfo != null && _buildInfo.HasData)
            {
                GUILayout.Space(4);

                // Build size with visual indicator
                long size = _buildInfo.TotalBuildSizeBytes;
                long limit = PlatformRules.GetSizeLimit(_platform);
                float pct = limit > 0 ? (float)size / limit : 0f;
                bool overLimit = pct > 1f;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Build Size:", GUILayout.Width(80));
                    GUIStyle sizeStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal = { textColor = overLimit ? new Color(1f, 0.4f, 0.4f) : pct > 0.8f ? new Color(1f, 0.8f, 0.2f) : new Color(0.4f, 0.9f, 0.4f) }
                    };
                    GUILayout.Label(SharedTypes.FormatBytes(size), sizeStyle);

                    if (limit > 0)
                    {
                        GUILayout.Label($"/ {SharedTypes.FormatBytes(limit)} ({pct * 100:0}%)", EditorStyles.miniLabel);
                    }
                }

                // Progress bar
                if (limit > 0)
                {
                    Rect barRect = EditorGUILayout.GetControlRect(false, 8);
                    barRect.x += 4; barRect.width -= 8;
                    EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
                    float fillWidth = Mathf.Min(barRect.width * pct, barRect.width);
                    Color barColor = overLimit ? BridgeStyles.StatusRed : pct > 0.8f ? BridgeStyles.StatusYellow : BridgeStyles.StatusGreen;
                    EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), barColor);
                }

                EditorGUILayout.LabelField("Analysis Mode", _buildInfo.DataMode.ToString());
            }

            BridgeStyles.EndCard();
        }

        private void DrawMultiPlatformStatus()
        {
            _foldMultiPlatform = BridgeStyles.DrawSectionHeader("Multi-Platform Overview", _foldMultiPlatform, "\u2605");
            if (!_foldMultiPlatform) return;

            BridgeStyles.BeginCard();

            long buildSize = _buildInfo?.TotalBuildSizeBytes ?? 0;

            // Platform compatibility grid
            var platforms = (TargetPlatform[])Enum.GetValues(typeof(TargetPlatform));

            foreach (var plat in platforms)
            {
                long limit = PlatformRules.GetSizeLimit(plat);
                if (limit <= 0) continue;

                bool fits = buildSize <= limit;
                float pct = (float)buildSize / limit;

                using (new EditorGUILayout.HorizontalScope())
                {
                    // Status icon
                    string icon = fits ? "✓" : "✗";
                    Color iconColor = fits ? new Color(0.4f, 0.9f, 0.4f) : new Color(1f, 0.4f, 0.4f);
                    GUIStyle iconStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = iconColor } };
                    GUILayout.Label(icon, iconStyle, GUILayout.Width(20));

                    // Platform name
                    GUILayout.Label(plat.ToString(), GUILayout.Width(100));

                    // Mini progress bar
                    Rect barRect = GUILayoutUtility.GetRect(100, 12, GUILayout.Width(100));
                    EditorGUI.DrawRect(barRect, new Color(0.2f, 0.2f, 0.2f));
                    float fillWidth = Mathf.Min(barRect.width * pct, barRect.width);
                    Color barColor = !fits ? BridgeStyles.StatusRed : pct > 0.8f ? BridgeStyles.StatusYellow : BridgeStyles.StatusGreen;
                    EditorGUI.DrawRect(new Rect(barRect.x, barRect.y, fillWidth, barRect.height), barColor);

                    // Size info
                    GUILayout.Label($"{SharedTypes.FormatBytes(limit)} limit", EditorStyles.miniLabel, GUILayout.Width(80));

                    // Percentage
                    string pctText = $"{pct * 100:0}%";
                    GUIStyle pctStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = fits ? Color.gray : new Color(1f, 0.4f, 0.4f) } };
                    GUILayout.Label(pctText, pctStyle);

                    GUILayout.FlexibleSpace();
                }
            }

            BridgeStyles.EndCard();
        }

        private void DrawInsights(List<RuleResult> results)
        {
            int failCount = results?.Count(r => !r.Passed) ?? 0;
            int passCount = results?.Count(r => r.Passed) ?? 0;
            bool allPassed = failCount == 0 && passCount > 0;

            string headerText = allPassed ? "Insights ✓ All Passed!" : $"Insights ({failCount} issues)";
            _foldInsights = BridgeStyles.DrawSectionHeader(headerText, _foldInsights, "\u26A0");
            if (!_foldInsights) return;

            BridgeStyles.BeginCard();

            if (results == null || results.Count == 0)
            {
                EditorGUILayout.LabelField("No rules loaded.", BridgeStyles.SubtitleStyle);
                BridgeStyles.EndCard();
                return;
            }

            // Celebratory message if all passed
            if (allPassed)
            {
                Rect celebRect = EditorGUILayout.GetControlRect(false, 50);
                EditorGUI.DrawRect(celebRect, new Color(0.15f, 0.4f, 0.15f, 0.4f));

                GUIStyle celebStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 16,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.4f, 0.95f, 0.4f) }
                };
                GUI.Label(celebRect, "All checks passed! Your build is ready.", celebStyle);

                BridgeStyles.EndCard();
                return;
            }

            // Show failing rules
            var failing = results
                .Where(r => !r.Passed)
                .OrderByDescending(r => (int)r.Rule.Severity)
                .ThenBy(r => r.Rule.Id)
                .Take(5)
                .ToList();

            EditorGUILayout.LabelField("Top issues:", EditorStyles.boldLabel);
            GUILayout.Space(4);

            for (int i = 0; i < failing.Count; i++)
            {
                var rr = failing[i];

                Rect rowRect = EditorGUILayout.GetControlRect(false, 24);
                Color bgColor = rr.Rule.Severity == RuleSeverity.Critical ? new Color(0.5f, 0.2f, 0.2f, 0.3f) : new Color(0.5f, 0.4f, 0.1f, 0.3f);
                EditorGUI.DrawRect(rowRect, bgColor);

                // Severity badge
                string sevText = rr.Rule.Severity == RuleSeverity.Critical ? "CRITICAL" : "WARNING";
                Color sevColor = rr.Rule.Severity == RuleSeverity.Critical ? BridgeStyles.StatusRed : BridgeStyles.StatusYellow;

                Rect badgeRect = new Rect(rowRect.x + 4, rowRect.y + 4, 60, 16);
                EditorGUI.DrawRect(badgeRect, sevColor);
                GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
                GUI.Label(badgeRect, sevText, badgeStyle);

                // Title
                GUI.Label(new Rect(rowRect.x + 70, rowRect.y + 4, rowRect.width - 80, 16), rr.Rule.Title, EditorStyles.label);

                if (!string.IsNullOrEmpty(rr.Message))
                {
                    EditorGUILayout.LabelField("  → " + rr.Message, BridgeStyles.SubtitleStyle);
                }
            }

            BridgeStyles.EndCard();
        }

        private void DrawRules(List<RuleResult> results)
        {
            int passCount = results?.Count(r => r.Passed) ?? 0;
            int failCount = results?.Count(r => !r.Passed) ?? 0;
            string headerText = $"All Rules ({passCount} passed, {failCount} failed)";

            _foldRules = BridgeStyles.DrawSectionHeader(headerText, _foldRules, "\u2714");
            if (!_foldRules) return;

            BridgeStyles.BeginCard();

            if (results == null || results.Count == 0)
            {
                EditorGUILayout.LabelField("No rules available.", BridgeStyles.SubtitleStyle);
                BridgeStyles.EndCard();
                return;
            }

            // Sort: failures first, then by severity
            var sorted = results
                .OrderBy(r => r.Passed ? 1 : 0)
                .ThenByDescending(r => (int)r.Rule.Severity)
                .ToList();

            foreach (var rr in sorted)
            {
                Rect r = EditorGUILayout.GetControlRect(false, 22);
                r.x += 4; r.width -= 8;

                Color bg = rr.Passed ? new Color(0.2f, 0.4f, 0.2f, 0.3f) : SeverityColor(rr.Rule.Severity);
                EditorGUI.DrawRect(r, bg);

                // Pass/Fail badge
                string badge = rr.Passed ? "PASS" : "FAIL";
                Rect badgeR = new Rect(r.x + 4, r.y + 3, 40, 16);
                EditorGUI.DrawRect(badgeR, rr.Passed ? BridgeStyles.StatusGreen : BridgeStyles.StatusRed);
                GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white }, fontStyle = FontStyle.Bold };
                GUI.Label(badgeR, badge, badgeStyle);

                // Severity
                GUI.Label(new Rect(r.x + 50, r.y + 3, 60, 16), rr.Rule.Severity.ToString(), EditorStyles.miniLabel);

                // Title
                GUI.Label(new Rect(r.x + 115, r.y + 3, r.width - 120, 16), rr.Rule.Title, EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(rr.Message) && !rr.Passed)
                {
                    EditorGUILayout.LabelField("    " + rr.Message, EditorStyles.wordWrappedMiniLabel);
                }
            }

            BridgeStyles.EndCard();
        }

        private static Color SeverityColor(RuleSeverity s)
        {
            switch (s)
            {
                case RuleSeverity.Critical: return new Color(0.5f, 0.2f, 0.2f, 0.4f);
                case RuleSeverity.Warning: return new Color(0.5f, 0.4f, 0.15f, 0.4f);
                default: return BridgeStyles.StatusGray;
            }
        }

        private void DrawCopyReport(List<RuleResult> results)
        {
            _foldReport = BridgeStyles.DrawSectionHeader("Export Report", _foldReport, "\u2398");
            if (!_foldReport) return;

            BridgeStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (BridgeStyles.DrawAccentButton(new GUIContent("Copy Report to Clipboard"), GUILayout.Height(26)))
                {
                    EditorGUIUtility.systemCopyBuffer = BuildReportText(results);
                    _status = "Report copied!";
                }
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.LabelField("Includes platform, build size, and all rule results.", BridgeStyles.SubtitleStyle);
            BridgeStyles.EndCard();
        }

        private string BuildReportText(List<RuleResult> results)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("Playgama Bridge Platform Checks Report");
            sb.AppendLine("--------------------------------------");
            sb.AppendLine($"Platform: {_platform}");

            if (_buildInfo != null && _buildInfo.HasData)
            {
                sb.AppendLine($"Build Size: {SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes)}");
                sb.AppendLine($"Analysis Mode: {_buildInfo.DataMode}");
                sb.AppendLine($"Assets: {_buildInfo.TrackedAssetCount}");
            }

            sb.AppendLine();
            sb.AppendLine("Rules:");
            if (results != null)
            {
                foreach (var rr in results)
                {
                    sb.AppendLine($"- {(rr.Passed ? "PASS" : "FAIL")} [{rr.Rule.Severity}] {rr.Rule.Title}");
                    if (!string.IsNullOrEmpty(rr.Message)) sb.AppendLine($"  {rr.Message}");
                }
            }
            return sb.ToString();
        }

        private sealed class RuleResult
        {
            public PlatformRule Rule;
            public bool Passed;
            public string Message;
        }

        private static List<RuleResult> EvaluateRules(List<PlatformRule> rules, BuildInfo bi)
        {
            var list = new List<RuleResult>(rules?.Count ?? 0);
            if (rules == null) return list;

            foreach (var rule in rules)
            {
                bool pass = true;
                string msg = "";
                try { pass = rule.Check?.Invoke(bi) ?? true; } catch (Exception ex) { pass = false; msg = ex.Message; }
                try { if (rule.GetMessage != null) msg = rule.GetMessage(bi); } catch { }
                list.Add(new RuleResult { Rule = rule, Passed = pass, Message = msg });
            }
            return list;
        }
    }
}
