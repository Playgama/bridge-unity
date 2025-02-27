using System.Collections.Generic;
using Playgama;
using UnityEngine;
using UnityEngine.UI;

namespace Examples
{
    public class RemoteConfigPanel : MonoBehaviour
    {
        [SerializeField] private Text _isSupportedText;
        [SerializeField] private Button _getButton;
        [SerializeField] private GameObject _overlay;

        private void Start()
        {
            _isSupportedText.text = $"Is Supported: { Bridge.remoteConfig.isSupported }";
            _getButton.onClick.AddListener(OnGetButtonClicked);
        }

        private void OnGetButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "yandex":
                    var clientFeatures = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            { "name", "levels" },
                            { "value", "5" },
                        }
                    };
                    
                    options.Add("clientFeatures", clientFeatures);
                    break;
            }
            
            Bridge.remoteConfig.Get(options, OnGetCompleted);
        }

        private void OnGetCompleted(bool success, Dictionary<string, string> values)
        {
            Debug.Log($"OnRemoteConfigGetCompleted, success: {success}, items:");
            
            if (success)
            {
                foreach (var keyValuePair in values)
                {
                    Debug.Log($"key: { keyValuePair.Key }, value: { keyValuePair.Value }");
                }
            }
            
            _overlay.SetActive(false);
        }
    }
}