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
        private static extern void PlaygamaBridgeRemoteConfigGet(string options);
#endif
        private Action<bool, Dictionary<string, string>> _getCallback;

        
        public void Get(Action<bool, Dictionary<string, string>> onComplete)
        {
            _getCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeRemoteConfigGet(string.Empty);
#else
            OnRemoteConfigGetFailed();
#endif
        }
        
        public void Get(Dictionary<string, object> options, Action<bool, Dictionary<string, string>> onComplete)
        {
            _getCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeRemoteConfigGet(options.ToJson());
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
                Debug.Log(e);
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