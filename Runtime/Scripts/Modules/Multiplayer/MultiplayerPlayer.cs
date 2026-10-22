#if UNITY_WEBGL
using System;

namespace Playgama.Modules.Multiplayer
{
    // Mirrors the core SDK `MultiplayerPlayer` shape. Free-form properties come as a JSON string.
    [Serializable]
    public class MultiplayerPlayer
    {
        public string id;
        public string name;
        public bool isHost;
        public bool isConnected; // false while the player is reconnecting
        public string properties; // JSON object
    }
}
#endif
