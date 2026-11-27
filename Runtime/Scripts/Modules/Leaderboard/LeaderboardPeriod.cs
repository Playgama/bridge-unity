#if UNITY_WEBGL
namespace Playgama.Modules.Leaderboards
{
    public enum LeaderboardPeriod
    {
        // Native platform leaderboards are always permanent.
        Permanent,
        // Periodic ones are Playgama SaaS leaderboards only and reset at 00:00 UTC:
        // daily every day, weekly on Monday, monthly on the 1st.
        Daily,
        Weekly,
        Monthly
    }
}
#endif
