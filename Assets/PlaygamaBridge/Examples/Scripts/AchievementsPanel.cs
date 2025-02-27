using System.Collections.Generic;
using Playgama;
using UnityEngine;
using UnityEngine.UI;

namespace Examples
{
    public class AchievementsPanel : MonoBehaviour
    {
        [SerializeField] private Text _isSupported;
        [SerializeField] private Text _isGetListSupported;
        [SerializeField] private Text _isNativePopupSupported;
        [SerializeField] private InputField _idInput;
        [SerializeField] private InputField _nameInput;
        [SerializeField] private Button _unlockButton;
        [SerializeField] private Button _getListButton;
        [SerializeField] private Button _showNativePopupButton;
        [SerializeField] private GameObject _overlay;

        private void Start()
        {
            _isSupported.text = $"Is Supported: { Bridge.achievements.isSupported }";
            _isGetListSupported.text = $"Is Get List Supported: { Bridge.achievements.isGetListSupported }";
            _isNativePopupSupported.text = $"Is Native Popup Supported: { Bridge.achievements.isNativePopupSupported }";
            
            _getListButton.onClick.AddListener(OnGetListButtonClicked);
            _unlockButton.onClick.AddListener(OnUnlockButtonClicked);
            _showNativePopupButton.onClick.AddListener(OnShowNativePopupButtonClicked);
        }

        private void OnUnlockButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "y8":
                    options.Add("achievement", "YOUR_ACHIEVEMENT_NAME");
                    options.Add("achievementkey", "YOUR_ACHIEVEMENT_KEY");
                    break;
                case "lagged":
                    options.Add("achievement", "YOUR_ACHIEVEMENT_ID");
                    break;
            }
            
            Bridge.achievements.Unlock(options, (success) =>
            {
                Debug.Log($"OnUnlockCompleted, success: {success}");
                _overlay.SetActive(false);
            });
        }
        
        private void OnShowNativePopupButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            
            Bridge.achievements.ShowNativePopup(options, (success) =>
            {
                Debug.Log($"OnShowNativePopupCompleted, success: {success}");
                _overlay.SetActive(false);
            });
        }
        private void OnGetListButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
        
            Bridge.achievements.GetList(options, (success, list) =>
            {
                Debug.Log($"OnGetListCompleted, success: {success}, items:");
                
                if (success)
                {
                    switch (Bridge.platform.id)
                    {
                        case "y8":
                            foreach (var item in list)
                            {
                                Debug.Log("achievementid:" + item["achievementid"]);
                                Debug.Log("achievement:" + item["achievement"]);
                                Debug.Log("achievementkey:" + item["achievementkey"]);
                                Debug.Log("description:" + item["description"]);
                                Debug.Log("icon:" + item["icon"]);
                                Debug.Log("difficulty:" + item["difficulty"]);
                                Debug.Log("secret:" + item["secret"]);
                                Debug.Log("awarded:" + item["awarded"]);
                                Debug.Log("game:" + item["game"]);
                                Debug.Log("link:" + item["link"]);
                                Debug.Log("playerid:" + item["playerid"]);
                                Debug.Log("playername:" + item["playername"]);
                                Debug.Log("lastupdated:" + item["lastupdated"]);
                                Debug.Log("date:" + item["date"]);
                                Debug.Log("rdate:" + item["rdate"]);
                            }
                            break;
                    }
                }
                
                _overlay.SetActive(false);
            });
        }
    }
}