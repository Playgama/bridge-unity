#if UNITY_WEBGL
namespace Playgama.Modules.Multiplayer
{
    public enum MultiplayerState
    {
        Disconnected,
        Connecting,
        Connected,
        // The connection dropped, the player keeps the seat while the SDK reconnects.
        Reconnecting
    }
}
#endif
