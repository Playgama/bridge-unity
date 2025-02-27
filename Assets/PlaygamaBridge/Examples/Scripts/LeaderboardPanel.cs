using System.Collections.Generic;
using Playgama;
using UnityEngine;
using UnityEngine.UI;

namespace Examples
{
    public class LeaderboardPanel : MonoBehaviour
    {
        [SerializeField] private Text _isSupported;
        [SerializeField] private Text _isNativePopupSupported;
        [SerializeField] private Text _isSetScoreSupported;
        [SerializeField] private Text _isGetScoreSupported;
        [SerializeField] private Text _isGetEntriesSupported;
        [SerializeField] private Button _showNativePopupButton;
        [SerializeField] private Button _setScoreButton;
        [SerializeField] private Button _getScoreButton;
        [SerializeField] private Button _getEntriesButton;
        [SerializeField] private GameObject _overlay;

        private void Start()
        {
            _isSupported.text = $"Is Supported: { Bridge.leaderboard.isSupported }";
            _isNativePopupSupported.text = $"Is Native Popup Supported: { Bridge.leaderboard.isNativePopupSupported }";
            _isSetScoreSupported.text = $"Is Set Score Supported: { Bridge.leaderboard.isSetScoreSupported }";
            _isGetScoreSupported.text = $"Is Get Score Supported: { Bridge.leaderboard.isGetScoreSupported }";
            _isGetEntriesSupported.text = $"Is Get Entries Supported: { Bridge.leaderboard.isGetEntriesSupported }";

            _showNativePopupButton.onClick.AddListener(OnShowNativePopupButtonClicked);
            _setScoreButton.onClick.AddListener(OnSetScoreButtonClicked);
            _getScoreButton.onClick.AddListener(OnGetScoreButtonClicked);
            _getEntriesButton.onClick.AddListener(OnGetEntriesButtonClicked);
        }

        private void OnShowNativePopupButtonClicked()
        {
            _overlay.SetActive(true);

            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "vk":
                    options.Add("userResult", 42);
                    options.Add("global", true);
                    break;
            }

            Bridge.leaderboard.ShowNativePopup(options, _ => { _overlay.SetActive(false); });
        }

        private void OnSetScoreButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "yandex":
                    options.Add("score", 42);
                    options.Add("leaderboardName", "YOUR_LEADERBOARD_NAME");
                    break;
            }

            Bridge.leaderboard.SetScore(options, _ => { _overlay.SetActive(false); });
        }

        private void OnGetScoreButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "yandex":
                    options.Add("leaderboardName", "YOUR_LEADERBOARD_NAME");
                    break;
            }

            Bridge.leaderboard.GetScore(
                options,
                (success, score) =>
                {
                    Debug.Log($"OnGetScoreCompleted, success: {success}, score: {score}");
                    _overlay.SetActive(false);
                });
        }

        private void OnGetEntriesButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "yandex":
                    options.Add("leaderboardName", "YOUR_LEADERBOARD_NAME");
                    options.Add("includeUser", true);
                    options.Add("quantityAround", 10);
                    options.Add("quantityTop", 10);
                    break;
            }

            Bridge.leaderboard.GetEntries(
                options,
                (success, entries) =>
                {
                    Debug.Log($"OnGetEntriesCompleted, success: {success}, entries:");
                   
                    if (success)
                    {
                        switch (Bridge.platform.id)
                        {
                            case "yandex":
                                foreach (var entry in entries)
                                {
                                    Debug.Log("ID: " + entry["id"]);
                                    Debug.Log("Score: " + entry["score"]);
                                    Debug.Log("Rank: " + entry["rank"]);
                                    Debug.Log("Name: " + entry["name"]);
                                    Debug.Log("Photo: " + entry["photo"]);
                                }
                                break;
                        }
                    }

                    _overlay.SetActive(false);
                });
        }
    }
}