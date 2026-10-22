#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Playgama.Modules.Multiplayer
{
    // Mirrors the core SDK `MultiplayerRoomInfo` shape returned by getRooms().
    [Serializable]
    public class MultiplayerRoomInfo : ISerializationCallbackReceiver
    {
        public string id;
        public string type;
        public int maxPlayers;
        public int playerCount;

        // Flat custom data: string, int, double or bool values
        public Dictionary<string, object> properties = new Dictionary<string, object>();

        // Filled by JsonUtility
#pragma warning disable CS0649
        [SerializeField] private List<MultiplayerValue> propertyList;
#pragma warning restore CS0649

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            properties = MultiplayerValue.ToDictionary(propertyList);
        }
    }
}
#endif
