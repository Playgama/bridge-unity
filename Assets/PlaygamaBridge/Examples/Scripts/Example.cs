using System;
using UnityEngine;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Advertisement;
using Playgama.Modules.Game;
#endif

namespace Examples
{
    public class Example : MonoBehaviour
    {
#if UNITY_WEBGL
        private void Start()
        {
            Bridge.game.visibilityStateChanged += OnGameVisibilityStateChanged;
            Bridge.advertisement.interstitialStateChanged += OnInterstitialStateChanged;
            Bridge.advertisement.rewardedStateChanged += OnRewardedStateChanged;
        }

        private void OnDestroy()
        {
            if (Bridge.instance != null)
            {
                Bridge.game.visibilityStateChanged -= OnGameVisibilityStateChanged;
                Bridge.advertisement.interstitialStateChanged -= OnInterstitialStateChanged;
                Bridge.advertisement.rewardedStateChanged -= OnRewardedStateChanged;
            }
        }

        private void OnGameVisibilityStateChanged(VisibilityState visibilityState)
        {
            switch (visibilityState)
            {
                case VisibilityState.Visible:
                    // play music
                    break;

                case VisibilityState.Hidden:
                    // pause music
                    break;
            }
        }

        private void OnInterstitialStateChanged(InterstitialState state)
        {
            switch (state)
            {
                case InterstitialState.Loading:
                case InterstitialState.Opened:
                    // pause music
                    break;
                case InterstitialState.Closed:
                case InterstitialState.Failed:
                    // play music
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private void OnRewardedStateChanged(RewardedState state)
        {
            switch (state)
            {
                case RewardedState.Loading:
                case RewardedState.Opened:
                    // pause music
                    break;
                case RewardedState.Rewarded:
                    break;
                case RewardedState.Closed:
                case RewardedState.Failed:
                    // play music
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
#endif
    }
}