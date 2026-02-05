# Project Playgama Bridge Unity

Playgama Bridge Unity is a cross-platform SDK for publishing HTML5 games to many web platforms (Playgama, Crazy Games, Yandex Games, Poki, Facebook Instant Games, Telegram, VK, Discord, etc.). It's a Unity Package Manager (UPM) package requiring Unity 2022.3+.

## Build & Development

This is a Unity plugin - there are no CLI build commands. Development workflow:
- Open project in Unity Editor
- Access Bridge tools via **Playgama > Bridge** menu
- Install template files via **Playgama > Install Template Files**
- Build for WebGL using Unity's standard build pipeline

Assembly definitions:
- `Playgama.Bridge` - Runtime code (all platforms)
- `Playgama.Bridge.Editor` - Editor tools (Editor platform only)

## Architecture

### Module System

The SDK uses a singleton-based module architecture. Entry point is `Bridge.cs` which exposes 12 modules as static properties:

```csharp
Bridge.advertisement   // Ads: Banner, Interstitial, Rewarded
Bridge.storage         // LocalStorage/SessionStorage persistence
Bridge.player          // Player authorization & info
Bridge.game            // Game lifecycle & visibility
Bridge.platform        // Platform detection & messaging
Bridge.device          // Device info & safe areas
Bridge.leaderboard     // Leaderboard management
Bridge.payments        // Payment processing
Bridge.achievements    // Achievements
Bridge.remoteConfig    // Remote configuration
Bridge.social          // Social features
```

### C#/JavaScript Interop Pattern

The bridge uses a three-layer architecture:

1. **C# Modules** (`Runtime/Scripts/Modules/`) - Public API consumed by game developers
2. **JavaScript Plugin** (`Runtime/Plugins/PlaygamaBridge.jslib`) - `DllImport("__Internal")` exports for browser APIs
3. **JavaScript Library** (`Runtime/WebGLTemplates/Bridge/playgama-bridge.js`) - Platform abstraction layer

### Conditional Compilation Strategy

```csharp
#if UNITY_WEBGL
    #if !UNITY_EDITOR
        // Actual JavaScript interop (WebGL builds only)
    #else
        // Mock implementations for Editor testing
    #endif
#endif
```

This pattern ensures:
- Non-WebGL platforms skip bridge code
- Editor has mock implementations for development without deploying
- WebGL builds use actual JavaScript bridge

### Namespace Structure

```
Playgama
├── Common                    // Singleton<T>, JsonHelper
├── Modules.Advertisement
├── Modules.Device
├── Modules.Game
├── Modules.Leaderboards
├── Modules.Payments
├── Modules.Achievements
├── Modules.Platform
├── Modules.Player
├── Modules.RemoteConfig
├── Modules.Social
├── Modules.Storage
├── Debug                     // Editor-only debug window
└── Editor                    // Editor tools & optimization
```

## Key Files

- [Bridge.cs](Runtime/Scripts/Bridge.cs) - Main entry point, initializes all modules
- [PlaygamaBridge.jslib](Runtime/Plugins/PlaygamaBridge.jslib) - JavaScript interop definitions
- [BridgeWindow.cs](Editor/Bridge/BridgeWindow.cs) - Main editor window with optimization tabs
- [playgama-bridge-config.json](Runtime/WebGLTemplates/Bridge/playgama-bridge-config.json) - Platform configuration schema

## Editor Tools

The **Playgama > Bridge** window provides:
- Build summary analysis (size breakdown)
- Texture optimization (ASTC/ETC2 compression)
- Audio optimization
- Mesh optimization
- Shader analysis
- First-run setup auto-detection via `Library/PlaygamaBridge.version`

## External Resources

- [Documentation](https://wiki.playgama.com/)
- [Unity Examples](https://github.com/playgama/bridge-unity-examples)
- [JavaScript Bridge](https://github.com/playgama/bridge)
