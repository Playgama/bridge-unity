using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL
using Playgama;
using Playgama.Modules.Platform;
#endif

namespace Examples
{
    public class PlatformPanel : MonoBehaviour
    {
        [SerializeField] private Text _id;
        [SerializeField] private Text _language;
        [SerializeField] private Text _payload;
        [SerializeField] private Text _tld;
        [SerializeField] private Text _isAudioEnabled;
        [SerializeField] private Text _isGetAllGamesSupported;
        [SerializeField] private Text _isGetGameByIdSupported;
        [SerializeField] private InputField _gameIdInputField;

        [SerializeField] private Button _sendGameReadyMessageButton;
        [SerializeField] private Button _sendInGameLoadingStartedMessageButton;
        [SerializeField] private Button _sendInGameLoadingStoppedMessageButton;
        [SerializeField] private Button _sendGameplayStartedMessageButton;
        [SerializeField] private Button _sendGameplayStoppedMessageButton;
        [SerializeField] private Button _sendPlayerGotAchievementMessageButton;
        [SerializeField] private Text _serverTimeText;
        [SerializeField] private Button _getServerTimeButton;
        [SerializeField] private Button _getAllGamesButton;
        [SerializeField] private Button _getGameByIdButton;
        [SerializeField] private GameObject _overlay;

#if UNITY_WEBGL
        private void Start()
        {
            _id.text = $"ID: { Bridge.platform.id }";
            _language.text = $"Language: { Bridge.platform.language }";
            _payload.text = $"Payload: { Bridge.platform.payload }";
            _tld.text = $"TLD: { Bridge.platform.tld }";

            _isAudioEnabled.text = $"Is Audio Enabled: { Bridge.platform.isAudioEnabled }";
            _isGetAllGamesSupported.text = $"Is Get All Games Supported: { Bridge.platform.isGetAllGamesSupported }";
            _isGetGameByIdSupported.text = $"Is Get Game By Id Supported: { Bridge.platform.isGetGameByIdSupported }";
            
            _sendGameReadyMessageButton.onClick.AddListener(OnSendGameReadyMessageButtonClicked);
            _sendInGameLoadingStartedMessageButton.onClick.AddListener(OnSendInGameLoadingStartedMessageButtonClicked);
            _sendInGameLoadingStoppedMessageButton.onClick.AddListener(OnSendInGameLoadingStoppedMessageButtonClicked);
            _sendGameplayStartedMessageButton.onClick.AddListener(OnSendGameplayStartedMessageButtonClicked);
            _sendGameplayStoppedMessageButton.onClick.AddListener(OnSendGameplayStoppedMessageButtonClicked);
            _sendPlayerGotAchievementMessageButton.onClick.AddListener(OnSendPlayerGotAchievementMessageButtonClicked);
            _getServerTimeButton.onClick.AddListener(OnGetServerTimeButtonClicked);
            
            _getAllGamesButton.onClick.AddListener(OnGetAllGamesButtonClicked);
            _getGameByIdButton.onClick.AddListener(OnGetGameByIdButtonClicked);
            
            Bridge.platform.audioStateChanged += OnPlatformAudioStateChanged;
            Bridge.platform.pauseStateChanged += OnPlatformPauseStateChanged;
        }

        private void OnDestroy()
        {
            if (Bridge.instance != null)
            {
                Bridge.platform.audioStateChanged -= OnPlatformAudioStateChanged;
                Bridge.platform.pauseStateChanged -= OnPlatformPauseStateChanged;
            }
        }

        private void OnPlatformAudioStateChanged(bool isEnabled)
        {
            _isAudioEnabled.text = $"Is Audio Enabled: { isEnabled }";
            Debug.Log($"OnAudioStateChanged, isEnabled: {isEnabled}");
        }
        
        private void OnPlatformPauseStateChanged(bool isPaused)
        {
            Debug.Log($"OnPauseStateChanged, isPaused: {isPaused}");
        }

        private void OnSendGameReadyMessageButtonClicked()
        {
            Bridge.platform.SendMessage(PlatformMessage.GameReady);
        }

        private void OnSendInGameLoadingStartedMessageButtonClicked()
        {
            Bridge.platform.SendMessage(PlatformMessage.InGameLoadingStarted);
        }
        
        private void OnSendInGameLoadingStoppedMessageButtonClicked()
        {
            Bridge.platform.SendMessage(PlatformMessage.InGameLoadingStopped);
        }
        
        private void OnSendGameplayStartedMessageButtonClicked()
        {
            Bridge.platform.SendMessage(PlatformMessage.GameplayStarted);
        }
        
        private void OnSendGameplayStoppedMessageButtonClicked()
        {
            Bridge.platform.SendMessage(PlatformMessage.GameplayStopped);
        }
        
        private void OnSendPlayerGotAchievementMessageButtonClicked()
        {
            Bridge.platform.SendMessage(PlatformMessage.PlayerGotAchievement);
        }

        private void OnGetServerTimeButtonClicked()
        {
            _overlay.SetActive(true);
            
            Bridge.platform.GetServerTime(date =>
            {
                _serverTimeText.text = date.HasValue
                    ? $"Server Time (UTC): {date.Value}"
                    : "Server Time (UTC): -";
                
                _overlay.SetActive(false);
            });
        }

        private void OnGetAllGamesButtonClicked()
        {
            _overlay.SetActive(true);
            
            Bridge.platform.GetAllGames((success, games) => {
                Debug.Log($"OnGetAllGamesCompleted, success: {success}, games:");

                if (success) {
                    foreach (var game in games) {
                        Debug.Log($"App ID: {game["appID"]}");
                        Debug.Log($"Title: {game["title"]}");
                        Debug.Log($"URL: {game["url"]}");
                        Debug.Log($"Cover URL: {game["coverURL"]}");
                        Debug.Log($"Icon URL: {game["iconURL"]}");
                    }
                }

                _overlay.SetActive(false);
            });
        }

        private void OnGetGameByIdButtonClicked()
        {
            _overlay.SetActive(true);

            Bridge.platform.GetGameById(new Dictionary<string, object>
            {
                { "gameId", _gameIdInputField.text }
            }, (success, game) =>
            {
                Debug.Log($"OnGetGameByIdCompleted, success: {success}, game:");

                if (success)
                {
                    Debug.Log($"App ID: {game["appID"]}");
                    Debug.Log($"Title: {game["title"]}");
                    Debug.Log($"URL: {game["url"]}");
                    Debug.Log($"Cover URL: {game["coverURL"]}");
                    Debug.Log($"Icon URL: {game["iconURL"]}");
                }

                _overlay.SetActive(false);
            });
        }
#endif
    }
}