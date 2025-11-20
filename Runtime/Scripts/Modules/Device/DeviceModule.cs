#if UNITY_WEBGL
#if !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
#endif

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

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetDeviceType();
#else
        public DeviceType type => DeviceType.Desktop;
#endif
    }
}
#endif