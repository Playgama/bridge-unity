#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using Playgama.Common;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Multiplayer
{
    public class MultiplayerModule : MonoBehaviour
    {
        public event Action<MultiplayerState> stateChanged;
        public event Action<RoomLeaveReason> roomLeft;
        public event Action<MultiplayerRoom> roomUpdated;
        public event Action<MultiplayerPlayer> playerJoined;
        public event Action<MultiplayerPlayer> playerLeft;
        public event Action<MultiplayerPlayer> playerUpdated;
        // The new host, null when the room has none.
        public event Action<MultiplayerPlayer> hostChanged;
        public event Action<MultiplayerMessage> messageReceived;
        // The host's world with the syncState room option, in the format of message data: for
        // each snapshot on the other players, and on a new host when it takes over the last one.
        public event Action<Dictionary<string, object>> syncStateReceived;

        public bool isSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsMultiplayerSupported() == "true";
#else
                return false;
#endif
            }
        }

        public MultiplayerState state
        {
            get
            {
#if !UNITY_EDITOR
                return ParseState(snapshot.state);
#else
                return MultiplayerState.Disconnected;
#endif
            }
        }

        // Connected or reconnecting.
        public bool isInRoom
        {
            get
            {
#if !UNITY_EDITOR
                return snapshot.isInRoom;
#else
                return false;
#endif
            }
        }

        public bool isHost
        {
            get
            {
#if !UNITY_EDITOR
                return snapshot.isHost;
#else
                return false;
#endif
            }
        }

        // Local player id in the room.
        public string playerId
        {
            get
            {
#if !UNITY_EDITOR
                return NullIfEmpty(snapshot.playerId);
#else
                return null;
#endif
            }
        }

        public string hostId
        {
            get
            {
#if !UNITY_EDITOR
                return NullIfEmpty(snapshot.hostId);
#else
                return null;
#endif
            }
        }

        // Room code other players type to join.
        public string roomId
        {
            get
            {
#if !UNITY_EDITOR
                return NullIfEmpty(snapshot.roomId);
#else
                return null;
#endif
            }
        }

        public MultiplayerRoom room
        {
            get
            {
#if !UNITY_EDITOR
                return snapshot.room;
#else
                return null;
#endif
            }
        }

        public List<MultiplayerPlayer> players
        {
            get
            {
#if !UNITY_EDITOR
                return new List<MultiplayerPlayer>(snapshot.players);
#else
                return new List<MultiplayerPlayer>();
#endif
            }
        }

        // The platform can share a link to the current room, see InviteToRoom().
        public bool isInviteSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsMultiplayerInviteSupported() == "true";
#else
                return false;
