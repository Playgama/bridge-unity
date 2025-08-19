using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL
using Playgama;
#endif

namespace Examples
{
    public class LeaderboardsPanel : MonoBehaviour
    {
        [SerializeField] private Text _typeText;
        [SerializeField] private InputField _leaderboardIdInput;
        [SerializeField] private InputField _leaderboardScoreInput;
        [SerializeField] private Button _setScoreButton;
        [SerializeField] private Button _getEntriesButton;
        [SerializeField] private GameObject _overlay;
        [SerializeField] private Button _showNativePopupButton;

#if UNITY_WEBGL
        private void Start()
        {
            _typeText.text = $"Type: { Bridge.leaderboards.type }";

            _setScoreButton.onClick.AddListener(OnSetScoreButtonClicked);
            _getEntriesButton.onClick.AddListener(OnGetEntriesButtonClicked);
            _showNativePopupButton.onClick.AddListener(OnShowNativePopupButtonClicked);
        }

        private void OnSetScoreButtonClicked()
        {
            _overlay.SetActive(true);
            Bridge.leaderboards.SetScore(_leaderboardIdInput.text, _leaderboardScoreInput.text, _ => { _overlay.SetActive(false); });
        }

        private void OnGetEntriesButtonClicked()
        {
            _overlay.SetActive(true);
            
            Bridge.leaderboards.GetEntries(
                _leaderboardIdInput.text,
                (success, entries) =>
                {
                    Debug.Log($"OnGetEntriesCompleted, success: {success}, entries:");
                   
                    if (success)
                    {
                        foreach (var entry in entries)
                        {
                            Debug.Log("ID: " + entry["id"]);
                            Debug.Log("Score: " + entry["score"]);
                            Debug.Log("Rank: " + entry["rank"]);
                            Debug.Log("Name: " + entry["name"]);
                            Debug.Log("Photo: " + entry["photo"]);
                        }
                    }

                    _overlay.SetActive(false);
                });
        }
        
        private void OnShowNativePopupButtonClicked()
        {
            _overlay.SetActive(true);
            
            Bridge.leaderboards.ShowNativePopup(_leaderboardIdInput.text, (success) =>
            {
                Debug.Log($"OnShowNativePopupCompleted, success: {success}");
                _overlay.SetActive(false);
            });
        }
#endif
    }
}