#if UNITY_WEBGL
using UnityEngine;
using System;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Game
{
    public class GameModule : MonoBehaviour
    {
        public event Action<VisibilityState> visibilityStateChanged;

#if !UNITY_EDITOR
        public VisibilityState visibilityState 
        { 
            get
            {
                var state = PlaygamaBridgeGetVisibilityState();

                if (Enum.TryParse<VisibilityState>(state, true, out var value)) {
                    return value;
                }

                return VisibilityState.Visible;
            }
        }

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetVisibilityState();
#else
        public VisibilityState visibilityState => VisibilityState.Visible;
#endif

        // Called from JS
        private void OnVisibilityStateChanged(string value)
        {
            if (Enum.TryParse<VisibilityState>(value, true, out var state))
            {
                visibilityStateChanged?.Invoke(visibilityState);
            }
        }
    }
}
#endif