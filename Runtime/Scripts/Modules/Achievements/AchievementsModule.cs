#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using Playgama.Common;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Achievements
{
    public class AchievementsModule : MonoBehaviour
    {
#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAchievementsUnlock(string id);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAchievementsGetAchievements();
#endif

        private Action<bool> _unlockCallback;
        private Action<bool, List<Dictionary<string, string>>> _getAchievementsCallback;

        public void Unlock(string id, Action<bool> onComplete = null)
        {
            _unlockCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAchievementsUnlock(id);
#else
            OnAchievementsUnlockCompleted("false");
#endif
        }

        public void GetAchievements(Action<bool, List<Dictionary<string, string>>> onComplete = null)
        {
            _getAchievementsCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAchievementsGetAchievements();
#else
            OnAchievementsGetAchievementsCompletedFailed();
#endif
        }

        // Called from JS
        private void OnAchievementsUnlockCompleted(string result)
        {
            var isSuccess = result == "true";
            _unlockCallback?.Invoke(isSuccess);
            _unlockCallback = null;
        }

        private void OnAchievementsGetAchievementsCompletedSuccess(string result)
        {
            var achievements = new List<Dictionary<string, string>>();

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    achievements = JsonHelper.FromJsonToListOfDictionaries(result);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }

            _getAchievementsCallback?.Invoke(true, achievements);
            _getAchievementsCallback = null;
        }

        private void OnAchievementsGetAchievementsCompletedFailed()
        {
            _getAchievementsCallback?.Invoke(false, null);
            _getAchievementsCallback = null;
        }
    }
}
#endif
