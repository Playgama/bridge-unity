#if UNITY_WEBGL
using System;

namespace Playgama.Modules.Multiplayer
{
    // Mirrors the core SDK `MultiplayerRoom` shape. Plain [Serializable] fields so
    // Unity's JsonUtility can parse it; free-form properties come as a JSON string.
    [Serializable]
    public class MultiplayerRoom
    {
        public string id; // room code players type to join
        public string type; // "relay"
        public int maxPlayers;
        public bool isOpen;
        public bool isVisible;
        public string hostId;
        public string properties; // JSON object
    }
}
#endif
