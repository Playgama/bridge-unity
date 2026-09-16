#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;
using Playgama.Common;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using Playgama.Debug;
#endif

namespace Playgama.Modules.Platform
{
    public class PlatformModule : MonoBehaviour
    {
        public event Action<bool> audioStateChanged;
        public event Action<bool> pauseStateChanged;
        
        public string id
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeGetPlatformId();
#else
                return "mock";
#endif
            }
        }

        public string language
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeGetPlatformLanguage();
#else
                return "en";
#endif
            }
        }

        public string payload
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeGetPlatformPayload();
#else
                return null;
#endif
            }
        }

        public string tld
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeGetPlatformTld();
#else
                return null;
#endif
            }
        }

        // Where the game was opened from: a notification or one of its own posts.
        // Null when the platform did not say.
        public LaunchSource? launchSource
        {
            get
            {
#if !UNITY_EDITOR
                switch (PlaygamaBridgeGetPlatformLaunchSource())
                {
                    case "notification":
                        return LaunchSource.Notification;
                    case "post":
                        return LaunchSource.Post;
                }

                return null;
#else
                return null;
#endif
            }
        }

        // Everything the launch carries: the parameters the platform passed to the
        // game and, when it was opened from one of the game's own posts, "postId" —
        // the id of that post's config entry.
        public Dictionary<string, string> data
        {
            get
            {
#if !UNITY_EDITOR
                var json = PlaygamaBridgeGetPlatformData();
                if (string.IsNullOrEmpty(json))
                {
                    return new Dictionary<string, string>();
                }

                try
                {
                    return JsonHelper.FromJsonToDictionary(json);
                }
                catch (Exception)
                {
                    return new Dictionary<string, string>();
                }
#else
                return new Dictionary<string, string>();
#endif
            }
        }

        public bool isAudioEnabled
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsPlatformAudioEnabled() == "true";
#else
                return true;
#endif
            }
        }

        public bool isExternalCallsSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsPlatformExternalCallsSupported() == "true";
#else
                return true;
#endif
            }
        }

        public bool isExternalLinksAllowed
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsPlatformExternalLinksAllowed() == "true";
#else
                return true;
#endif
            }
        }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformId();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformLanguage();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformPayload();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformTld();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformLaunchSource();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformData();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsPlatformAudioEnabled();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsPlatformExternalCallsSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsPlatformExternalLinksAllowed();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeSendMessageToPlatform(string message, string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeSendCustomMessageToPlatform(string id, string options);

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetServerTime();
#endif
        private Action<DateTime?> _getServerTimeCallback;

        public void SendMessage(PlatformMessage message, Dictionary<string, object> options = null)
        {
#if !UNITY_EDITOR
            var messageString = "";

            switch (message)
            {
                case PlatformMessage.GameReady:
                    messageString = "game_ready";
                    break;

                case PlatformMessage.InGameLoadingStarted:
                    messageString = "in_game_loading_started";
                    break;

                case PlatformMessage.InGameLoadingStopped:
                    messageString = "in_game_loading_stopped";
                    break;

#pragma warning disable 0612, 0618
                case PlatformMessage.GameplayStarted:
                    messageString = "gameplay_started";
                    break;

                case PlatformMessage.GameplayStopped:
                    messageString = "gameplay_stopped";
                    break;
#pragma warning restore 0612, 0618

                case PlatformMessage.PlayerGotAchievement:
                    messageString = "player_got_achievement";
                    break;

                case PlatformMessage.LevelStarted:
                    messageString = "level_started";
                    break;

                case PlatformMessage.LevelCompleted:
                    messageString = "level_completed";
                    break;

                case PlatformMessage.LevelFailed:
                    messageString = "level_failed";
                    break;

                case PlatformMessage.LevelPaused:
                    messageString = "level_paused";
                    break;

                case PlatformMessage.LevelResumed:
                    messageString = "level_resumed";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(message), message, null);
            }

            SendMessageToPlatform(messageString, options);
#endif
        }

        public new void SendMessage(string message)
        {
            SendMessageToPlatform(message, null);
        }

        public new void SendMessage(string message, object value)
        {
            SendMessageToPlatform(message, value as Dictionary<string, object>);
        }

        public new void SendMessage(string message, SendMessageOptions options)
        {
            SendMessageToPlatform(message, null);
        }

        public new void SendMessage(string message, object value, SendMessageOptions options)
        {
            SendMessageToPlatform(message, value as Dictionary<string, object>);
        }

        private void SendMessageToPlatform(string message, Dictionary<string, object> options)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeSendMessageToPlatform(message, options != null ? options.ToJson() : null);
#endif
        }

        public void SendCustomMessage(string id, Dictionary<string, object> options = null)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeSendCustomMessageToPlatform(id, options != null ? options.ToJson() : null);
#endif
        }

        public void GetServerTime(Action<DateTime?> callback)
        {
            _getServerTimeCallback = callback;
#if !UNITY_EDITOR
            PlaygamaBridgeGetServerTime();
#else
            DebugWindow.ShowSimple(
                "Get Server Time",
                () => OnGetServerTimeCompleted(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()),
                () => OnGetServerTimeCompleted("")
            );
#endif
        }

        // Called from JS
        private void OnAudioStateChanged(string isEnabled)
        {
            audioStateChanged?.Invoke(isEnabled == "true");
        }
        
        private void OnPauseStateChanged(string isPaused)
        {
            pauseStateChanged?.Invoke(isPaused == "true");
        }
        
        private void OnGetServerTimeCompleted(string result)
        {
            DateTime? date = null;

            if (double.TryParse(result, out var ticks))
            {
                var time = TimeSpan.FromMilliseconds(ticks);
                date = new DateTime(1970, 1, 1) + time;
            }
            
            _getServerTimeCallback?.Invoke(date);
            _getServerTimeCallback = null;
        }
    }
}
#endif