using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit.Tabs
{
    /// <summary>
    /// UI tab that runs build-size-only compliance checks for a selected target platform.
    ///
    /// Key points:
    /// - This tab does NOT modify assets or project settings (no auto-fixes).
    /// - It evaluates a platform-specific list of rules against the latest analysis data (BuildInfo).
    /// - It surfaces the most severe failures as "Insights", shows all rules, and can copy a text report.
    /// </summary>
    public sealed class PlatformChecksTab : ITab
    {
        /// <summary>Displayed name of the tab in the parent UI.</summary>
        public string TabName { get { return "Platform Checks"; } }

        /// <summary>EditorPrefs key used to persist the selected platform between sessions.</summary>
        private const string Pref_Platform = "SUIT_PLATFORM_CHECKS_PLATFORM";

        /// <summary>Reference to the latest analysis data provided by the hosting window.</summary>
        private BuildInfo _buildInfo;

        /// <summary>Scroll position for the tab content.</summary>
        private Vector2 _scroll;

        /// <summary>Short UI status message shown at the bottom of the tab.</summary>
        private string _status = "";

        /// <summary>Current platform used to pick the rule set.</summary>
        private TargetPlatform _platform = TargetPlatform.Playgama;

        // Foldout states for collapsible sections.
        private bool _foldHeader = true;
        private bool _foldPlatform = true;
        private bool _foldInsights = true;
        private bool _foldRules = true;
        private bool _foldReport = false;

        /// <summary>
        /// Centralized GUIContent labels and tooltips.
        /// Keeping these in one place prevents tooltip drift and keeps OnGUI readable.
        /// </summary>
        private static class UI
        {
            public static readonly GUIContent HeaderTitle = new GUIContent(
                "Platform Checks (Build Size Only)",
                "Runs build-size-focused checks against the latest Playgama Suit analysis data.\n" +
                "This tab does not change anything in the project.");

            public static readonly GUIContent HeaderHelp = new GUIContent(
                "This tab contains ONLY build-size-related guidance.\n" +
                "• No auto-fixes\n" +
                "• No non-size rules\n" +
                "• No platform SDK assumptions beyond what the rules describe",
                "High-level rules of engagement: report only, do not modify project.");

            public static readonly GUIContent TargetPlatformTitle = new GUIContent(
                "Target Platform",
                "Select which platform's build-size requirements/guidance to evaluate.\n" +
                "The selection is saved in EditorPrefs.");

            public static readonly GUIContent PlatformField = new GUIContent(
                "Platform",
                "The platform whose rule set will be evaluated.");

            public static readonly GUIContent BuildSizeReal = new GUIContent(
                "Total Build Size (real)",
                "Measured from the actual build output size, not estimates.");

            public static readonly GUIContent AnalysisMode = new GUIContent(
                "Analysis Mode",
                "Which Playgama Suit analysis source was used to estimate per-asset size mapping.");

            public static readonly GUIContent NoData = new GUIContent(
                "No analysis data yet. Run Build & Analyze first.",
                "Platform checks rely on BuildInfo. Without analysis data, rules may fail or show generic messages.");

            public static readonly GUIContent InsightsTitle = new GUIContent(
                "Insights",
                "Top failing rules (highest severity first). Shows only a few to keep focus.");

            public static readonly GUIContent NoRulesLoaded = new GUIContent(
                "No rules loaded.",
                "No rules were returned for the selected platform (PlatformRules.GetRules).");

            public static readonly GUIContent LooksGood = new GUIContent(
                "Looks good. No build-size issues detected by current rules.",
                "All rules passed for the selected platform.");

            public static readonly GUIContent TopProblems = new GUIContent(
                "Top problems (highest severity first):",
                "This is a short list of the most severe failures so you can prioritize fixes.");

            public static readonly GUIContent RulesTitle = new GUIContent(
                "Rules",
                "Full list of evaluated rules and their pass/fail result.");

            public static readonly GUIContent NoRulesAvailable = new GUIContent(
                "No rules available.",
                "The selected platform returned an empty rule list.");

            public static readonly GUIContent ReportTitle = new GUIContent(
                "Report",
                "Generate a text report of the current evaluation and copy it to clipboard.");

            public static readonly GUIContent CopyReport = new GUIContent(
                "Copy report to clipboard",
                "Copies platform, build size, analysis mode, and rule results.\n" +
                "Useful for sharing with teammates or pasting into a ticket.");

            public static readonly GUIContent ReportHelp = new GUIContent(
                "The report includes platform, real build size, analysis mode, and rule results.",
                "This is a text-only summary for quick communication.");

            public static readonly GUIContent PassLabel = new GUIContent(
                "PASS",
                "Rule passed for the current BuildInfo snapshot.");

            public static readonly GUIContent FailLabel = new GUIContent(
                "FAIL",
                "Rule failed for the current BuildInfo snapshot.");

            public static readonly GUIContent StatusSaved = new GUIContent(
                "Platform selection saved.",
                "The selected platform was persisted to EditorPrefs.");
        }

        /// <summary>
        /// Receives analysis data and restores the last-selected platform from EditorPrefs.
        /// </summary>
        public void Init(BuildInfo buildInfo)
        {
            _buildInfo = buildInfo;

            string saved = EditorPrefs.GetString(Pref_Platform, TargetPlatform.Playgama.ToString());
            if (!Enum.TryParse(saved, out _platform))
                _platform = TargetPlatform.Playgama;
        }

        /// <summary>
        /// Main Unity IMGUI entry point for this tab.
        /// Workflow:
        /// 1) Draw header and platform selector
        /// 2) Load platform rule set
        /// 3) Evaluate rules against BuildInfo
        /// 4) Render insights + full rule list + copyable report
        /// </summary>
        public void OnGUI()
        {
            using (var sv = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = sv.scrollPosition;

                DrawHeader();
                DrawPlatformSelector();

                if (_buildInfo == null || !_buildInfo.HasData)
                    EditorGUILayout.HelpBox(UI.NoData.text, MessageType.Warning);

                var rules = PlatformRules.GetRules(_platform);
                var results = EvaluateRules(rules, _buildInfo);

                DrawInsights(results);
                DrawRules(results);
                DrawCopyReport(results);

                if (!string.IsNullOrEmpty(_status))
                    EditorGUILayout.HelpBox(_status, MessageType.None);
            }
        }

        /// <summary>
        /// Header explains the scope (build size only) and sets expectations (no auto-fixes).
        /// </summary>
        private void DrawHeader()
        {
            _foldHeader = SuitStyles.DrawSectionHeader("About Platform Checks", _foldHeader, "\u2139");
            if (_foldHeader)
            {
                SuitStyles.BeginCard();
                EditorGUILayout.LabelField("Build-size-focused checks only. No auto-fixes, no non-size rules.", SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
            }
        }

        /// <summary>
        /// Platform selector:
        /// - Lets the user pick the target platform
        /// - Persists selection in EditorPrefs
        /// - Shows last-known build size and analysis mode if BuildInfo exists
        /// </summary>
        private void DrawPlatformSelector()
        {
            _foldPlatform = SuitStyles.DrawSectionHeader("Target Platform", _foldPlatform, "\u2316");
            if (_foldPlatform)
            {
                SuitStyles.BeginCard();
                var newPlat = (TargetPlatform)EditorGUILayout.EnumPopup(UI.PlatformField, _platform);
                if (newPlat != _platform)
                {
                    _platform = newPlat;
                    EditorPrefs.SetString(Pref_Platform, _platform.ToString());
                    _status = UI.StatusSaved.text;
                }

                if (_buildInfo != null && _buildInfo.HasData)
                {
                    EditorGUILayout.LabelField(UI.BuildSizeReal, SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes));
                    EditorGUILayout.LabelField(UI.AnalysisMode, _buildInfo.DataMode.ToString());
                }
                SuitStyles.EndCard();
            }
        }

        // --------------------------
        // Insights
        // --------------------------

        /// <summary>
        /// Shows a short prioritized list of failing rules:
        /// - Only rules that failed
        /// - Sorted by severity (descending) then by rule id
        /// - Limited to the top 3 failures for focus
        /// </summary>
        private void DrawInsights(List<RuleResult> results)
        {
            _foldInsights = SuitStyles.DrawSectionHeader("Insights", _foldInsights, "\u26A0");
            if (!_foldInsights) return;

            SuitStyles.BeginCard();
            if (results == null || results.Count == 0)
            {
                EditorGUILayout.LabelField(UI.NoRulesLoaded.text, SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
                return;
            }

            var failing = results
                .Where(r => !r.Passed)
                .OrderByDescending(r => (int)r.Rule.Severity)
                .ThenBy(r => r.Rule.Id)
                .Take(3)
                .ToList();

            if (failing.Count == 0)
            {
                EditorGUILayout.LabelField(UI.LooksGood.text, SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
                return;
            }

            EditorGUILayout.LabelField("Top problems (highest severity first):", EditorStyles.miniBoldLabel);

            for (int i = 0; i < failing.Count; i++)
            {
                var rr = failing[i];

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(
                        new GUIContent($"{i + 1}. [{rr.Rule.Severity}] {rr.Rule.Title}",
                            "A top failing rule, sorted by severity then id."),
                        EditorStyles.wordWrappedLabel);
                }

                if (!string.IsNullOrEmpty(rr.Message))
                    EditorGUILayout.LabelField(new GUIContent("→ " + rr.Message, "Rule-specific guidance/details."),
                        SuitStyles.SubtitleStyle);

                GUILayout.Space(4);
            }
            SuitStyles.EndCard();
        }

        // --------------------------
        // Rules
        // --------------------------

        /// <summary>
        /// Renders the full list of evaluated rules and their pass/fail status.
        /// Each rule row uses a background color derived from severity or pass status.
        /// </summary>
        private void DrawRules(List<RuleResult> results)
        {
            int ruleCount = results?.Count ?? 0;
            _foldRules = SuitStyles.DrawSectionHeader($"All Rules ({ruleCount} items)", _foldRules, "\u2714");
            if (!_foldRules) return;

            SuitStyles.BeginCard();
            if (results == null || results.Count == 0)
            {
                EditorGUILayout.LabelField(UI.NoRulesAvailable.text, SuitStyles.SubtitleStyle);
                SuitStyles.EndCard();
                return;
            }

            for (int i = 0; i < results.Count; i++)
                DrawRuleRow(results[i]);
            SuitStyles.EndCard();
        }

        /// <summary>
        /// Draws a single rule result row.
        /// Layout:
        /// - PASS/FAIL label
        /// - Severity
        /// - Title
        /// - Optional message under the row (word-wrapped)
        /// </summary>
        private void DrawRuleRow(RuleResult rr)
        {
            Rect r = EditorGUILayout.GetControlRect(false, 24);
            r.x += 6;
            r.y += 2;
            r.height -= 4;

            Color bg = rr.Passed ? SuitStyles.StatusGreen : SeverityColor(rr.Rule.Severity);
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, r.height), bg);

            Rect iconR = new Rect(r.x + 6, r.y + 2, 60, r.height);
            EditorGUI.LabelField(
                iconR,
                rr.Passed ? UI.PassLabel : UI.FailLabel,
                EditorStyles.miniBoldLabel);

            Rect sevR = new Rect(r.x + 66, r.y + 2, 80, r.height);
            EditorGUI.LabelField(
                sevR,
                new GUIContent(rr.Rule.Severity.ToString(), "Rule severity (used for prioritization)."),
                EditorStyles.miniLabel);

            Rect titleR = new Rect(r.x + 150, r.y + 2, r.width - 150, r.height);
            EditorGUI.LabelField(
                titleR,
                new GUIContent(rr.Rule.Title, $"Rule ID: {rr.Rule.Id}\nSeverity: {rr.Rule.Severity}"),
                EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(rr.Message))
                EditorGUILayout.LabelField(new GUIContent("  " + rr.Message, "Rule output / guidance."),
                    EditorStyles.wordWrappedMiniLabel);
        }

        /// <summary>
        /// Maps severity to a soft background color for failed rows.
        /// Pass rows use a green tint regardless of severity.
        /// </summary>
        private static Color SeverityColor(RuleSeverity s)
        {
            switch (s)
            {
                case RuleSeverity.Critical: return SuitStyles.StatusRed;
                case RuleSeverity.Warning: return SuitStyles.StatusYellow;
                default: return SuitStyles.StatusGray;
            }
        }

        // --------------------------
        // Copy report
        // --------------------------

        /// <summary>
        /// Provides a single button to copy a text report for external sharing.
        /// </summary>
        private void DrawCopyReport(List<RuleResult> results)
        {
            _foldReport = SuitStyles.DrawSectionHeader("Copy Report", _foldReport, "\u2398");
            if (!_foldReport) return;

            SuitStyles.BeginCard();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (SuitStyles.DrawAccentButton(UI.CopyReport, GUILayout.Height(28)))
                {
                    string text = BuildReportText(results);
                    EditorGUIUtility.systemCopyBuffer = text;
                    _status = "Report copied to clipboard.";
                }

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.LabelField("Includes platform, real build size, analysis mode, and rule results.", SuitStyles.SubtitleStyle);
            SuitStyles.EndCard();
        }

        /// <summary>
        /// Builds a plain-text report:
        /// - Header + selected platform
        /// - BuildInfo snapshot (if available)
        /// - List of rule results with messages
        /// </summary>
        private string BuildReportText(List<RuleResult> results)
        {
            var sb = new StringBuilder(2048);

            sb.AppendLine("Playgama Suit Platform Checks Report (Build Size Only)");
            sb.AppendLine("------------------------------------------------");
            sb.AppendLine("Platform: " + _platform);

            if (_buildInfo != null && _buildInfo.HasData)
            {
                sb.AppendLine("Total Build Size (real): " + SharedTypes.FormatBytes(_buildInfo.TotalBuildSizeBytes));
                sb.AppendLine("Analysis Mode: " + _buildInfo.DataMode);
                sb.AppendLine("Tracked Assets: " + _buildInfo.TrackedAssetCount);

                string trackedBytes = SharedTypes.FormatBytes(_buildInfo.TrackedBytes) +
                                      (_buildInfo.DataMode == BuildDataMode.DependenciesFallback ? " (estimated)" : "");
                sb.AppendLine("Tracked Bytes: " + trackedBytes);
            }
            else
            {
                sb.AppendLine(UI.NoData.text);
            }

            sb.AppendLine();
            sb.AppendLine("Rules:");

            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    var rr = results[i];
                    sb.AppendLine($"- {(rr.Passed ? "PASS" : "FAIL")} [{rr.Rule.Severity}] {rr.Rule.Title}");
                    if (!string.IsNullOrEmpty(rr.Message))
                        sb.AppendLine("  " + rr.Message);
                }
            }

            return sb.ToString();
        }

        // --------------------------
        // Evaluation
        // --------------------------

        /// <summary>
        /// Runtime evaluation output for a platform rule:
        /// - Rule reference (metadata + delegates)
        /// - Pass/fail result
        /// - Optional message (guidance/details)
        /// </summary>
        private sealed class RuleResult
        {
            public PlatformRule Rule;
            public bool Passed;
            public string Message;
        }

        /// <summary>
        /// Evaluates every platform rule against the provided BuildInfo.
        /// Behavior:
        /// - If rule.Check throws: mark fail and capture exception message (as a fallback)
        /// - rule.GetMessage is executed separately (if available) to provide guidance text
        /// - Results preserve rule order from PlatformRules.GetRules (no reordering here)
        /// </summary>
        private static List<RuleResult> EvaluateRules(List<PlatformRule> rules, BuildInfo bi)
        {
            var list = new List<RuleResult>(rules != null ? rules.Count : 0);
            if (rules == null) return list;

            for (int i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                bool pass = true;
                string msg = "";

                try
                {
                    pass = rule.Check != null ? rule.Check(bi) : true;
                }
                catch (Exception ex)
                {
                    pass = false;
                    msg = "Rule check exception: " + ex.Message;
                }

                try
                {
                    if (rule.GetMessage != null)
                        msg = rule.GetMessage(bi);
                }
                catch (Exception ex)
                {
                    if (string.IsNullOrEmpty(msg))
                        msg = "Rule message exception: " + ex.Message;
                }

                list.Add(new RuleResult
                {
                    Rule = rule,
                    Passed = pass,
                    Message = msg
                });
            }

            return list;
        }
    }
}
