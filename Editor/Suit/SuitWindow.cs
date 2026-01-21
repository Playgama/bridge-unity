using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Suit
{
    /// <summary>
    /// Main editor window for Playgama Suit.
    ///
    /// Responsibilities:
    /// - Hosts a vertical tab navigation on the left
    /// - Draws the currently selected tab's OnGUI on the right
    /// - Subscribes to BuildAnalyzer.OnBuildInfoChanged to keep tabs in sync
    ///
    /// Design notes:
    /// - Tabs implement ITab; they are created once and reused across repaints
    /// - Heavy work (list builds, calculations) is scheduled via EditorApplication.delayCall inside each tab
    /// - This window only handles layout, tab selection, and the Build & Analyze entry point
    /// </summary>
    public sealed class SuitWindow : EditorWindow
    {
        /// <summary>Opens the window and returns the instance.</summary>
        public static SuitWindow ShowWindow()
        {
            var w = GetWindow<SuitWindow>();
            w.titleContent = new GUIContent("Playgama Suit");
            w.minSize = new Vector2(820, 480);
            w.Show();
            return w;
        }

        /// <summary>Registered tabs shown in the left navigation.</summary>
        private readonly List<ITab> _tabs = new List<ITab>();

        /// <summary>Currently selected tab index (persisted in EditorPrefs).</summary>
        private int _selectedTab = 0;

        /// <summary>Scroll position for the tab list (left column).</summary>
        private Vector2 _tabScroll;

        /// <summary>
        /// Shared analysis data model populated by BuildAnalyzer.
        /// Tabs read from this when rendering their UI.
        /// </summary>
        private BuildInfo _buildInfo;

        /// <summary>Width of the left tab navigation column (pixels).</summary>
        private const float TabColumnWidth = 150f;

        /// <summary>EditorPrefs key for persisting the selected tab index.</summary>
        private const string Pref_SelectedTab = "SUIT_SELECTED_TAB";

        private void OnEnable()
        {
            // Enable mouse move events for responsive hover effects
            wantsMouseMove = true;

            _buildInfo = new BuildInfo();

            _tabs.Clear();
            _tabs.Add(new Tabs.HomeTab());
            _tabs.Add(new Tabs.SummaryTab());
            _tabs.Add(new Tabs.TexturesTab());
            _tabs.Add(new Tabs.AudioTab());
            _tabs.Add(new Tabs.MeshesTab());
            _tabs.Add(new Tabs.ShadersTab());
            _tabs.Add(new Tabs.FontsTab());
            _tabs.Add(new Tabs.BuildSettingsTab());
            _tabs.Add(new Tabs.PlatformChecksTab());
            _tabs.Add(new Tabs.SettingsTab());

            // Try to load the most recent saved report
            var savedReport = BuildReportStorage.LoadMostRecentReport();
            if (savedReport != null)
            {
                CopyBuildInfo(savedReport, _buildInfo);
            }

            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Init(_buildInfo);

            _selectedTab = EditorPrefs.GetInt(Pref_SelectedTab, 0);
            if (_selectedTab < 0 || _selectedTab >= _tabs.Count)
                _selectedTab = 0;

            BuildAnalyzer.OnBuildInfoChanged -= OnBuildInfoChanged;
            BuildAnalyzer.OnBuildInfoChanged += OnBuildInfoChanged;
        }

        private void OnDisable()
        {
            BuildAnalyzer.OnBuildInfoChanged -= OnBuildInfoChanged;
        }

        /// <summary>
        /// Event handler invoked by BuildAnalyzer when analysis data updates.
        /// Copies new data into _buildInfo and reinitializes tabs.
        /// </summary>
        private void OnBuildInfoChanged(BuildInfo info)
        {
            if (info == null) return;

            CopyBuildInfo(info, _buildInfo);

            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Init(_buildInfo);

            Repaint();

            // Schedule additional repaint to ensure UI updates after delayed rebuilds
            EditorApplication.delayCall += () =>
            {
                Repaint();
            };
        }

        /// <summary>
        /// Copies all fields from source BuildInfo to destination.
        /// </summary>
        private static void CopyBuildInfo(BuildInfo source, BuildInfo dest)
        {
            dest.TotalBuildSizeBytes = source.TotalBuildSizeBytes;
            dest.DataMode = source.DataMode;
            dest.Assets = source.Assets;
            dest.HasData = source.HasData;
            dest.TrackedAssetCount = source.TrackedAssetCount;
            dest.TrackedBytes = source.TrackedBytes;
            dest.StatusMessage = source.StatusMessage;
            dest.BuildTargetName = source.BuildTargetName;
            dest.BuildTime = source.BuildTime;
            dest.BuildSucceeded = source.BuildSucceeded;
            dest.UsedBuildReport = source.UsedBuildReport;
            dest.PackedGroupsCount = source.PackedGroupsCount;
            dest.EmptyPathsCount = source.EmptyPathsCount;
            dest.ModeDiagnostics = source.ModeDiagnostics;
        }

        /// <summary>
        /// Loads a saved report and updates all tabs.
        /// </summary>
        public void LoadSavedReport(BuildInfo info)
        {
            if (info == null) return;

            CopyBuildInfo(info, _buildInfo);

            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Init(_buildInfo);

            Repaint();
        }

        private void OnGUI()
        {
            // Repaint on mouse move for responsive hover effects
            if (Event.current.type == EventType.MouseMove)
            {
                Repaint();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawTabColumn();
                DrawContentColumn();
            }
        }

        /// <summary>
        /// Left column: vertical tab list for navigation.
        /// </summary>
        private void DrawTabColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(TabColumnWidth)))
            {
                GUILayout.Space(6);
                GUILayout.Label("Playgama Suit", EditorStyles.boldLabel);
                GUILayout.Space(4);

                using (var sv = new EditorGUILayout.ScrollViewScope(_tabScroll, GUILayout.ExpandHeight(true)))
                {
                    _tabScroll = sv.scrollPosition;

                    for (int i = 0; i < _tabs.Count; i++)
                    {
                        bool selected = (i == _selectedTab);
                        GUIStyle style = selected ? SuitStyles.TabButtonSelected : SuitStyles.TabButton;

                        if (GUILayout.Button(_tabs[i].TabName, style, GUILayout.Height(28)))
                        {
                            _selectedTab = i;
                            EditorPrefs.SetInt(Pref_SelectedTab, _selectedTab);
                        }
                    }
                }

                GUILayout.Space(8);

                if (SuitStyles.DrawAccentButton(new GUIContent("Build & Analyze", "Build WebGL and run analysis."), GUILayout.Height(32)))
                {
                    EditorApplication.delayCall += () => BuildAnalyzer.BuildAndAnalyze();
                }

                GUILayout.Space(6);
            }
        }

        /// <summary>
        /// Right column: renders the selected tab's OnGUI.
        /// </summary>
        private void DrawContentColumn()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                if (_selectedTab >= 0 && _selectedTab < _tabs.Count)
                    _tabs[_selectedTab].OnGUI();
            }
        }
    }
}
