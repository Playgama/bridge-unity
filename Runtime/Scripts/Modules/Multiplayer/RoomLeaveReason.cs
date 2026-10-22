#if UNITY_WEBGL
namespace Playgama.Modules.Multiplayer
{
    public enum RoomLeaveReason
    {
        // LeaveRoom() was called.
        Left,
        // Reconnection failed.
        ConnectionLost,
        // The server closed the room.
        RoomClosed
    }
}
#endif
