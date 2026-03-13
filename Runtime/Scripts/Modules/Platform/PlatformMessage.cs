#if UNITY_WEBGL
namespace Playgama.Modules.Platform
{
    public enum PlatformMessage
    {
        GameReady,
        InGameLoadingStarted,
        InGameLoadingStopped,
        [System.Obsolete("Use LevelStarted or LevelResumed instead")]
        GameplayStarted,
        [System.Obsolete("Use LevelPaused, LevelCompleted or LevelFailed instead")]
        GameplayStopped,
        PlayerGotAchievement,
        LevelStarted,
        LevelCompleted,
        LevelFailed,
        LevelPaused,
        LevelResumed
    }
}
#endif