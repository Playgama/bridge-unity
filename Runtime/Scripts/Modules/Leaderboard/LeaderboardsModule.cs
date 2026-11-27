#if UNITY_WEBGL
using System;
using System.Collections.Generic;
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
        private static extern void PlaygamaBridgeLeaderboardsGetEntries(string id, string period);
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeLeaderboardsGetInfo(string id);
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeLeaderboardsShowNativePopup(string id);
        
#endif

        private Action<bool> _setScoreCallback;
        private Action<bool, List<Dictionary<string, string>>> _getEntriesCallback;
        private Action<bool, LeaderboardInfo> _getInfoCallback;
        private Action<bool> _showNativePopupCallback;

        
        // Best score mode keeps the best score, Sum mode adds the score, so send what the player earned.
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
            GetEntries(id, LeaderboardPeriod.Permanent, onComplete);
        }

        // Entries of the given standing. Platform leaderboards have only Permanent, other periods fail there.
        public void GetEntries(string id, LeaderboardPeriod period, Action<bool, List<Dictionary<string, string>>> onComplete)
        {
            _getEntriesCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeLeaderboardsGetEntries(id, ToPeriodString(period));
#else
            OnLeaderboardsGetEntriesCompletedFailed();
#endif
        }
        
        // Score mode, readable periods and seconds until each period resets. Platform leaderboards
        // are Best with only Permanent. Fails when leaderboards are not available or the request fails.
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
            var info = new LeaderboardInfo();

            if (string.IsNullOrEmpty(json))
            {
                return info;
            }

            try
            {
                var data = JsonUtility.FromJson<LeaderboardInfoJson>(json);
                info.id = data.id;
                info.scoreMode = data.scoreMode == "sum" ? LeaderboardScoreMode.Sum : LeaderboardScoreMode.Best;

                foreach (var value in data.periods)
                {
                    var period = ToPeriod(value);
                    if (period == null)
                    {
                        continue;
                    }

                    info.periods.Add(period.Value);

                    // timeLeft has a key for every listed period except permanent
                    switch (period.Value)
                    {
                        case LeaderboardPeriod.Daily:
                            info.SetTimeLeft(period.Value, data.timeLeft.daily);
                            break;
                        case LeaderboardPeriod.Weekly:
                            info.SetTimeLeft(period.Value, data.timeLeft.weekly);
                            break;
                        case LeaderboardPeriod.Monthly:
                            info.SetTimeLeft(period.Value, data.timeLeft.monthly);
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e);
            }

            return info;
        }

        private static LeaderboardPeriod? ToPeriod(string value)
        {
            switch (value)
            {
                case "permanent":
                    return LeaderboardPeriod.Permanent;
                case "daily":
                    return LeaderboardPeriod.Daily;
                case "weekly":
                    return LeaderboardPeriod.Weekly;
                case "monthly":
                    return LeaderboardPeriod.Monthly;
            }

            return null;
        }

        private static string ToPeriodString(LeaderboardPeriod period)
        {
            switch (period)
            {
                case LeaderboardPeriod.Daily:
                    return "daily";
                case LeaderboardPeriod.Weekly:
                    return "weekly";
                case LeaderboardPeriod.Monthly:
                    return "monthly";
            }

            return "permanent";
        }

        // Filled by JsonUtility
#pragma warning disable CS0649
        [Serializable]
        private class LeaderboardInfoJson
        {
            public string id;
            public string scoreMode;
            public List<string> periods = new List<string>();
            public TimeLeftJson timeLeft = new TimeLeftJson();
        }

        [Serializable]
        private class TimeLeftJson
        {
            public int daily;
            public int weekly;
            public int monthly;
        }
#pragma warning restore CS0649
    }
}
#endif