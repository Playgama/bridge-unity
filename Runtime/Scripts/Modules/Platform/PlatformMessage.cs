#if UNITY_WEBGL
namespace Playgama.Modules.Platform
{
    public enum PlatformMessage
    {
        GameReady,
        InGameLoadingStarted,
        InGameLoadingStopped,
        GameplayStarted,
        GameplayStopped,
        PlayerGotAchievement
    }
}
#endif