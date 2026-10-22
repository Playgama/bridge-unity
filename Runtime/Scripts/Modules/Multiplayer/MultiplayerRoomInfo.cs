#if UNITY_WEBGL
using System;

namespace Playgama.Modules.Multiplayer
{
    // Mirrors the core SDK `MultiplayerRoomInfo` shape returned by getRooms().
    [Serializable]
    public class MultiplayerRoomInfo
    {
        public string id;
        public string type;
        public int maxPlayers;
        public int playerCount;
        public string properties; // JSON object
    }
}
#endif
