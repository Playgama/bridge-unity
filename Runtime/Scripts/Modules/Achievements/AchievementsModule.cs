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
        public bool isSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsAchievementsSupported() == "true";
#else
                return false;
#endif
            }
        }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsAchievementsSupported();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAchievementsUnlock(string id);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAchievementsGetList();
#endif

        private Action<bool> _unlockCallback;
        private Action<bool, List<Dictionary<string, string>>> _getListCallback;

        public void Unlock(string id, Action<bool> onComplete = null)
        {
            _unlockCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAchievementsUnlock(id);
#else
            OnAchievementsUnlockCompleted("false");
#endif
        }

        public void GetList(Action<bool, List<Dictionary<string, string>>> onComplete = null)
        {
            _getListCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAchievementsGetList();
#else
            OnAchievementsGetListCompletedFailed();
#endif
        }
        
        // Called from JS
        private void OnAchievementsUnlockCompleted(string result)
        {
            var isSuccess = result == "true";
            _unlockCallback?.Invoke(isSuccess);
            _unlockCallback = null;
        }

        private void OnAchievementsGetListCompletedSuccess(string result)
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

            _getListCallback?.Invoke(true, achievements);
            _getListCallback = null;
        }

        private void OnAchievementsGetListCompletedFailed()
        {
            _getListCallback?.Invoke(false, null);
            _getListCallback = null;
        }
    }
}
#endif