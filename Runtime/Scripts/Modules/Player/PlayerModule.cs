#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_EDITOR
using Playgama.Common;
using System.Runtime.InteropServices;
#else
using Playgama.Debug;
#endif

namespace Playgama.Modules.Player
{
    public class PlayerModule : MonoBehaviour
    {
        public bool isAuthorizationSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsPlayerAuthorizationSupported() == "true";
#else
                return true;
#endif
            }
        }

        public bool isAuthorized
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsPlayerAuthorized() == "true";
#else
                return _isAuthorized;
#endif
            }
        }

        public string id
        {
            get
            {
#if !UNITY_EDITOR
                var value = PlaygamaBridgePlayerId();
                if (string.IsNullOrEmpty(value)) {
                    return null;
                }

                return value;
#else
                return _isAuthorized ? _id : null;
#endif
            }
        }

        public new string name
        {
            get
            {
#if !UNITY_EDITOR
                var value = PlaygamaBridgePlayerName();
                if (string.IsNullOrEmpty(value)) {
                    return null;
                }

                return value;
#else
                return _isAuthorized ? _name : null;
#endif
            }
        }

        public List<string> photos
        {
            get
            {
#if !UNITY_EDITOR
                var json = PlaygamaBridgePlayerPhotos();
                if (string.IsNullOrEmpty(json)) {
                    return new List<string>();
                }
                
                try
                {
                    return JsonHelper.FromJsonToListOfStrings(json);
                }
                catch (Exception)
                {
                    return new List<string>();
                }
#else
                return new List<string>();
#endif
            }
        }
        
        public Dictionary<string, string> extra
        {
            get
            {
#if !UNITY_EDITOR
                var json = PlaygamaBridgePlayerExtra();
                if (string.IsNullOrEmpty(json))
                {
                    return new Dictionary<string, string>();
                }
                
                try
                {
                    return JsonHelper.FromJsonToDictionary(json);
                }
                catch (Exception)
                {
                    return new Dictionary<string, string>();
                }
#else
                return new Dictionary<string, string>();
#endif
            }
        }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsPlayerAuthorizationSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsPlayerAuthorized();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgePlayerId();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgePlayerName();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgePlayerPhotos();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgePlayerExtra();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAuthorizePlayer(string options);
#endif

#if UNITY_EDITOR
		private bool _isAuthorized = false;
		private string _id = "player_12345";
		private string _name = "Player 12345";
#endif
        private Action<bool> _authorizationCallback;


        public void Authorize(Dictionary<string, object> options = null, Action<bool> onComplete = null)
        {
            _authorizationCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgeAuthorizePlayer(options.ToJson());
#else
			if (_isAuthorized) {
				OnAuthorizeCompleted("true");
				return;
			}

            DebugWindow.ShowSimple("Authorize Player", 
				() => {
					_isAuthorized = true;
					OnAuthorizeCompleted("true");
				},
				() => OnAuthorizeCompleted("false"));
#endif
        }


        // Called from JS
        private void OnAuthorizeCompleted(string result)
        {
            var isSuccess = result == "true";
            _authorizationCallback?.Invoke(isSuccess);
            _authorizationCallback = null;
        }
    }
}
#endif