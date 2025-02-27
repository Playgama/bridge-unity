using System;
using Playgama;
using Playgama.Modules.Advertisement;
using Playgama.Modules.Game;
using UnityEngine;

namespace Examples
{
    public class Example : MonoBehaviour
    {
        [SerializeField] private AudioSource _musicAudioSource;

        private void Start()
        {
            _musicAudioSource.Play();
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
                    _musicAudioSource.Play();
                    break;

                case VisibilityState.Hidden:
                    _musicAudioSource.Pause();
                    break;
            }
        }

        private void OnInterstitialStateChanged(InterstitialState state)
        {
            switch (state)
            {
                case InterstitialState.Loading:
                case InterstitialState.Opened:
                    _musicAudioSource.Pause();
                    break;
                case InterstitialState.Closed:
                case InterstitialState.Failed:
                    _musicAudioSource.Play();
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
                    _musicAudioSource.Pause();
                    break;
                case RewardedState.Rewarded:
                    break;
                case RewardedState.Closed:
                case RewardedState.Failed:
                    _musicAudioSource.Play();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}