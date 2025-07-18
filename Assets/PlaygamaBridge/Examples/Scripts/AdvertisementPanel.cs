using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Advertisement;
#endif

namespace Examples
{
    public class AdvertisementPanel : MonoBehaviour
    {
        [SerializeField] private Text _bannerState;
        [SerializeField] private Text _interstitialState;
        [SerializeField] private Text _rewardedState;
        [SerializeField] private Text _isBannerSupported;
        [SerializeField] private Text _isInterstitialSupported;
        [SerializeField] private Text _isRewardedSupported;
        [SerializeField] private InputField _minimumDelayBetweenInterstitial;
        [SerializeField] private Button _setMinimumDelayBetweenInterstitialButton;
        [SerializeField] private Button _showBannerButton;
        [SerializeField] private Button _hideBannerButton;
        [SerializeField] private Button _showInterstitialButton;
        [SerializeField] private Button _showRewardedButton;
        [SerializeField] private Text _rewardedPlacementText;
        [SerializeField] private Text _adBlockDetectedText;
        [SerializeField] private Button _checkAdBlockButton;
        [SerializeField] private GameObject _overlay;

        private readonly List<BannerState> _lastBannerStates = new();
        private readonly List<InterstitialState> _lastInterstitialStates = new();
        private readonly List<RewardedState> _lastRewardedStates = new();

#if UNITY_WEBGL
        private void Start()
        {
            Bridge.advertisement.bannerStateChanged += OnBannerStateChanged;
            Bridge.advertisement.interstitialStateChanged += OnInterstitialStateChanged;
            Bridge.advertisement.rewardedStateChanged += OnRewardedStateChanged;
            
            _setMinimumDelayBetweenInterstitialButton.onClick.AddListener(OnSetMinimumDelayBetweenInterstitialButtonClicked);
            _showBannerButton.onClick.AddListener(OnShowBannerButtonClicked);
            _hideBannerButton.onClick.AddListener(OnHideBannerButtonClicked);
            _showInterstitialButton.onClick.AddListener(OnShowInterstitialButtonClicked);
            _showRewardedButton.onClick.AddListener(OnShowRewardedButtonClicked);
            _checkAdBlockButton.onClick.AddListener(OnCheckAdBlockButtonClicked);

            _isBannerSupported.text = $"Is Banner Supported: { Bridge.advertisement.isBannerSupported }";
            _isInterstitialSupported.text = $"Is Interstitial Supported: { Bridge.advertisement.isInterstitialSupported }";
            _isRewardedSupported.text = $"Is Rewarded Supported: { Bridge.advertisement.isRewardedSupported }";

            OnBannerStateChanged(Bridge.advertisement.bannerState);
            OnInterstitialStateChanged(Bridge.advertisement.interstitialState);
            OnRewardedStateChanged(Bridge.advertisement.rewardedState);
            UpdateMinimumDelayBetweenInterstitial();
        }

        private void OnDestroy()
        {
            if (Bridge.instance != null)
            {
                Bridge.advertisement.bannerStateChanged -= OnBannerStateChanged;
                Bridge.advertisement.interstitialStateChanged -= OnInterstitialStateChanged;
                Bridge.advertisement.rewardedStateChanged -= OnRewardedStateChanged;
            }
        }

        private void OnBannerStateChanged(BannerState state)
        {
            _lastBannerStates.Add(state);

            if (_lastBannerStates.Count > 3)
            {
                _lastBannerStates.RemoveRange(0, _lastBannerStates.Count - 3);
            }

            _bannerState.text = $"Last Banner States: { string.Join(" → ", _lastBannerStates) }";
        }

        private void OnInterstitialStateChanged(InterstitialState state)
        {
            switch (state)
            {
                case InterstitialState.Loading:
                    _overlay.SetActive(true);
                    break;

                case InterstitialState.Closed:
                case InterstitialState.Failed:
                    _overlay.SetActive(false);
                    break;
            }
            
            _lastInterstitialStates.Add(state);

            if (_lastInterstitialStates.Count > 3)
            {
                _lastInterstitialStates.RemoveRange(0, _lastInterstitialStates.Count - 3);
            }

            _interstitialState.text = $"Last Interstitial States: { string.Join(" → ", _lastInterstitialStates) }";
        }

        private void OnRewardedStateChanged(RewardedState state)
        {
            switch (state)
            {
                case RewardedState.Loading:
                    _overlay.SetActive(true);
                    break;

                case RewardedState.Closed:
                case RewardedState.Failed:
                    _overlay.SetActive(false);
                    break;
            }
            
            _lastRewardedStates.Add(state);

            if (_lastRewardedStates.Count > 3)
            {
                _lastRewardedStates.RemoveRange(0, _lastRewardedStates.Count - 3);
            }

            _rewardedState.text = $"Last Rewarded States: { string.Join(" → ", _lastRewardedStates) }";
            _rewardedPlacementText.text = $"Rewarded Placement: { Bridge.advertisement.rewardedPlacement }";
        }

        private void OnSetMinimumDelayBetweenInterstitialButtonClicked()
        {
            int.TryParse(_minimumDelayBetweenInterstitial.text, out var seconds);
            Bridge.advertisement.SetMinimumDelayBetweenInterstitial(seconds);
            UpdateMinimumDelayBetweenInterstitial();
        }
        
        private void OnShowBannerButtonClicked()
        {
            Bridge.advertisement.ShowBanner();
        }

        private void OnHideBannerButtonClicked()
        {
            Bridge.advertisement.HideBanner();
        }

        private void OnShowInterstitialButtonClicked()
        {
            Bridge.advertisement.ShowInterstitial();
        }

        private void OnShowRewardedButtonClicked()
        {
            Bridge.advertisement.ShowRewarded();
        }

        private void OnCheckAdBlockButtonClicked()
        {
            _overlay.SetActive(true);
            Bridge.advertisement.CheckAdBlock(isAdBlockDetected =>
            {
                _adBlockDetectedText.text = $"AdBlock Detected: {isAdBlockDetected}";
                _overlay.SetActive(false);
            });
        }

        private void UpdateMinimumDelayBetweenInterstitial()
        {
            _minimumDelayBetweenInterstitial.text = Bridge.advertisement.minimumDelayBetweenInterstitial.ToString();
        }
#endif
    }
}