#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Playgama.Modules.Multiplayer
{
    // One key of a flat object (properties, message data) as JS sends it: JsonUtility cannot
    // parse dictionaries, so the plugin turns the list back into Dictionary<string, object>.
#pragma warning disable CS0649
    [Serializable]
    internal class MultiplayerValue
    {
        public string key;
        public string type; // "string", "number" or "boolean"
        public string stringValue;
        public double numberValue;
        public bool boolValue;
#pragma warning restore CS0649

        // Whole numbers come as int, other numbers as double, nested values as JSON strings.
        public static Dictionary<string, object> ToDictionary(List<MultiplayerValue> values)
        {
            var result = new Dictionary<string, object>();
            if (values == null)
            {
                return result;
            }

            foreach (var value in values)
            {
                switch (value.type)
                {
                    case "number":
                        var number = value.numberValue;
                        var isInt = Math.Abs(number % 1) < double.Epsilon && number >= int.MinValue && number <= int.MaxValue;
                        result[value.key] = isInt ? (object)(int)number : number;
                        break;
                    case "boolean":
                        result[value.key] = value.boolValue;
                        break;
                    default:
                        result[value.key] = value.stringValue;
                        break;
                }
            }

            return result;
        }
    }
}
#endif