#endif
            }
        }

        // Room code from the invite link the game was opened with: pass it to JoinRoom().
        public string launchRoomId
        {
            get
            {
#if !UNITY_EDITOR
                return NullIfEmpty(PlaygamaBridgeMultiplayerLaunchRoomId());
#else
                return null;
#endif
            }
        }

        // A copy of the last world set with SetSyncState() or received; empty after leaving the room.
        public Dictionary<string, object> syncState
        {
            get
            {
#if !UNITY_EDITOR
                return ParseDictionary(PlaygamaBridgeMultiplayerSyncState());
#else
                return new Dictionary<string, object>();
#endif
            }
        }

        // Error code of the last failed request, e.g. MULTIPLAYER_ROOM_FULL; null after a success.
        public string lastError { get; private set; }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsMultiplayerSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeMultiplayerSnapshot();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeMultiplayerTakeMessages();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsMultiplayerInviteSupported();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeMultiplayerLaunchRoomId();

        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeMultiplayerSyncState();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerCreateRoom(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerJoinRoom(string roomId, string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerJoinOrCreateRoom(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerGetRooms(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerInviteToRoom(string options);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerLeaveRoom();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerSetRoomOpen(string isOpen);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerSetRoomVisible(string isVisible);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerSetRoomProperties(string properties);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerSetPlayerProperties(string properties);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerSend(string type, string data, string to);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgeMultiplayerSetSyncState(string state);
#endif

        private Action<bool, string> _createRoomCallback;
        private Action<bool> _joinRoomCallback;
        private Action<bool, string> _joinOrCreateRoomCallback;
        private Action<bool, List<MultiplayerRoomInfo>> _getRoomsCallback;
        private Action<bool> _inviteToRoomCallback;

#if !UNITY_EDITOR
        private MultiplayerSnapshot _snapshot;

        // Read from JS once and again only after a multiplayer event changed the room state.
        private MultiplayerSnapshot snapshot
        {
            get
            {
                if (_snapshot == null)
                {
                    _snapshot = ParseObject<MultiplayerSnapshot>(PlaygamaBridgeMultiplayerSnapshot()) ?? new MultiplayerSnapshot();

                    if (_snapshot.players == null)
                    {
                        _snapshot.players = new List<MultiplayerPlayer>();
                    }

                    if (string.IsNullOrEmpty(_snapshot.roomId))
                    {
                        _snapshot.room = null;
                    }
                }

                return _snapshot;
            }
        }

        // Messages are taken from JS once per frame in one batch.
        private void Update()
        {
            if (isInRoom)
            {
                OnMultiplayerMessagesReceived(PlaygamaBridgeMultiplayerTakeMessages());
            }
        }
#endif

        // Options: maxPlayers, isVisible (false by default: joined by code only), isOpen,
        // filter (lets quick match and filtered room lists find a visible room), properties,
        // playerName, playerProperties, syncState (true, or a dictionary with updatesPerSecond
        // from 1 to 10, 10 by default: see SetSyncState()). The callback receives the room id.
        public void CreateRoom(Dictionary<string, object> options = null, Action<bool, string> onComplete = null)
        {
            _createRoomCallback = onComplete;
            lastError = null;

#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerCreateRoom(options.ToJson());
#else
            OnMultiplayerCreateRoomCompletedFailed();
#endif
        }

        // Options: playerName, playerProperties, syncState (see CreateRoom()).
        public void JoinRoom(string roomId, Dictionary<string, object> options = null, Action<bool> onComplete = null)
        {
            _joinRoomCallback = onComplete;
            lastError = null;

#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerJoinRoom(roomId ?? string.Empty, options.ToJson());
#else
            OnMultiplayerJoinRoomCompletedFailed();
#endif
        }

        // Quick match: enters a visible room with the same maxPlayers and filter or creates one.
        // Options: maxPlayers, filter, properties, playerName, playerProperties,
        // syncState (see CreateRoom()). The callback receives the room id.
        public void JoinOrCreateRoom(Dictionary<string, object> options = null, Action<bool, string> onComplete = null)
        {
            _joinOrCreateRoomCallback = onComplete;
            lastError = null;

#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerJoinOrCreateRoom(options.ToJson());
#else
            OnMultiplayerJoinOrCreateRoomCompletedFailed();
#endif
        }

        // Visible open rooms with a free seat. Options: filter (only rooms created with exactly
        // this filter), maxPlayers, limit.
        public void GetRooms(Dictionary<string, object> options = null, Action<bool, List<MultiplayerRoomInfo>> onComplete = null)
        {
            _getRoomsCallback = onComplete;
            lastError = null;

#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerGetRooms(options.ToJson());
#else
            OnMultiplayerGetRoomsCompletedFailed();
#endif
        }

        // Asks the platform to share a link to the current room. Options: text, image,
        // any other key is passed to the platform as is.
        public void InviteToRoom(Dictionary<string, object> options = null, Action<bool> onComplete = null)
        {
            _inviteToRoomCallback = onComplete;
            lastError = null;

#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerInviteToRoom(options.ToJson());
#else
            OnMultiplayerInviteToRoomCompletedFailed();
#endif
        }

        public void LeaveRoom()
        {
#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerLeaveRoom();
#endif
        }

        // Host only: stop (or resume) accepting new players.
        public void SetRoomOpen(bool isOpen)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerSetRoomOpen(isOpen ? "true" : "false");
#endif
        }

        // Host only: show (or hide) the room in the room list and matchmaking.
        public void SetRoomVisible(bool isVisible)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerSetRoomVisible(isVisible ? "true" : "false");
#endif
        }

        // Host only: keys are merged, a null value removes the key.
        public void SetRoomProperties(Dictionary<string, object> properties)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerSetRoomProperties(properties.ToJson());
#endif
        }

        // Updates the local player: keys are merged, a null value removes the key.
        public void SetPlayerProperties(Dictionary<string, object> properties)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerSetPlayerProperties(properties.ToJson());
#endif
        }

        // `data` is a flat dictionary: string, number or bool values; null sends an empty one.
        public void Send(string type, Dictionary<string, object> data = null, MultiplayerTarget to = MultiplayerTarget.Others)
        {
            SendToTarget(type, data, TargetToString(to));
        }

        // Sends to a single player.
        public void Send(string type, Dictionary<string, object> data, string playerId)
        {
            SendToTarget(type, data, playerId);
        }

        // Sends to the listed players.
        public void Send(string type, Dictionary<string, object> data, string[] playerIds)
        {
            SendToTarget(type, data, "[" + string.Join(",", Array.ConvertAll(playerIds, id => id.ToJson())) + "]");
        }

        private void SendToTarget(string type, Dictionary<string, object> data, string to)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerSend(type, (data ?? new Dictionary<string, object>()).ToJson(), to);
