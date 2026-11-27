#if UNITY_WEBGL
using System.Collections.Generic;

namespace Playgama.Modules.Leaderboards
{
    // Mirrors the core SDK `LeaderboardInfo` shape returned by getInfo().
    public class LeaderboardInfo
    {
        public string id;
        public LeaderboardScoreMode scoreMode;

        // Standings GetEntries() can read: all periods on Playgama leaderboards,
        // only Permanent on platform ones.
        public List<LeaderboardPeriod> periods = new List<LeaderboardPeriod>();

        private readonly Dictionary<LeaderboardPeriod, int> _timeLeft = new Dictionary<LeaderboardPeriod, int>();

        // Whole seconds until the period resets; null for Permanent and for periods
        // the leaderboard does not have.
        public int? GetTimeLeft(LeaderboardPeriod period)
        {
            return _timeLeft.TryGetValue(period, out var seconds) ? seconds : (int?)null;
        }

        internal void SetTimeLeft(LeaderboardPeriod period, int seconds)
        {
            _timeLeft[period] = seconds;
        }
    }
}
#endif
