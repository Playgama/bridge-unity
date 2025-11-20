#if UNITY_WEBGL
using UnityEngine;

namespace Playgama.Common
{
    public class Singleton<T> : MonoBehaviour where T : Component
    {
        public static T instance
        {
            get
            {
                if (_isApplicationQuitting)
                {
                    return null;
                }

                if (_instance != null)
                {
                    return _instance;
                }

                _instance = FindFirstObjectByType<T>();
                if (_instance != null)
                {
                    return _instance;
                }

                var obj = new GameObject { name = $"{typeof(T).Name}" };
                _instance = obj.AddComponent<T>();
                return _instance;
            }
        }
        
        protected static T _instance;
        protected static bool _isApplicationQuitting;

        
        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual void OnApplicationQuit()
        {
            _isApplicationQuitting = true;
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod]
        private static void ResetOnLoad()
        {
            _instance = null;
            _isApplicationQuitting = false;
        }
#endif
    }
}
#endif