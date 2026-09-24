#if UNITY_WEBGL
using System.Collections.Generic;
using UnityEngine;
using Playgama.Common;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Analytics
{
    public class AnalyticsModule : MonoBehaviour
    {
#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeAnalyticsSend(string eventName, string data);
#endif

        // Sends a game event. The name and the payload are entirely up to the
        // game — they are never matched against the SDK's own event names.
        public void Send(string eventName, Dictionary<string, object> data = null)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeAnalyticsSend(eventName, data != null ? data.ToJson() : null);
#endif
        }
    }
}
#endif
