#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;
using Playgama.Common;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using Playgama.Debug;
#endif

namespace Playgama.Modules.CrossPromo
{
    public class CrossPromoModule : MonoBehaviour
    {
        public bool isVisible
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsCrossPromoVisible() == "true";
#else
                return false;
#endif
            }
        }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsCrossPromoVisible();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeCrossPromoGetGamesList();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeCrossPromoShow();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeCrossPromoHide();
#endif
        private Action<bool, List<Dictionary<string, string>>> _getGamesListCallback;

        public void GetGames(Action<bool, List<Dictionary<string, string>>> onComplete = null)
        {
            _getGamesListCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeCrossPromoGetGamesList();
#else
            OnCrossPromoGetGamesListCompletedFailed();
#endif
        }

        public void Show()
        {
#if !UNITY_EDITOR
            PlaygamaBridgeCrossPromoShow();
#endif
        }

        public void Hide()
        {
#if !UNITY_EDITOR
            PlaygamaBridgeCrossPromoHide();
#endif
        }


        // Called from JS
        private void OnCrossPromoGetGamesListCompletedSuccess(string result)
        {
            var games = new List<Dictionary<string, string>>();

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    games = JsonHelper.FromJsonToListOfDictionaries(result);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }

            _getGamesListCallback?.Invoke(true, games);
            _getGamesListCallback = null;
        }

        private void OnCrossPromoGetGamesListCompletedFailed()
        {
            _getGamesListCallback?.Invoke(false, null);
            _getGamesListCallback = null;
        }
    }
}
#endif
