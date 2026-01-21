namespace Playgama.Bridge
{
    /// <summary>
    /// Contract for a tab that can be hosted in the Playgama Bridge window.
    /// Implementers are responsible for their own IMGUI rendering and any deferred work.
    /// </summary>
    public interface ITab
    {
        /// <summary>
        /// Display name shown in the tab navigation.
        /// </summary>
        string TabName { get; }

        /// <summary>
        /// Receives analysis data from the host window.
        /// Called on window enable and whenever BuildAnalyzer reports new data.
        /// </summary>
        /// <param name="buildInfo">Shared analysis model (may be empty until a build completes).</param>
        void Init(BuildInfo buildInfo);

        /// <summary>
        /// Main IMGUI entry point for the tab.
        /// Called every repaint cycle while the tab is selected.
        /// Heavy work should be scheduled via EditorApplication.delayCall to keep OnGUI fast.
        /// </summary>
        void OnGUI();
    }
}
