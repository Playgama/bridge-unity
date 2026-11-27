#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Globalization;
using Playgama.Common;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Leaderboards
{
    public class LeaderboardsModule : MonoBehaviour
    {
        public LeaderboardType type
        {
            get
            {
#if !UNITY_EDITOR
                var stringType = PlaygamaBridgeLeaderboardsType();

                switch (stringType)
                {
                    case "in_game":
                        return LeaderboardType.InGame;
                    case "native":
                        return LeaderboardType.Native;
                    case "native_popup":
                        return LeaderboardType.NativePopup;
                }

                return LeaderboardType.NotAvailable;
#else
                return LeaderboardType.NotAvailable;
#endif
            }
        }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeLeaderboardsType();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeLeaderboardsSetScore(string id, string score);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeLeaderboardsGetEntries(string id);
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeLeaderboardsGetInfo(string id);
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeLeaderboardsShowNativePopup(string id);
        
#endif

        private Action<bool> _setScoreCallback;
        private Action<bool, List<Dictionary<string, string>>> _getEntriesCallback;
        private Action<bool, LeaderboardInfo> _getInfoCallback;
        private Action<bool> _showNativePopupCallback;

        
        public void SetScore(string id, int score, Action<bool> onComplete = null)
        {
            SetScore(id, score.ToString(), onComplete);
        }
        
        public void SetScore(string id, string score, Action<bool> onComplete = null)
        {
            _setScoreCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeLeaderboardsSetScore(id, score);
#else
            OnLeaderboardsSetScoreCompleted("false");
#endif
        }

        public void GetEntries(string id, Action<bool, List<Dictionary<string, string>>> onComplete)
        {
            _getEntriesCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeLeaderboardsGetEntries(id);
#else
            OnLeaderboardsGetEntriesCompletedFailed();
#endif
        }
        
        // Period of the leaderboard and seconds until it resets. Native platform leaderboards
        // are always Permanent. Fails when leaderboards are not available or the request fails.
        public void GetInfo(string id, Action<bool, LeaderboardInfo> onComplete)
        {
            _getInfoCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeLeaderboardsGetInfo(id);
#else
            OnLeaderboardsGetInfoCompletedFailed();
#endif
        }

        public void ShowNativePopup(string id, Action<bool> onComplete = null)
        {
            _showNativePopupCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeLeaderboardsShowNativePopup(id);
#else
            OnLeaderboardsShowNativePopupCompleted("false");
#endif
        }

        // Called from JS
        private void OnLeaderboardsSetScoreCompleted(string result)
        {
            var isSuccess = result == "true";
            _setScoreCallback?.Invoke(isSuccess);
            _setScoreCallback = null;
        }

        private void OnLeaderboardsGetEntriesCompletedSuccess(string result)
        {
            var entries = new List<Dictionary<string, string>>();

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    entries = JsonHelper.FromJsonToListOfDictionaries(result);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }

            _getEntriesCallback?.Invoke(true, entries);
            _getEntriesCallback = null;
        }

        private void OnLeaderboardsGetEntriesCompletedFailed()
        {
            _getEntriesCallback?.Invoke(false, null);
            _getEntriesCallback = null;
        }
        
        private void OnLeaderboardsGetInfoCompletedSuccess(string result)
        {
            _getInfoCallback?.Invoke(true, ParseInfo(result));
            _getInfoCallback = null;
        }

        private void OnLeaderboardsGetInfoCompletedFailed()
        {
            _getInfoCallback?.Invoke(false, null);
            _getInfoCallback = null;
        }

        private void OnLeaderboardsShowNativePopupCompleted(string result)
        {
            var isSuccess = result == "true";
            _showNativePopupCallback?.Invoke(isSuccess);
            _showNativePopupCallback = null;
        }
        
        private static LeaderboardInfo ParseInfo(string json)
        {
            var data = string.IsNullOrEmpty(json)
                ? new Dictionary<string, string>()
                : JsonHelper.FromJsonToDictionary(json);

            data.TryGetValue("id", out var id);
            data.TryGetValue("period", out var period);
            data.TryGetValue("timeLeft", out var timeLeft);

            return new LeaderboardInfo
            {
                id = id,
                period = ToPeriod(period),
                timeLeft = int.TryParse(timeLeft, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                    ? seconds
                    : (int?)null
            };
        }

        private static LeaderboardPeriod ToPeriod(string value)
        {
            switch (value)
            {
                case "daily":
                    return LeaderboardPeriod.Daily;
                case "weekly":
                    return LeaderboardPeriod.Weekly;
                case "monthly":
                    return LeaderboardPeriod.Monthly;
            }

            return LeaderboardPeriod.Permanent;
        }
    }
}
#endif