#if UNITY_WEBGL
using System;

namespace Playgama.Modules.Notifications
{
    // Mirrors the core SDK `ScheduledNotification` shape passed to schedule().
    // `id` is the game-level id mapped to the platform value in the config file.
    [Serializable]
    public class ScheduledNotification
    {
        public string id;
        public string title;
        public string description;
        public int delaySeconds = -1;
        public string image;
        public string callToAction;
        public string payload;
    }
}
#endif
