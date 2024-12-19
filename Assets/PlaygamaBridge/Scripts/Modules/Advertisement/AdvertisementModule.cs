#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;
#if !UNITY_EDITOR
using Playgama.Common;
using System.Runtime.InteropServices;

#endif

namespace Playgama.Modules.Advertisement
{
    public class AdvertisementModule : MonoBehaviour
    {
        public event Action<BannerState> bannerStateChanged;
        public event Action<InterstitialState> interstitialStateChanged;
        public event Action<RewardedState> rewardedStateChanged;

        public BannerState bannerState { get; private set; } = BannerState.Hidden;
        public InterstitialState interstitialState { get; private set; } = InterstitialState.Closed;
        public RewardedState rewardedState { get; private set; } = RewardedState.Closed;

        public int minimumDelayBetweenInterstitial
        {
            get
            {
#if !UNITY_EDITOR
                int.TryParse(PlaygamaBridgeMinimumDelayBetweenInterstitial(), out int value);
                return value;
#else
                return _minimumDelayBetweenInterstitial;
#endif
            }
        }
        
        public bool isBannerSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsBannerSupported() == "true";
#else
                return false;
#endif
            }
        }
        
#if UNITY_EDITOR
        private int _minimumDelayBetweenInterstitial;

        private DateTime _lastInterstitialShownTimestamp = DateTime.MinValue;
#else
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetInterstitialState();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeMinimumDelayBetweenInterstitial();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeSetMinimumDelayBetweenInterstitial(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeShowInterstitial();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeShowRewarded();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsBannerSupported();
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeShowBanner(string options);
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeHideBanner();
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeCheckAdBlock();
#endif
        private Action<bool> _checkAdBlockCallback;

        
        public void ShowBanner(Dictionary<string, object> options = null)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeShowBanner(options.ToJson());
#else
            OnBannerStateChanged(BannerState.Loading.ToString());
            OnBannerStateChanged(BannerState.Shown.ToString());
#endif
        }

        public void HideBanner()
        {
#if !UNITY_EDITOR
            PlaygamaBridgeHideBanner();
#else
            OnBannerStateChanged(BannerState.Hidden.ToString());
#endif
        }


        public void SetMinimumDelayBetweenInterstitial(int seconds)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeSetMinimumDelayBetweenInterstitial(seconds.ToString());
#else
            _minimumDelayBetweenInterstitial = seconds;
#endif
        }

        public void ShowInterstitial()
        {
#if !UNITY_EDITOR
            PlaygamaBridgeShowInterstitial();
#else
            var delta = DateTime.Now - _lastInterstitialShownTimestamp;
            if (delta.TotalSeconds > _minimumDelayBetweenInterstitial)
            {
                OnInterstitialStateChanged(InterstitialState.Loading.ToString());
                OnInterstitialStateChanged(InterstitialState.Opened.ToString());
                OnInterstitialStateChanged(InterstitialState.Closed.ToString());
            }
            else
            {
                OnInterstitialStateChanged(InterstitialState.Failed.ToString());
            }
#endif
        }

        public void ShowRewarded()
        {
#if !UNITY_EDITOR
            PlaygamaBridgeShowRewarded();
#else
            OnRewardedStateChanged(RewardedState.Loading.ToString());
            OnRewardedStateChanged(RewardedState.Opened.ToString());
            OnRewardedStateChanged(RewardedState.Rewarded.ToString());
            OnRewardedStateChanged(RewardedState.Closed.ToString());
#endif
        }

        public void CheckAdBlock(Action<bool> callback)
        {
            _checkAdBlockCallback = callback;
#if !UNITY_EDITOR
            PlaygamaBridgeCheckAdBlock();
#else
            OnCheckAdBlockCompleted("false");
#endif
        }

        
        private void Awake()
        {
#if !UNITY_EDITOR
            string value = PlaygamaBridgeGetInterstitialState();
            if (Enum.TryParse<InterstitialState>(value, true, out var state))
            {
                interstitialState = state;
            }
#endif
        }
        
        // Called from JS
        private void OnBannerStateChanged(string value)
        {
            if (Enum.TryParse<BannerState>(value, true, out var state))
            {
                bannerState = state;
                bannerStateChanged?.Invoke(bannerState);
            }
        }

        private void OnInterstitialStateChanged(string value)
        {
            if (Enum.TryParse<InterstitialState>(value, true, out var state))
            {
                interstitialState = state;
                interstitialStateChanged?.Invoke(interstitialState);
                
#if UNITY_EDITOR
                if (interstitialState == InterstitialState.Closed)
                {
                    _lastInterstitialShownTimestamp = DateTime.Now;
                }
#endif
            }
        }

        private void OnRewardedStateChanged(string value)
        {
            if (Enum.TryParse<RewardedState>(value, true, out var state))
            {
                rewardedState = state;
                rewardedStateChanged?.Invoke(rewardedState);
            }
        }
        
        private void OnCheckAdBlockCompleted(string result)
        {
            _checkAdBlockCallback?.Invoke(result == "true");
            _checkAdBlockCallback = null;
        }
    }
}
#endif