#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using Playgama.Common;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.DailyRewards
{
    public class DailyRewardsModule : MonoBehaviour
    {
#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeDailyRewardsGetRewards();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeDailyRewardsGetCurrentDay();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeDailyRewardsGetCurrentReward();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeDailyRewardsClaimCurrentReward();
#endif

        private Action<bool, string[]> _getRewardsCallback;
        private Action<bool, int> _getCurrentDayCallback;
        private Action<bool, string> _getCurrentRewardCallback;
        private Action<bool> _claimCurrentRewardCallback;

        // Returns the ordered list of reward ids (one per day) as a JSON array.
        public void GetRewards(Action<bool, string[]> onComplete = null)
        {
            _getRewardsCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeDailyRewardsGetRewards();
#else
            OnDailyRewardsGetRewardsCompletedFailed();
#endif
        }

        // Returns the 0-based index of the reward the player is currently on.
        public void GetCurrentDay(Action<bool, int> onComplete = null)
        {
            _getCurrentDayCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeDailyRewardsGetCurrentDay();
#else
            OnDailyRewardsGetCurrentDayCompletedFailed();
#endif
        }

        // Returns the id of the reward claimable today, or null when nothing can be claimed.
        public void GetCurrentReward(Action<bool, string> onComplete = null)
        {
            _getCurrentRewardCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeDailyRewardsGetCurrentReward();
#else
            OnDailyRewardsGetCurrentRewardCompletedFailed();
#endif
        }

        // Claims the current reward. The callback receives true when the claim
        // succeeded, false otherwise.
        public void ClaimCurrentReward(Action<bool> onComplete = null)
        {
            _claimCurrentRewardCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeDailyRewardsClaimCurrentReward();
#else
            OnDailyRewardsClaimCurrentRewardCompletedFailed();
#endif
        }

        // Called from JS
        private void OnDailyRewardsGetRewardsCompletedSuccess(string result)
        {
            _getRewardsCallback?.Invoke(true, ParseRewards(result));
            _getRewardsCallback = null;
        }

        private void OnDailyRewardsGetRewardsCompletedFailed()
        {
            _getRewardsCallback?.Invoke(false, null);
            _getRewardsCallback = null;
        }

        private void OnDailyRewardsGetCurrentDayCompletedSuccess(string result)
        {
            int.TryParse(result, out var day);
            _getCurrentDayCallback?.Invoke(true, day);
            _getCurrentDayCallback = null;
        }

        private void OnDailyRewardsGetCurrentDayCompletedFailed()
        {
            _getCurrentDayCallback?.Invoke(false, 0);
            _getCurrentDayCallback = null;
        }

        private void OnDailyRewardsGetCurrentRewardCompletedSuccess(string result)
        {
            _getCurrentRewardCallback?.Invoke(true, string.IsNullOrEmpty(result) ? null : result);
            _getCurrentRewardCallback = null;
        }

        private void OnDailyRewardsGetCurrentRewardCompletedFailed()
        {
            _getCurrentRewardCallback?.Invoke(false, null);
            _getCurrentRewardCallback = null;
        }

        private void OnDailyRewardsClaimCurrentRewardCompletedSuccess(string result)
        {
            _claimCurrentRewardCallback?.Invoke(result == "true");
            _claimCurrentRewardCallback = null;
        }

        private void OnDailyRewardsClaimCurrentRewardCompletedFailed()
        {
            _claimCurrentRewardCallback?.Invoke(false);
            _claimCurrentRewardCallback = null;
        }

        private static string[] ParseRewards(string json)
        {
            var result = new List<string>();

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    // JsonUtility cannot parse a top-level array, so wrap it.
                    var wrapper = JsonUtility.FromJson<RewardListWrapper>("{\"items\":" + json + "}");
                    if (wrapper?.items != null)
                    {
                        result = wrapper.items;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }

            return result.ToArray();
        }

        [Serializable]
        private class RewardListWrapper
        {
            public List<string> items;
        }
    }
}
#endif
