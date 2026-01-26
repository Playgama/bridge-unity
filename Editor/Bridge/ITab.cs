namespace Playgama.Editor
{
    public interface ITab
    {
        string TabName { get; }
        void Init(BuildInfo buildInfo);
        void OnGUI();
    }
}
