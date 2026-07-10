#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using Playgama.Common;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.RemoteConfig
{
    public class RemoteConfigModule : MonoBehaviour
    {
        public bool isSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsRemoteConfigSupported() == "true";
#else
                return false;
#endif
            }
        }
        
#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsRemoteConfigSupported();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeRemoteConfigSetContext(string parameters);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeRemoteConfigGet();
#endif
        private Action<bool, Dictionary<string, string>> _getCallback;


        // Sets dynamic game/player parameters used for config segmentation.
        // Accumulates across calls; only used by platforms that support it.
        public void SetContext(Dictionary<string, object> parameters)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeRemoteConfigSetContext(parameters.ToJson());
#endif
        }

        public void Get(Action<bool, Dictionary<string, string>> onComplete)
        {
            _getCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeRemoteConfigGet();
#else
            OnRemoteConfigGetFailed();
#endif
        }


        // Called from JS
        private void OnRemoteConfigGetSuccess(string result)
        {
            var values = new Dictionary<string, string>();
            
            try
            {
                values = JsonHelper.FromJsonToDictionary(result);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e);
            }
            
            _getCallback?.Invoke(true, values);
        }

        private void OnRemoteConfigGetFailed()
        {
            _getCallback?.Invoke(false, null);
        }
    }
}
#endif