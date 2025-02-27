using System.Collections.Generic;
using Playgama;
using Playgama.Modules.Storage;
using UnityEngine;
using UnityEngine.UI;

namespace Examples
{
    public class StoragePanel : MonoBehaviour
    {
        [SerializeField] private Text _defaultTypeText;
        [SerializeField] private Text _isLocalStorageSupportedText;
        [SerializeField] private Text _isLocalStorageAvailableText;
        [SerializeField] private Text _isPlatformInternalSupportedText;
        [SerializeField] private Text _isPlatformInternalAvailableText;
        [SerializeField] private InputField _coinsInput;
        [SerializeField] private InputField _levelInput;
        [SerializeField] private Button _setStorageDataButton;
        [SerializeField] private Button _getStorageDataButton;
        [SerializeField] private Button _deleteStorageDataButton;
        [SerializeField] private GameObject _overlay;

        private const string _coinsKey = "coins";
        private const string _levelKey = "level";

        private void Start()
        {
            _defaultTypeText.text = $"Default Type: { Bridge.storage.defaultType }";
            _isLocalStorageSupportedText.text = $"Is Local Storage Supported: { Bridge.storage.IsSupported(StorageType.LocalStorage) }";
            _isPlatformInternalSupportedText.text = $"Is Platform Internal Supported: { Bridge.storage.IsSupported(StorageType.PlatformInternal) }";
            _isLocalStorageAvailableText.text = $"Is Local Storage Available: { Bridge.storage.IsAvailable(StorageType.LocalStorage) }";
            _isPlatformInternalAvailableText.text = $"Is Platform Internal Available: { Bridge.storage.IsAvailable(StorageType.PlatformInternal) }";

            _setStorageDataButton.onClick.AddListener(OnSetStorageDataButtonClicked);
            _getStorageDataButton.onClick.AddListener(OnGetStorageDataButtonClicked);
            _deleteStorageDataButton.onClick.AddListener(OnDeleteStorageDataButtonClicked);
        }

        private void OnSetStorageDataButtonClicked()
        {
            _overlay.SetActive(true);

            int.TryParse(_coinsInput.text, out var coins);
            var level = _levelInput.text;
            
            Bridge.storage.Set(new List<string> { _coinsKey, _levelKey }, new List<object> { coins, level }, _ => _overlay.SetActive(false));
        }

        private void OnGetStorageDataButtonClicked()
        {
            _overlay.SetActive(true);

            var keys = new List<string> { _coinsKey, _levelKey };
            Bridge.storage.Get(keys, (success, data) =>
            {
                if (data[0] != null)
                {
                    int.TryParse(data[0], out var coins);
                    _coinsInput.text = coins.ToString();
                }
                else
                {
                    _coinsInput.text = "0";
                }

                if (data[1] != null)
                {
                    _levelInput.text = data[1];
                }
                else
                {
                    _levelInput.text = "default_level";
                }

                _overlay.SetActive(false);
            });
        }

        private void OnDeleteStorageDataButtonClicked()
        {
            _overlay.SetActive(true);

            var keys = new List<string> { _coinsKey, _levelKey };
            Bridge.storage.Delete(keys, success =>
            {
                _coinsInput.text = string.Empty;
                _levelInput.text = string.Empty;
                _overlay.SetActive(false);
            });
        }
    }
}