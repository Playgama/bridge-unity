#if UNITY_WEBGL
namespace Playgama.Modules.Leaderboards
{
    // How a leaderboard combines submitted scores, set in the developer console.
    public enum LeaderboardScoreMode
    {
        // Keeps the best score.
        Best,
        // Adds every submitted score; the game sends what the player earned.
        Sum
    }
}
#endif
