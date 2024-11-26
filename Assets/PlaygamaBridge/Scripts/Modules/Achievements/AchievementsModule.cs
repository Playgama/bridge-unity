/*
 * This file is part of Playgama Bridge.
 *
 * Playgama Bridge is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * any later version.
 *
 * Playgama Bridge is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with Playgama Bridge. If not, see <https://www.gnu.org/licenses/>.
*/

#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
#if !UNITY_EDITOR
using Playgama.Common;
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
        private static extern void PlaygamaBridgeAchievementsGetList();
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAchievementsShowNativePopup();
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
        
        public void ShowNativePopup(Action<bool> onComplete = null)
        {
            _showNativePopupCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAchievementsShowNativePopup();
#else
            OnAchievementsShowNativePopupCompleted("false");
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
                    achievements = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(result);
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