#endif
        }

        // World for the syncState room option, flat like Send() data; null sets an empty one.
        // Only the host's world is sent: change it on the host and set it after each change.
        // Works outside a room too, e.g. to set the starting world before CreateRoom().
        public void SetSyncState(Dictionary<string, object> state)
        {
#if !UNITY_EDITOR
            PlaygamaBridgeMultiplayerSetSyncState(state.ToJson());
#endif
        }

        // Called from JS
        private void OnMultiplayerStateChanged(string value)
        {
            ResetSnapshot();
            stateChanged?.Invoke(ParseState(value));
        }

        private void OnMultiplayerRoomLeft(string value)
        {
            ResetSnapshot();
            roomLeft?.Invoke(ParseLeaveReason(value));
        }

        private void OnMultiplayerRoomUpdated(string value)
        {
            ResetSnapshot();
            roomUpdated?.Invoke(ParseObject<MultiplayerRoom>(value));
        }

        private void OnMultiplayerPlayerJoined(string value)
        {
            ResetSnapshot();
            playerJoined?.Invoke(ParseObject<MultiplayerPlayer>(value));
        }

        private void OnMultiplayerPlayerLeft(string value)
        {
            ResetSnapshot();
            playerLeft?.Invoke(ParseObject<MultiplayerPlayer>(value));
        }

        private void OnMultiplayerPlayerUpdated(string value)
        {
            ResetSnapshot();
            playerUpdated?.Invoke(ParseObject<MultiplayerPlayer>(value));
        }

        private void OnMultiplayerHostChanged(string value)
        {
            ResetSnapshot();
            hostChanged?.Invoke(ParseObject<MultiplayerPlayer>(value));
        }

        // A JSON array of messages: taken in Update() or sent from JS before another event.
        private void OnMultiplayerMessagesReceived(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var message in ParseList<MultiplayerMessage>(value))
            {
                messageReceived?.Invoke(message);
            }
        }

        // A JSON array of values, sent from JS right after the queued messages.
        private void OnMultiplayerSyncStateReceived(string value)
        {
            syncStateReceived?.Invoke(ParseDictionary(value));
        }

        private void OnMultiplayerCreateRoomCompletedSuccess(string result)
        {
            _createRoomCallback?.Invoke(true, result);
            _createRoomCallback = null;
        }

        private void OnMultiplayerCreateRoomCompletedFailed(string error = null)
        {
            lastError = NullIfEmpty(error);
            _createRoomCallback?.Invoke(false, null);
            _createRoomCallback = null;
        }

        private void OnMultiplayerJoinRoomCompletedSuccess(string result)
        {
            _joinRoomCallback?.Invoke(true);
            _joinRoomCallback = null;
        }

        private void OnMultiplayerJoinRoomCompletedFailed(string error = null)
        {
            lastError = NullIfEmpty(error);
            _joinRoomCallback?.Invoke(false);
            _joinRoomCallback = null;
        }

        private void OnMultiplayerJoinOrCreateRoomCompletedSuccess(string result)
        {
            _joinOrCreateRoomCallback?.Invoke(true, result);
            _joinOrCreateRoomCallback = null;
        }

        private void OnMultiplayerJoinOrCreateRoomCompletedFailed(string error = null)
        {
            lastError = NullIfEmpty(error);
            _joinOrCreateRoomCallback?.Invoke(false, null);
            _joinOrCreateRoomCallback = null;
        }

        private void OnMultiplayerGetRoomsCompletedSuccess(string result)
        {
            _getRoomsCallback?.Invoke(true, ParseList<MultiplayerRoomInfo>(result));
            _getRoomsCallback = null;
        }

        private void OnMultiplayerGetRoomsCompletedFailed(string error = null)
        {
            lastError = NullIfEmpty(error);
            _getRoomsCallback?.Invoke(false, null);
            _getRoomsCallback = null;
        }

        private void OnMultiplayerInviteToRoomCompletedSuccess(string result)
        {
            _inviteToRoomCallback?.Invoke(true);
            _inviteToRoomCallback = null;
        }

        private void OnMultiplayerInviteToRoomCompletedFailed(string error = null)
        {
            lastError = NullIfEmpty(error);
            _inviteToRoomCallback?.Invoke(false);
            _inviteToRoomCallback = null;
        }

        private void ResetSnapshot()
        {
#if !UNITY_EDITOR
            _snapshot = null;
#endif
        }

        private static MultiplayerState ParseState(string value)
        {
            return Enum.TryParse<MultiplayerState>(value, true, out var state) ? state : MultiplayerState.Disconnected;
        }

        private static RoomLeaveReason ParseLeaveReason(string value)
        {
            switch (value)
            {
                case "connection_lost":
                    return RoomLeaveReason.ConnectionLost;
                case "room_closed":
                    return RoomLeaveReason.RoomClosed;
            }

            return RoomLeaveReason.Left;
        }

        private static string TargetToString(MultiplayerTarget target)
        {
            switch (target)
            {
                case MultiplayerTarget.All:
                    return "all";
                case MultiplayerTarget.Host:
                    return "host";
            }

            return "others";
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private static T ParseObject<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e);
                return null;
            }
        }

        private static List<T> ParseList<T>(string json)
        {
            var result = new List<T>();

            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    // JsonUtility cannot parse a top-level array, so wrap it.
                    var wrapper = JsonUtility.FromJson<ListWrapper<T>>("{\"items\":" + json + "}");
                    if (wrapper?.items != null)
                    {
                        result = wrapper.items;
                    }
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.Log(e);
                }
            }

            return result;
        }

        // A flat object sent as a JSON array of values, see MultiplayerValue.
        private static Dictionary<string, object> ParseDictionary(string json)
        {
            return MultiplayerValue.ToDictionary(ParseList<MultiplayerValue>(json));
        }

        [Serializable]
        private class ListWrapper<T>
        {
            public List<T> items;
        }
    }
}
#endif
