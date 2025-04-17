#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using UnityEngine;
using Playgama.Common;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Platform
{
    public class PlatformModule : MonoBehaviour
    {        
#if !UNITY_EDITOR
        public string id { get; } = PlaygamaBridgeGetPlatformId();
        public string language { get; } = PlaygamaBridgeGetPlatformLanguage();
        public string payload { get; } = PlaygamaBridgeGetPlatformPayload();
        public string tld { get; } = PlaygamaBridgeGetPlatformTld();
        public bool isGetAllGamesSupported { get; } = PlaygamaBridgeIsPlatformGetAllGamesSupported() == "true";
        public bool isGetGameByIdSupported { get; } = PlaygamaBridgeIsPlatformGetGameByIdSupported() == "true";

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformId();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformLanguage();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformPayload();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetPlatformTld();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsPlatformGetAllGamesSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsPlatformGetGameByIdSupported();
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeSendMessageToPlatform(string message);
        
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetServerTime();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetAllGames();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeGetGameById(string options);
#else
        public string id => "mock";
        public string language => "en";
        public string payload => null;
        public string tld => null;
        public bool isGetAllGamesSupported => false;
        public bool isGetGameByIdSupported => false;
#endif
        private Action<DateTime?> _getServerTimeCallback;

        private Action<bool, List<Dictionary<string, string>>> _getAllGamesCallback;
        private Action<bool, Dictionary<string, string>> _getGamesByIdCallback;
        
        public void SendMessage(PlatformMessage message)
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

                case PlatformMessage.GameplayStarted:
                    messageString = "gameplay_started";
                    break;

                case PlatformMessage.GameplayStopped:
                    messageString = "gameplay_stopped";
                    break;

                case PlatformMessage.PlayerGotAchievement:
                    messageString = "player_got_achievement";
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(message), message, null);
            }

            PlaygamaBridgeSendMessageToPlatform(messageString);
#endif
        }

        public void GetServerTime(Action<DateTime?> callback)
        {
            _getServerTimeCallback = callback;
#if !UNITY_EDITOR
            PlaygamaBridgeGetServerTime();
#else
            OnGetServerTimeCompleted(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString());
#endif
        }

        public void GetAllGames(Action<bool, List<Dictionary<string, string>>> onComplete = null)
        {
            _getAllGamesCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeGetAllGames();
#else
            OnGetAllGamesCompletedFailed();
#endif
        }

        public void GetGameById(Dictionary<string, object> options, Action<bool, Dictionary<string, string>> onComplete = null) 
        {
            _getGamesByIdCallback = onComplete;
#if !UNITY_EDITOR
            PlaygamaBridgeGetGameById(options.ToJson());
#else
            OnGetGameByIdCompletedFailed();
#endif
        }


        // Called from JS
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

        private void OnGetAllGamesCompletedSuccess(string result)
        {
            var games = new List<Dictionary<string, string>>();

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    games = JsonHelper.FromJsonToListOfDictionaries(result);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }

            _getAllGamesCallback?.Invoke(true, games);
            _getAllGamesCallback = null;
        }

        private void OnGetAllGamesCompletedFailed()
        {
            _getAllGamesCallback?.Invoke(false, null);
            _getAllGamesCallback = null;
        }

        private void OnGetGameByIdCompletedSuccess(string result)
        {
            var game = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    game = JsonHelper.FromJsonToDictionary(result);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }

            _getGamesByIdCallback?.Invoke(true, game);
            _getGamesByIdCallback = null;
        }

        private void OnGetGameByIdCompletedFailed()
        {
            _getGamesByIdCallback?.Invoke(false, null);
            _getGamesByIdCallback = null;
        }
    }
}
#endif