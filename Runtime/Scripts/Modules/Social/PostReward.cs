#if UNITY_WEBGL
using System;

namespace Playgama.Modules.Social
{
    // Mirrors the core SDK `PostReward` shape returned by getPostReward(): a reward
    // declared in the config entry of a post, verified by the platform backend.
    // Plain [Serializable] fields so Unity's JsonUtility can parse the list.
    [Serializable]
    public class PostReward
    {
        public string id;
        public int amount;
        public string type; // "visit" | "author"
    }
}
#endif
