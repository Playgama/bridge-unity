#if UNITY_WEBGL
namespace Playgama.Modules.Multiplayer
{
    // Message recipients; a player id or a list of ids can be passed to Send() instead.
    public enum MultiplayerTarget
    {
        All,
        Others,
        Host
    }
}
#endif
