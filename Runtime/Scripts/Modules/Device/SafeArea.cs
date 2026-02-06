#if UNITY_WEBGL
using System;

namespace Playgama.Modules.Device
{
    [Serializable]
    public class SafeArea
    {
        public int top;
        public int bottom;
        public int left;
        public int right;
    }
}
#endif
