using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Playgama.Editor
{
    public sealed class BridgeWindow : EditorWindow
    {
        private readonly List<ITab> _tabs = new List<ITab>();
        private int _selectedTab = 0;
        private Vector2 _tabScroll;
        private BuildInfo _buildInfo;

        private const float TabColumnWidth = 150f;
        private const string Pref_SelectedTab = "BRIDGE_SELECTED_TAB";

        // Tab indices for external navigation
        public const int TabHome = 0;
        public const int TabSummary = 1;
        public const int TabTextures = 2;
        public const int TabAudio = 3;
        public const int TabMeshes = 4;
        public const int TabShaders = 5;
        public const int TabFonts = 6;
        public const int TabBuildSettings = 7;
        public const int TabSettings = 8;

        public static BridgeWindow ShowWindow()
        {
            var w = GetWindow<BridgeWindow>();
            w.titleContent = new GUIContent("Playgama Bridge");
            w.minSize = new Vector2(820, 480);
            w.Show();
            return w;
        }

        private void OnEnable()
        {
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
            _tabs.Add(new Tabs.SettingsTab());

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

        private void OnBuildInfoChanged(BuildInfo info)
        {
            if (info == null) return;

            CopyBuildInfo(info, _buildInfo);

            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Init(_buildInfo);

            Repaint();

            EditorApplication.delayCall += () =>
            {
                Repaint();
            };
        }

        private static void CopyBuildInfo(BuildInfo source, BuildInfo dest)
        {
            dest.totalBuildSizeBytes = source.totalBuildSizeBytes;
            dest.dataMode = source.dataMode;
            dest.assets = source.assets;
            dest.hasData = source.hasData;
            dest.trackedAssetCount = source.trackedAssetCount;
            dest.trackedBytes = source.trackedBytes;
            dest.statusMessage = source.statusMessage;
            dest.buildTargetName = source.buildTargetName;
            dest.buildTime = source.buildTime;
            dest.buildSucceeded = source.buildSucceeded;
            dest.usedBuildReport = source.usedBuildReport;
            dest.packedGroupsCount = source.packedGroupsCount;
            dest.emptyPathsCount = source.emptyPathsCount;
            dest.modeDiagnostics = source.modeDiagnostics;
        }

        public void LoadSavedReport(BuildInfo info)
        {
            if (info == null) return;

            CopyBuildInfo(info, _buildInfo);

            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Init(_buildInfo);

            Repaint();
        }
        
        public void SetSelectedTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= _tabs.Count)
                return;

            _selectedTab = tabIndex;
            EditorPrefs.SetInt(Pref_SelectedTab, _selectedTab);
            Repaint();
        }

        private void OnGUI()
        {
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

        private void DrawTabColumn()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(TabColumnWidth)))
            {
                GUILayout.Space(6);
                GUILayout.Label("Playgama Bridge", EditorStyles.boldLabel);
                GUILayout.Space(4);

                using (var sv = new EditorGUILayout.ScrollViewScope(_tabScroll, GUILayout.ExpandHeight(true)))
                {
                    _tabScroll = sv.scrollPosition;

                    for (int i = 0; i < _tabs.Count; i++)
                    {
                        bool selected = (i == _selectedTab);
                        GUIStyle style = selected ? BridgeStyles.tabButtonSelected : BridgeStyles.tabButton;

                        if (GUILayout.Button(_tabs[i].TabName, style, GUILayout.Height(28)))
                        {
                            _selectedTab = i;
                            EditorPrefs.SetInt(Pref_SelectedTab, _selectedTab);
                        }
                    }
                }

                GUILayout.Space(8);

                if (BridgeStyles.DrawAccentButton(new GUIContent("Build & Analyze", "Build WebGL and run analysis."), GUILayout.Height(32)))
                {
                    EditorApplication.delayCall += () => BuildAnalyzer.BuildAndAnalyze();
                }

                GUILayout.Space(6);
            }
        }

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
