#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Storage
{
    public class StorageModule : MonoBehaviour
    {
#if !UNITY_EDITOR
        public StorageType defaultType 
        { 
            get
            {
                var type = PlaygamaBridgeGetStorageDefaultType();
                return ParseStorageType(type);
            }
        }

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsStorageSupported(string storageType);

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsStorageAvailable(string storageType);

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetStorageDefaultType();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeGetStorageData(string key, string storageType);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeSetStorageData(string key, string value, string storageType);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeDeleteStorageData(string key, string storageType);
#else
        public StorageType defaultType => StorageType.LocalStorage;
        private const string _storageDataEditorPlayerPrefsPrefix = "bridge_storage_data";
#endif
        private const string _dataSeparator = "{bridge_data_separator}";
        private const string _keysSeparator = "{bridge_keys_separator}";
        private const string _valuesSeparator = "{bridge_values_separator}";

        private readonly Dictionary<string, List<Action<bool, string>>> _getDataCallbacks = new();
        private readonly Dictionary<string, List<Action<bool, List<string>>>> _getMultipleDataCallbacks = new();
        private readonly Dictionary<string, List<Action<bool>>> _setDataCallbacks = new();
        private readonly Dictionary<string, List<Action<bool>>> _deleteDataCallbacks = new();


        public bool IsSupported(StorageType storageType)
        {
#if !UNITY_EDITOR
            return PlaygamaBridgeIsStorageSupported(ConvertStorageType(storageType)) == "true";
#else
            return storageType == StorageType.LocalStorage;
#endif
        }
        
        public bool IsAvailable(StorageType storageType)
        {
#if !UNITY_EDITOR
            return PlaygamaBridgeIsStorageAvailable(ConvertStorageType(storageType)) == "true";
#else
            return storageType == StorageType.LocalStorage;
#endif
        }

        public void Get(string key, Action<bool, string> onComplete, StorageType? storageType = null)
        {
            if (_getDataCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks.Add(onComplete);
                _getDataCallbacks[key] = callbacks;
            }
            else
            {
                _getDataCallbacks.Add(key, new List<Action<bool, string>> { onComplete });
#if !UNITY_EDITOR
                PlaygamaBridgeGetStorageData(key, ConvertStorageType(storageType));
#else
                var data = PlayerPrefs.GetString($"{_storageDataEditorPlayerPrefsPrefix}_{key}", null);
                OnGetStorageDataSuccess($"{key}{_dataSeparator}{data}");
#endif
            }
        }

        public void Get(List<string> keys, Action<bool, List<string>> onComplete, StorageType? storageType = null)
        {
            var keysCount = keys.Count;
            if (keysCount <= 0)
            {
                onComplete?.Invoke(false, null);
                return;
            }

            if (keysCount == 1)
            {
                Get(keys[0], (success, data) => { onComplete?.Invoke(success, success ? new List<string> { data } : null); }, storageType);
                return;
            }
            
            var key = string.Join(_keysSeparator, keys);
            if (_getMultipleDataCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks.Add(onComplete);
                _getMultipleDataCallbacks[key] = callbacks;
            }
            else
            {
                _getMultipleDataCallbacks.Add(key, new List<Action<bool, List<string>>> { onComplete });
#if !UNITY_EDITOR
                PlaygamaBridgeGetStorageData(key, ConvertStorageType(storageType));
#else
                var values = new List<string>();
                foreach (var k in keys)
                {
                    var v = PlayerPrefs.GetString($"{_storageDataEditorPlayerPrefsPrefix}_{k}", null);
                    if (string.IsNullOrEmpty(v))
                    {
                        v = null;
                    }

                    values.Add(v);
                }

                var value = string.Join(_valuesSeparator, values);
                OnGetStorageDataSuccess($"{key}{_dataSeparator}{value}");
#endif
            }
        }


        public void Set(string key, string value, Action<bool> onComplete = null, StorageType? storageType = null)
        {
            if (_setDataCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks.Add(onComplete);
                _setDataCallbacks[key] = callbacks;
            }
            else
            {
                _setDataCallbacks.Add(key, new List<Action<bool>> { onComplete });
#if !UNITY_EDITOR
                PlaygamaBridgeSetStorageData(key, value, ConvertStorageType(storageType));
#else
                PlayerPrefs.SetString($"{_storageDataEditorPlayerPrefsPrefix}_{key}", value);
                OnSetStorageDataSuccess(key);
#endif
            }
        }

        public void Set(string key, int value, Action<bool> onComplete = null, StorageType? storageType = null)
        {
            Set(key, value.ToString(), onComplete, storageType);
        }

        public void Set(string key, bool value, Action<bool> onComplete = null, StorageType? storageType = null)
        {
            Set(key, value.ToString(), onComplete, storageType);
        }

        public void Set(List<string> keys, List<object> values, Action<bool> onComplete = null, StorageType? storageType = null)
        {
            var key = string.Join(_keysSeparator, keys);
            if (_setDataCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks.Add(onComplete);
                _setDataCallbacks[key] = callbacks;
            }
            else
            {
                _setDataCallbacks.Add(key, new List<Action<bool>> { onComplete });
#if !UNITY_EDITOR
                var value = string.Join(_valuesSeparator, values);
                PlaygamaBridgeSetStorageData(key, value, ConvertStorageType(storageType));
#else
                for (var i = 0; i < keys.Count; i++)
                {
                    PlayerPrefs.SetString($"{_storageDataEditorPlayerPrefsPrefix}_{keys[i]}", values[i].ToString());
                }

                OnSetStorageDataSuccess($"{key}");
#endif
            }
        }


        public void Delete(string key, Action<bool> onComplete = null, StorageType? storageType = null)
        {
            if (_deleteDataCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks.Add(onComplete);
                _deleteDataCallbacks[key] = callbacks;
            }
            else
            {
                _deleteDataCallbacks.Add(key, new List<Action<bool>> { onComplete });
#if !UNITY_EDITOR
                PlaygamaBridgeDeleteStorageData(key, ConvertStorageType(storageType));
#else
                PlayerPrefs.DeleteKey($"{_storageDataEditorPlayerPrefsPrefix}_{key}");
                OnDeleteStorageDataSuccess(key);
#endif
            }
        }

        public void Delete(List<string> keys, Action<bool> onComplete = null, StorageType? storageType = null)
        {
            var key = string.Join(_keysSeparator, keys);
            if (_deleteDataCallbacks.TryGetValue(key, out var callbacks))
            {
                callbacks.Add(onComplete);
                _deleteDataCallbacks[key] = callbacks;
            }
            else
            {
                _deleteDataCallbacks.Add(key, new List<Action<bool>> { onComplete });
#if !UNITY_EDITOR
                PlaygamaBridgeDeleteStorageData(key, ConvertStorageType(storageType));
#else
                foreach (var k in keys)
                {
                    PlayerPrefs.DeleteKey($"{_storageDataEditorPlayerPrefsPrefix}_{k}");
                }

                OnDeleteStorageDataSuccess($"{key}");
#endif
            }
        }


        // Called from JS
        private void OnGetStorageDataSuccess(string result)
        {
            var keyEndIndex = result.IndexOf(_dataSeparator);
            if (keyEndIndex <= 0)
            {
                return;
            }

            var keysString = result.Substring(0, keyEndIndex);
            var valuesString = result.Substring(keyEndIndex + _dataSeparator.Length, result.Length - keyEndIndex - _dataSeparator.Length);
            var keys = keysString.Split(_keysSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (keys.Length > 1)
            {
                var values = valuesString.Split(_valuesSeparator).ToList();

                for (var i = 0; i < values.Count; i++)
                {
                    var value = values[i];
                    if (string.IsNullOrEmpty(value))
                    {
                        values[i] = null;
                    }
                }

                if (_getMultipleDataCallbacks.TryGetValue(keysString, out var callbacks))
                {
                    _getMultipleDataCallbacks.Remove(keysString);

                    foreach (var callback in callbacks)
                    {
                        callback?.Invoke(true, values);
                    }
                }
            }
            else
            {
                if (_getDataCallbacks.TryGetValue(keysString, out var callbacks))
                {
                    _getDataCallbacks.Remove(keysString);

                    foreach (var callback in callbacks)
                    {
                        callback?.Invoke(true, string.IsNullOrEmpty(valuesString) ? null : valuesString);
                    }
                }
            }
        }

        private void OnGetStorageDataFailed(string keysString)
        {
            var keys = keysString.Split(_keysSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (keys.Length > 1)
            {
                if (_getMultipleDataCallbacks.TryGetValue(keysString, out var callbacks))
                {
                    _getMultipleDataCallbacks.Remove(keysString);

                    foreach (var callback in callbacks)
                    {
                        callback?.Invoke(false, null);
                    }
                }
            }
            else
            {
                if (_getDataCallbacks.TryGetValue(keysString, out var callbacks))
                {
                    _getDataCallbacks.Remove(keysString);

                    foreach (var callback in callbacks)
                    {
                        callback?.Invoke(false, null);
                    }
                }
            }
        }

        private void OnSetStorageDataSuccess(string key)
        {
            if (_setDataCallbacks.TryGetValue(key, out var callbacks))
            {
                _setDataCallbacks.Remove(key);

                foreach (var callback in callbacks)
                {
                    callback?.Invoke(true);
                }
            }
        }

        private void OnSetStorageDataFailed(string key)
        {
            if (_setDataCallbacks.TryGetValue(key, out var callbacks))
            {
                _setDataCallbacks.Remove(key);

                foreach (var callback in callbacks)
                {
                    callback?.Invoke(false);
                }
            }
        }

        private void OnDeleteStorageDataSuccess(string key)
        {
            if (_deleteDataCallbacks.TryGetValue(key, out var callbacks))
            {
                _deleteDataCallbacks.Remove(key);

                foreach (var callback in callbacks)
                {
                    callback?.Invoke(true);
                }
            }
        }

        private void OnDeleteStorageDataFailed(string key)
        {
            if (_deleteDataCallbacks.TryGetValue(key, out var callbacks))
            {
                _deleteDataCallbacks.Remove(key);

                foreach (var callback in callbacks)
                {
                    callback?.Invoke(false);
                }
            }
        }


        private string ConvertStorageType(StorageType? storageType)
        {
            if (storageType.HasValue)
            {
                switch (storageType.Value)
                {
                    case StorageType.LocalStorage:
                        return "local_storage";

                    case StorageType.PlatformInternal:
                        return "platform_internal";

                    default:
                        throw new ArgumentOutOfRangeException(nameof(storageType), storageType, null);
                }
            }

            return "";
        }

        private StorageType ParseStorageType(string type)
        {
            switch (type)
            {
                case "local_storage":
                    return StorageType.LocalStorage;

                case "platform_internal":
                    return StorageType.PlatformInternal;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
#endif