#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Playgama.Modules.Multiplayer
{
    // Mirrors the core SDK `MultiplayerRoom` shape.
    [Serializable]
    public class MultiplayerRoom : ISerializationCallbackReceiver
    {
        public string id; // room code players type to join
        public string type; // "relay"
        public int maxPlayers;
        public bool isOpen;
        public bool isVisible;
        public string hostId;

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
