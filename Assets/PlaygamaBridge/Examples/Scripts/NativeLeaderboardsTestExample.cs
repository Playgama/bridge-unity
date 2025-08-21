using System.Collections.Generic;
using Playgama;
using Playgama.Modules.Leaderboards;
using UnityEngine;

public class NativeLeaderboardsTestExample : MonoBehaviour
{


    public void CheckLeaderboardType()
    {
        if (Bridge.leaderboards.type == LeaderboardType.NativePopup) // this means that the leaderboards is native, but should be called from game to be shown
        {
            // show leaderboard button or call leaderboard from game
            Debug.LogError("Leaderboards is native popup, but should be called from game to be shown");
        }
    }
    
    // Get your Leaderboard ID from the Playgama Manager
    public void ShowLeaderboard()
    {
        Bridge.leaderboards.ShowNativePopup("TopScores", (success) =>
        {
            Debug.Log($"OnShowNativePopupCompleted, success: {success}");
        });
    }


    public void JoinPage()
    {
        var options = new Dictionary<string, object>();
            
        switch (Bridge.platform.id)
        {
            case "fb":
                options.Add("isPage", true);
                break;
        }
        
        Bridge.social.JoinCommunity(options, _ =>
        {
            
        });
    }
    
    public void JoinGroup()
    {
        var options = new Dictionary<string, object>();
            
        switch (Bridge.platform.id)
        {
            case "fb":
                options.Add("isPage", false);
                break;
        }
        
        Bridge.social.JoinCommunity(options, _ =>
        {
            
        });
    }

    public void CheckCommunitySupported()
    {
        
        Debug.LogError("Joining community is supported :" + Bridge.social.isJoinCommunitySupported);
        
        if (Bridge.social.isJoinCommunitySupported)
        {
            // joining community is supported


        }

    }
}
