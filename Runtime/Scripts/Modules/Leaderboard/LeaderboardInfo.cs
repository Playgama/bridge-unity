#if UNITY_WEBGL
namespace Playgama.Modules.Leaderboards
{
    // Mirrors the core SDK `LeaderboardInfo` shape returned by getInfo().
    public class LeaderboardInfo
    {
        public string id;
        public LeaderboardPeriod period;

        // Whole seconds until the current period resets; null for Permanent.
        public int? timeLeft;
    }
}
#endif
