#if UNITY_WEBGL
using System;

namespace Playgama.Modules.Multiplayer
{
    // Mirrors the core SDK `MultiplayerMessage` shape. `data` is the sent value as a JSON string.
    [Serializable]
    public class MultiplayerMessage
    {
        public string type;
        public string data;
        public string from; // sender player id
    }
}
#endif
