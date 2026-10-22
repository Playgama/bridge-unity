#if UNITY_WEBGL
using System;
using System.Collections.Generic;

namespace Playgama.Modules.Multiplayer
{
    // Room state read from JS in one call and cached until the next multiplayer event.
    // Fields are filled by JsonUtility.
#pragma warning disable CS0649
    [Serializable]
    internal class MultiplayerSnapshot
    {
        public string state;
        public bool isInRoom;
        public bool isHost;
        public string playerId;
        public string hostId;
        public string roomId;
        public MultiplayerRoom room; // JsonUtility fills it even when null in JSON, see roomId
        public List<MultiplayerPlayer> players;
    }
#pragma warning restore CS0649
}
#endif
