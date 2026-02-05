#if UNITY_WEBGL
#if !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace Playgama.Modules.Device
{
    public class DeviceModule
    {
#if !UNITY_EDITOR
        public DeviceType type
        {
            get
            {
                var stringType = PlaygamaBridgeGetDeviceType();

                if (Enum.TryParse<DeviceType>(stringType, true, out var value)) {
                    return value;
                }

                return DeviceType.Desktop;
            }
        }

        public SafeArea safeArea
        {
            get
            {
                var json = PlaygamaBridgeGetSafeArea();
                if (string.IsNullOrEmpty(json))
                {
                    return new SafeArea();
                }

                return JsonUtility.FromJson<SafeArea>(json);
            }
        }

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetDeviceType();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetSafeArea();
#else
        public DeviceType type => DeviceType.Desktop;

        public SafeArea safeArea
        {
            get
            {
                var unityArea = Screen.safeArea;
                return new SafeArea
                {
                    left = (int)unityArea.x,
                    bottom = (int)unityArea.y,
                    right = (int)(Screen.width - unityArea.x - unityArea.width),
                    top = (int)(Screen.height - unityArea.y - unityArea.height)
                };
            }
        }
#endif
    }
}
#endif