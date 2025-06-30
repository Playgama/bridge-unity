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
#endif

        private Action<bool> _setScoreCallback;
        private Action<bool, List<Dictionary<string, string>>> _getEntriesCallback;

        
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
                    Debug.Log(e);
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
    }
}
#endif