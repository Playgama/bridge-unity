#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
using Playgama.Common;
#endif

namespace Playgama.Modules.Social
{
    public class SocialModule : MonoBehaviour
    {
        public bool isShareSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsShareSupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isInviteFriendsSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsInviteFriendsSupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isJoinCommunitySupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsJoinCommunitySupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isCreatePostSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsCreatePostSupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isAddToHomeScreenSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsAddToHomeScreenSupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isAddToHomeScreenRewardSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsAddToHomeScreenRewardSupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isAddToFavoritesSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsAddToFavoritesSupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isAddToFavoritesRewardSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsAddToFavoritesRewardSupported() == "true";
#else
                return false;
#endif
            }
        }

        public bool isRateSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsRateSupported() == "true";
#else
                return false;
#endif
            }
        }
        
        public bool isExternalLinksAllowed
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsExternalLinksAllowed() == "true";
#else
                return true;
#endif
            }
        }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsShareSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsInviteFriendsSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsJoinCommunitySupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsCreatePostSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsAddToHomeScreenSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsAddToHomeScreenRewardSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsAddToFavoritesSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsAddToFavoritesRewardSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsRateSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsExternalLinksAllowed();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeShare(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeInviteFriends(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeJoinCommunity(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeCreatePost(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAddToHomeScreen();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAddToFavorites();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeRate();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeGetAddToHomeScreenReward();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeGetAddToFavoritesReward();
#endif

        private Action<bool> _shareCallback;
        private Action<bool> _inviteFriendsCallback;
        private Action<bool> _joinCommunityCallback;
        private Action<bool> _createPostCallback;
        private Action<bool> _addToHomeScreenCallback;
        private Action<bool> _addToFavoritesCallback;
        private Action<bool> _rateCallback;
        private Action<bool> _getAddToHomeScreenRewardCallback;
        private Action<bool> _getAddToFavoritesRewardCallback;


        public void Share(Dictionary<string, object> options, Action<bool> onComplete = null)
        {
            _shareCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeShare(options.ToJson());
#else
            OnShareCompleted("false");
#endif
        }

        public void InviteFriends(Dictionary<string, object> options, Action<bool> onComplete = null)
        {
            _inviteFriendsCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeInviteFriends(options.ToJson());
#else
            OnInviteFriendsCompleted("false");
#endif
        }

        public void JoinCommunity(Dictionary<string, object> options, Action<bool> onComplete = null)
        {
            _joinCommunityCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeJoinCommunity(options.ToJson());
#else
            OnJoinCommunityCompleted("false");
#endif
        }

        public void CreatePost(Dictionary<string, object> options, Action<bool> onComplete = null)
        {
            _createPostCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeCreatePost(options.ToJson());
#else
            OnCreatePostCompleted("false");
#endif
        }

        public void AddToHomeScreen(Action<bool> onComplete = null)
        {
            _addToHomeScreenCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeAddToHomeScreen();
#else
            OnAddToHomeScreenCompleted("false");
#endif
        }

        public void AddToFavorites(Action<bool> onComplete = null)
        {
            _addToFavoritesCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeAddToFavorites();
#else
            OnAddToFavoritesCompleted("false");
#endif
        }

        public void Rate(Action<bool> onComplete = null)
        {
            _rateCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeRate();
#else
            OnRateCompleted("false");
#endif
        }

        public void GetAddToHomeScreenReward(Action<bool> onComplete = null)
        {
            _getAddToHomeScreenRewardCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeGetAddToHomeScreenReward();
#else
            OnGetAddToHomeScreenRewardCompleted("false");
#endif
        }

        public void GetAddToFavoritesReward(Action<bool> onComplete = null)
        {
            _getAddToFavoritesRewardCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeGetAddToFavoritesReward();
#else
            OnGetAddToFavoritesRewardCompleted("false");
#endif
        }


        // Called from JS
        private void OnShareCompleted(string result)
        {
            var isSuccess = result == "true";
            _shareCallback?.Invoke(isSuccess);
            _shareCallback = null;
        }

        private void OnInviteFriendsCompleted(string result)
        {
            var isSuccess = result == "true";
            _inviteFriendsCallback?.Invoke(isSuccess);
            _inviteFriendsCallback = null;
        }

        private void OnJoinCommunityCompleted(string result)
        {
            var isSuccess = result == "true";
            _joinCommunityCallback?.Invoke(isSuccess);
            _joinCommunityCallback = null;
        }

        private void OnCreatePostCompleted(string result)
        {
            var isSuccess = result == "true";
            _createPostCallback?.Invoke(isSuccess);
            _createPostCallback = null;
        }

        private void OnAddToHomeScreenCompleted(string result)
        {
            var isSuccess = result == "true";
            _addToHomeScreenCallback?.Invoke(isSuccess);
            _addToHomeScreenCallback = null;
        }

        private void OnAddToFavoritesCompleted(string result)
        {
            var isSuccess = result == "true";
            _addToFavoritesCallback?.Invoke(isSuccess);
            _addToFavoritesCallback = null;
        }

        private void OnRateCompleted(string result)
        {
            var isSuccess = result == "true";
            _rateCallback?.Invoke(isSuccess);
            _rateCallback = null;
        }

        private void OnGetAddToHomeScreenRewardCompleted(string result)
        {
            var canReceiveReward = result == "true";
            _getAddToHomeScreenRewardCallback?.Invoke(canReceiveReward);
            _getAddToHomeScreenRewardCallback = null;
        }

        private void OnGetAddToFavoritesRewardCompleted(string result)
        {
            var canReceiveReward = result == "true";
            _getAddToFavoritesRewardCallback?.Invoke(canReceiveReward);
            _getAddToFavoritesRewardCallback = null;
        }
    }
}
#endif