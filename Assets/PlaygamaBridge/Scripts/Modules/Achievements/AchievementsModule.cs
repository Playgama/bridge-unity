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

        public bool isGetListSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsGetAchievementsListSupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isNativePopupSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsAchievementsNativePopupSupported() == "true";
#else
                return false;
#endif
            }
        }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsAchievementsSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsGetAchievementsListSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsAchievementsNativePopupSupported();
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAchievementsUnlock(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAchievementsGetList(string options);
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAchievementsShowNativePopup(string options);
#endif
        
        private Action<bool> _unlockCallback;
        private Action<bool> _showNativePopupCallback;
        private Action<bool, List<Dictionary<string, string>>> _getListCallback;
        
        public void Unlock(Dictionary<string, object> options, Action<bool> onComplete = null)
        {
            _unlockCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAchievementsUnlock(options.ToJson());
#else
            OnAchievementsUnlockCompleted("false");
#endif
        }
        
        public void ShowNativePopup(Dictionary<string, object> options, Action<bool> onComplete = null)
        {
            _showNativePopupCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAchievementsShowNativePopup(options.ToJson());
#else
            OnAchievementsShowNativePopupCompleted("false");
#endif
        }
        
        public void GetList(Dictionary<string, object> options, Action<bool, List<Dictionary<string, string>>> onComplete = null)
        {
            _getListCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAchievementsGetList(options.ToJson());
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
        
        private void OnAchievementsShowNativePopupCompleted(string result)
        {
            var isSuccess = result == "true";
            _showNativePopupCallback?.Invoke(isSuccess);
            _showNativePopupCallback = null;
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
                    Debug.Log(e);
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