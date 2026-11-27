#if UNITY_WEBGL
namespace Playgama.Modules.Leaderboards
{
    // Standing GetEntries() reads. Playgama leaderboards keep all of them from the same scores,
    // Daily/Weekly/Monthly reset at 00:00 UTC (weekly on Monday, monthly on the 1st).
    // Platform leaderboards have only Permanent.
    public enum LeaderboardPeriod
    {
        Permanent,
        Daily,
        Weekly,
        Monthly
    }
}
#endif
