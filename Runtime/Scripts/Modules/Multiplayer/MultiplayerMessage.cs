#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Playgama.Modules.Multiplayer
{
    // Mirrors the core SDK `MultiplayerMessage` shape.
    [Serializable]
    public class MultiplayerMessage : ISerializationCallbackReceiver
    {
        public string type;
        public string from; // sender player id

        // Flat data the sender passed to Send(): string, int, double or bool values
        public Dictionary<string, object> data = new Dictionary<string, object>();

        // Filled by JsonUtility
#pragma warning disable CS0649
        [SerializeField] private List<MultiplayerValue> dataList;
#pragma warning restore CS0649

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            data = MultiplayerValue.ToDictionary(dataList);
        }
    }
}
#endif
