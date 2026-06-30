#if UNITY_WEBGL
using System;
using System.Collections.Generic;

namespace Playgama.Modules.Tasks
{
    // Mirrors the core SDK `Task` shape returned by getTasks()/addProgress().
    // Plain [Serializable] fields so Unity's JsonUtility can parse the nested
    // structure (targets/rewards arrays) the hand-rolled JsonHelper cannot.
    [Serializable]
    public class Task
    {
        public string id;
        public string type; // "daily" | "weekly" | "permanent"
        public List<TaskTarget> targets;
        public List<TaskReward> rewards;
        public bool completed;
        public bool claimed;
    }

    [Serializable]
    public class TaskTarget
    {
        public string id;
        public int amount;
        public int progress;
        public bool completed;
    }

    [Serializable]
    public class TaskReward
    {
        public string id;
        public int amount;
    }
}
#endif
