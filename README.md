# RogueLikeNet

[![Build](https://github.com/federicodangelo/RogueLikeNet/actions/workflows/build.yml/badge.svg)](https://github.com/federicodangelo/RogueLikeNet/actions/workflows/build.yml)
[![Release](https://img.shields.io/github/v/release/federicodangelo/RogueLikeNet)](https://github.com/federicodangelo/RogueLikeNet/releases/latest)

<p align="center">
  <a href="https://federicodangelo.github.io/RogueLikeNet/">
    <img src="https://img.shields.io/badge/%E2%96%B6%20PLAY%20IN%20BROWSER-federicodangelo.github.io%2FRogueLikeNet-brightgreen?style=for-the-badge" alt="Play in Browser" />
  </a>
</p>

A multiplayer ASCII roguelike game built with .NET 10. Features procedurally generated dungeons with multiple generators (BSP, biome-based, cellular automata caves, overworld, town), real-time combat, typed entity architecture, item stacking and equipment, spells and mana system, quest system with wayfinding, NPC dialogue and trading, persistent save/load with SQLite, and both desktop (SDL3/native AOT) and web browser (WASM) clients.

## About This Project

This project was created as an experiment to push the boundaries of what's possible when using **AI coding agents** for game development. The entire codebase — rendering, procedural generation, entity architecture, UI, and gameplay — was written by [Claude](https://www.anthropic.com/claude) (Opus 4.6 and Sonnet 4.6) through iterative prompting.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                       Clients                           │
│  ┌───────────────────────┐  ┌──────────────────────┐    │
│  │   Client.Desktop      │  │    Client.Web        │    │
│  │   (Native AOT exe)    │  │  (browser-wasm/PWA)  │    │
│  │  Engine.Sdl (SDL3)    │  │  Engine.Web (Canvas) │    │
│  └──────────┬────────────┘  └──────────┬───────────┘    │
│             └──────────────┬────────────┘               │
│                   ┌────────┴────────┐                   │
│                   │  Client.Core    │                   │
│                   │  Rendering +    │                   │
│                   │  Networking     │                   │
│                   └────────┬────────┘                   │
│                   ┌────────┴────────┐                   │
│                   │     Engine      │                   │
│                   │  IPlatform +    │                   │
│                   │  ISpriteRenderer│                   │
│                   │  (abstractions) │                   │
│                   └─────────────────┘                   │
└─────────────────────────────────────────────────────────┘
           WebSocket   │   or in-process
┌──────────────────────┼──────────────────────────────────┐
│              ┌───────┴────────┐                         │
│              │    Protocol    │                         │
│              │  MessagePack   │                         │
│              └───────┬────────┘                         │
│              ┌───────┴────────┐                         │
│              │    Server      │                         │
│              │  ASP.NET Core  │                         │
│              │ GameServer 20Hz│                         │
│              │  SQLite persist│                         │
│              └───────┬────────┘                         │
│              ┌───────┴────────┐                         │
│              │     Core       │                         │
│              │  Typed Entities│                         │
│              │  Game Logic    │                         │
│              └────────────────┘                         │
└─────────────────────────────────────────────────────────┘
```

## Projects

### `src/Engine`

Platform abstraction library. Zero external dependencies, AOT-compatible. Defines interfaces for all cross-platform capabilities.

| Folder / File                 | Description                                                                                                     |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------- |
| `Platform/IPlatform.cs`       | Top-level platform interface: window, renderer, input, audio, settings, save game                               |
| `Platform/ISpriteRenderer.cs` | 2D rendering interface: rectangles, circles, lines, text, textures, tile grids, glyph grids                     |
| `Platform/IFontRenderer.cs`   | Bitmap font rendering interface                                                                                 |
| `Platform/IInputManager.cs`   | Keyboard and pointer input abstraction                                                                          |
| `Platform/IAudioManager.cs`   | Audio playback abstraction                                                                                      |
| `Platform/ITextureManager.cs` | Texture loading and management abstraction                                                                      |
| `Platform/ISaveGame.cs`       | Persistent save data abstraction                                                                                |
| `Platform/GameBase.cs`        | Base class for games — holds `IPlatform` and exposes subsystem accessors                                        |
| `Platform/Base/`              | Shared base implementations of all platform interfaces                                                          |
| `Platform/Null/`              | Null (no-op) implementations for testing and headless use                                                       |
| `Rendering/Base/`             | Shared rendering utilities: `BaseFontRenderer`, `BufferedFontRenderer`, `MiniBitmapFont`, `RenderCommandBuffer` |
| `Core/EngineTypes.cs`         | Value types used across the engine: `Color3`, `Color4`, `Rect`, `GlyphTile`, etc.                               |
| `Core/Camera.cs`              | 2D camera (world→screen transform)                                                                              |

### `src/Engine.Sdl`

SDL3 desktop implementation of the Engine platform interfaces. Used by the desktop client.

| File                                 | Description                                   |
| ------------------------------------ | --------------------------------------------- |
| `Platform/Sdl/SdlPlatform.cs`        | SDL3 window, event loop, and subsystem wiring |
| `Platform/Sdl/SdlSpriteRenderer.cs`  | SDL3 GPU-accelerated 2D renderer              |
| `Platform/Sdl/SdlFontRenderer.cs`    | Bitmap font rendering via SDL3                |
| `Platform/Sdl/SdlTextureManager.cs`  | SDL3 texture loading                          |
| `Platform/Sdl/SdlInputManager.cs`    | SDL3 keyboard and mouse input                 |
| `Platform/Sdl/SdlAudioManager.cs`    | SDL3 audio                                    |
| `Platform/Sdl/SdlTileMapRenderer.cs` | Optimised CP437 tile-grid renderer            |
| `Platform/Sdl/SdlSaveGame.cs`        | File-based save/load                          |
| `Platform/Sdl/SdlSettings.cs`        | SDL3 platform settings                        |

**Package:** SDL3-CS 3.4.2

### `src/Engine.Web`

Browser / WebAssembly implementation of the Engine platform interfaces. Used by the web client.

| File                                | Description                                    |
| ----------------------------------- | ---------------------------------------------- |
| `Platform/Web/WebPlatform.cs`       | Browser platform wiring                        |
| `Platform/Web/WebSpriteRenderer.cs` | Canvas 2D rendering via JS interop             |
| `Platform/Web/WebFontRenderer.cs`   | Bitmap font rendering in the browser           |
| `Platform/Web/WebTextureManager.cs` | Image loading via JS interop                   |
| `Platform/Web/WebInputManager.cs`   | Keyboard/pointer input via JS events           |
| `Platform/Web/WebAudioManager.cs`   | Web Audio API via JS interop                   |
| `Platform/Web/JsInterop.cs`         | `[JSImport]`/`[JSExport]` bridge to JavaScript |
| `Platform/Web/WebSaveGame.cs`       | `localStorage`-backed save/load                |
| `Platform/Web/WebSettings.cs`       | Browser-side settings                          |

### `src/RogueLikeNet.Core`

The pure game logic library. Zero external dependencies (only references the Engine abstraction). Contains no networking, rendering, or persistence code. Uses typed entity classes stored in per-chunk lists.

| Folder          | Description                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `World/`        | Entity definitions and world structure: 7 entity classes (`PlayerEntity`, `MonsterEntity`, `GroundItemEntity`, `ResourceNodeEntity`, `TownNpcEntity`, `AnimalEntity`, `CropEntity`) in individual files, `EntitiesCollection` base (Span-based per-chunk storage + ref-struct enumerators), `Chunk` (64×64 tiles + entity lists), `WorldMap` (chunk dictionary + player registry), `TileInfo`                                                                                                           |
| `Components/`   | Entity components — all use `int`/`long` values: `Position`, `Health`, `CombatStats`, `Mana`, `FOVData`, `LightSource`, `Inventory`, `ClassData`, `AIState`, `PlayerInput`, `TileAppearance`, `Tags`, `Equipment`, `QuickSlots`, `PlayerQuests`, `Survival`, `ActiveEffects`, `GridVelocity`                                                                                                                                                                                                            |
| `Algorithms/`   | Custom integer-only algorithms: **ShadowCast FOV** (8-octant recursive), **A\* Pathfinding** (Manhattan heuristic), **Bresenham** line/LOS                                                                                                                                                                                                                                                                                                                                                              |
| `Systems/`      | Game systems: `MovementSystem`, `CombatSystem`, `AISystem`, `FOVSystem`, `LightingSystem`, `InventorySystem`, `CraftingSystem`, `BuildingSystem`, `QuestSystem`, `SpellSystem`, `TradingSystem`, `FarmingSystem`, `AnimalSystem`, `SurvivalSystem`, `ActiveEffectsSystem`                                                                                                                                                                                                                               |
| `Generation/`   | Procedural content: `BspDungeonGenerator` (BSP rooms+corridors), `BiomeDungeonGenerator` (biome-themed floors), `CellularAutomataCaveGenerator` (organic caves), `DirectionalTunnelGenerator` (winding tunnels), `OverworldGenerator` (world map with biome climate), `TownGenerator` (towns with buildings, shops, and NPCs), `ArenaGenerator`, `StructurePlacer` (JSON-driven structure placement), `MultiLevelDungeonGenerator`, `GeneratorRegistry`, `PerlinNoise`, `SeededRandom` (xoshiro256\*\*) |
| `Data/`         | Data-driven definitions and registries: `ItemRegistry`, `TilesRegistry`, `NpcRegistry`, `ClassRegistry`, `RecipeRegistry`, `ResourceNodeRegistry`, `AnimalRegistry`, `BiomeRegistry`, `QuestRegistry`, `ShopRegistry`, `SpellRegistry`, `StructureRegistry`, `TownRegistry`, `PlayerLevelTable`, `GameData` singleton, `DataLoader` (two-stage JSON → registry pipeline), `DefinitionIdHash` (MurmurHash3 numeric IDs)                                                                                  |
| `GameEngine.cs` | Orchestrates all systems, manages entity spawning/migration and the world map                                                                                                                                                                                                                                                                                                                                                                                                                           |

### `src/RogueLikeNet.Protocol`

Network message definitions using [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) binary serialization.

| File                             | Description                                                                          |
| -------------------------------- | ------------------------------------------------------------------------------------ |
| `Messages/NetworkEnvelope.cs`    | Type-discriminated message wrapper + `MessageTypes` constants                        |
| `Messages/ClientInputMsg.cs`     | Client → Server: player actions (move, attack, use item, cast spell, interact)       |
| `Messages/ChunkDataMsg.cs`       | Server → Client: chunk tile + entity data                                            |
| `Messages/WorldDeltaMsg.cs`      | Server → Client: incremental updates (entity moves, combat events, NPC interactions) |
| `Messages/PlayerStateMsg.cs`     | Server → Client: player HUD data (HP, mana, stats, inventory, equipment, quests)     |
| `Messages/LoginMsg.cs`           | Client → Server: login request with username/password                                |
| `Messages/LoginResponseMsg.cs`   | Server → Client: login result + player info                                          |
| `Messages/QuestMessages.cs`      | Quest-related messages (quest state, updates)                                        |
| `Messages/NpcInteractionMsg.cs`  | NPC dialogue and shop interaction messages                                           |
| `Messages/CombatEventMsg.cs`     | Server → Client: combat hit/miss/kill events                                         |
| `Messages/EntityUpdateMsg.cs`    | Server → Client: entity state changes                                                |
| `Messages/SaveGameCommandMsg.cs` | Client → Server: save/load game slot commands                                        |
| `Messages/TileUpdateMsg.cs`      | Server → Client: individual tile change notifications                                |
| `NetSerializer.cs`               | Serialize/deserialize helpers with `UntrustedData` security                          |
| `GameStateSerializer.cs`         | Shared helpers for entity/chunk/HUD serialization                                    |
| `ChunkTracker.cs`                | Tracks which chunks each client has received for delta compression                   |

### `src/RogueLikeNet.Server.Core`

Server-side game logic: authoritative game loop, connection management, and persistence.

| File / Folder                               | Description                                                                                                  |
| ------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| `GameServer.cs`                             | 20 tick/sec authoritative loop, manages connections, processes inputs, broadcasts deltas, auto-save every 5s |
| `PlayerConnection.cs`                       | Per-player server state with concurrent input queue                                                          |
| `Persistence/ISaveGameProvider.cs`          | Save/load abstraction — chunk, player, and world metadata persistence                                        |
| `Persistence/SqliteSaveGameProvider.cs`     | SQLite implementation via `Microsoft.Data.Sqlite` — save slots, chunks, players                              |
| `Persistence/InMemorySaveGameProvider.cs`   | In-memory implementation for tests and offline play                                                          |
| `Persistence/EntitySerializer.cs`           | JSON serialization for chunk entities (monsters, items, NPCs, etc.)                                          |
| `Persistence/PlayerSerializer.cs`           | Player state serialization (position, stats, inventory, equipment, skills)                                   |
| `Persistence/ChunkSerializer.cs`            | Tile data serialization for chunk persistence                                                                |
| `Persistence/PersistentDungeonGenerator.cs` | Wraps dungeon generators to restore previously-saved chunks on load                                          |
| `Persistence/SaveDataTypes.cs`              | Save data DTOs: `PlayerSaveData`, `ChunkSaveEntry`, `WorldSaveData`, `SaveSlotInfo`                          |

### `src/RogueLikeNet.Server`

ASP.NET Core host for the authoritative game server.

| File                        | Description                                                           |
| --------------------------- | --------------------------------------------------------------------- |
| `Program.cs`                | Server entry point — WebSocket endpoint at `/ws`, health check at `/` |
| `ServerWebSocketHandler.cs` | WebSocket middleware — read loop, message dispatch                    |

**Default URL:** `http://localhost:5090` (WebSocket at `ws://localhost:5090/ws`)

### `src/RogueLikeNet.Client.Core`

Shared client library — the **only** layer that contains rendering code and float operations. Renders through the `Engine` abstraction layer (`ISpriteRenderer`), not directly to any platform API.

| File / Folder                             | Description                                                                                                                                                                                                                                                                                              |
| ----------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `RogueLikeGame.cs`                        | Main game class — state machine (`MainMenu` → `Login` → `Connecting` → `Playing`), events for `StartOfflineRequested`, `StartOnlineRequested`, `ReturnToMenuRequested`, `QuitRequested`                                                                                                                  |
| `Screens/`                                | Screen stack manager + per-screen logic: `PlayingScreen`, `InventoryScreen`, `CraftingScreen`, `QuestLogScreen`, `NpcDialogueScreen`, `PausedScreen`, `OptionsScreen`, `HelpScreen`; Menus: `MainMenuScreen`, `LoginScreen`, `ClassSelectScreen`, `NewGameScreen`, `SaveSlotScreen`, `ServerAdminScreen` |
| `Rendering/Game/`                         | In-game rendering: `TileRenderer` (CP437 glyph grid), `PlayingBackdropRenderer`                                                                                                                                                                                                                          |
| `Rendering/Hud/`                          | HUD renderers: `HudRenderer`, `InventoryRenderer`, `CraftingRenderer`, `QuestLogRenderer`, `NpcDialogueRenderer`                                                                                                                                                                                         |
| `Rendering/Menus/`                        | Menu renderers: `MainMenuRenderer`, `LoginRenderer`, `ClassSelectRenderer`, `NewGameRenderer`, `SaveSlotMenuRenderer`, `OptionsRenderer`, `HelpRenderer`, `ConnectingRenderer`, `ServerAdminRenderer`                                                                                                    |
| `Rendering/ParticleSystem.cs`             | Particle effects (projectile trails, hit sparks, etc.)                                                                                                                                                                                                                                                   |
| `Networking/IGameServerConnection.cs`     | Connection abstraction (`ConnectAsync`, `SendInputAsync`, events)                                                                                                                                                                                                                                        |
| `Networking/WebSocketServerConnection.cs` | WebSocket client implementation for remote server play                                                                                                                                                                                                                                                   |
| `Networking/EmbeddedServerConnection.cs`  | In-process bridge to an embedded `GameLoop` for standalone/offline play — shared by Desktop and Web                                                                                                                                                                                                      |
| `State/ClientGameState.cs`                | Client-side world state — applies snapshots and deltas                                                                                                                                                                                                                                                   |

### `src/RogueLikeNet.Client.Desktop`

Native SDL3 desktop application compiled with **Native AOT** (`PublishAot=true`, full trimming). Can run in **standalone mode** (embedded server) or connect to a remote server.

| File         | Description                                                                                           |
| ------------ | ----------------------------------------------------------------------------------------------------- |
| `Program.cs` | Entry point — creates `SdlPlatform` (1280×960), initialises `RogueLikeGame`, and drives the game loop |

### `src/RogueLikeNet.Client.Web`

Browser WebAssembly client (`RuntimeIdentifier: browser-wasm`). Runs the game engine **locally in the browser** for offline play (PWA). Can also connect to a remote server via WebSocket.

| File         | Description                                                                                                                               |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `Program.cs` | WASM entry point — creates `WebPlatform`, initialises `RogueLikeGame`; exports `RunOneFrame()` via `[JSExport]` for the JS animation loop |
| `wwwroot/`   | Static files: `index.html`, `manifest.json`, service workers for offline caching (PWA)                                                    |

### `tests/`

| Project                          | Tests | Coverage                                                                                                                                                                                                                                                                                                                                                                                     |
| -------------------------------- | ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `RogueLikeNet.Core.Tests`        | 969   | Position, Health, Chunk, SeededRandom, BSP/biome/cave/tunnel/overworld/town dungeon generation, Bresenham, A\*, ShadowCast FOV, WorldMap, GameEngine, CombatSystem, InventorySystem, QuestSystem, SpellSystem, TradingSystem, FarmingSystem, AnimalSystem, ItemDefinitions, NpcDefinitions, ClassData, Loot, QuickSlots, entity migration, chunk dirty tracking, structures, town generation |
| `RogueLikeNet.Client.Core.Tests` | 103   | BiomePalette, ClientGameState, HudLayout, ParticleSystem, DebugSettings                                                                                                                                                                                                                                                                                                                      |
| `RogueLikeNet.Protocol.Tests`    | 86    | MessagePack round-trip, envelope wrapping, delta/player-state serialization, GameStateSerializer, ChunkTracker, login/quest/NPC interaction messages                                                                                                                                                                                                                                         |
| `RogueLikeNet.Server.Tests`      | 156   | GameServer lifecycle, connections, player spawning, input queuing, save/load persistence, entity serialization round-trip, player serialization, SQLite provider, TradingSystem                                                                                                                                                                                                              |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.201 or later)

All NuGet packages are restored automatically on build.

## Quick Start

### Build everything

```bash
dotnet build
```

### Run tests

```bash
dotnet test
```

### Run the dedicated server

```bash
dotnet run --project src/RogueLikeNet.Server
```

The server starts at `http://localhost:5090`. WebSocket endpoint: `ws://localhost:5090/ws`.

### Run the desktop client (standalone mode)

```bash
dotnet run --project src/RogueLikeNet.Client.Desktop
```

Opens an SDL3 window. Starts with an embedded server — no separate server needed. Use WASD or arrow keys to move, Space to wait.

### Run the web client

The web client targets `browser-wasm` and must be published before serving:

```bash
dotnet publish src/RogueLikeNet.Client.Web -c Release
# Serve with any static file server, e.g.:
dotnet tool run dotnet-serve -- bin/Release/net10.0/browser-wasm/AppBundle
```

Or use the provided script which handles publishing and serving in one step:

```bash
.\scripts\run-web.ps1    # Windows
./scripts/run-web.sh     # Linux/macOS
```

## Scripts

Platform-specific scripts are provided in the `scripts/` folder:

| Script                                               | Description                              |
| ---------------------------------------------------- | ---------------------------------------- |
| `scripts/build.ps1` / `scripts/build.sh`             | Build the entire solution                |
| `scripts/test.ps1` / `scripts/test.sh`               | Run all unit tests                       |
| `scripts/run-server.ps1` / `scripts/run-server.sh`   | Start the dedicated multiplayer server   |
| `scripts/run-desktop.ps1` / `scripts/run-desktop.sh` | Launch the desktop client (standalone)   |
| `scripts/run-web.ps1` / `scripts/run-web.sh`         | Publish and serve the web client locally |

## Controls

| Key       | Action                         |
| --------- | ------------------------------ |
| `W` / `↑` | Move north                     |
| `S` / `↓` | Move south                     |
| `A` / `←` | Move west                      |
| `D` / `→` | Move east                      |
| `Space`   | Wait one turn                  |
| `F`       | Attack nearest enemy           |
| `G`       | Pick up item                   |
| `1`-`8`   | Use quick-slot item            |
| `E`       | Interact (NPC / door / object) |
| `X`       | Drop item                      |
| `I`       | Open inventory                 |
| `C`       | Open crafting                  |
| `Q`       | Open quest log                 |
| `M`       | Cast spell                     |
| `P`       | Place held item                |
| `.` / `,` | Use stairs (down / up)         |
| `T`       | Open chat                      |
| `Tab`     | Cycle panel section            |
| `Escape`  | Pause / Back                   |

### Inventory Controls

| Key       | Action              |
| --------- | ------------------- |
| `↑` / `↓` | Navigate slots      |
| `Enter`   | Use selected item   |
| `E`       | Equip selected item |
| `U`       | Unequip weapon      |
| `R`       | Unequip armor       |
| `X`       | Drop selected item  |
| `Escape`  | Close inventory     |

## Technology Stack

| Component            | Technology                                                                                                                 |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| Game Logic           | Custom typed entity classes with per-chunk storage                                                                         |
| Serialization        | [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp) 3.1 — binary protocol                       |
| Platform Abstraction | Custom `Engine` library — `IPlatform`, `ISpriteRenderer`, `IInputManager`, `IAudioManager`                                 |
| Desktop Rendering    | [SDL3-CS](https://github.com/flibitijibibo/SDL3-CS) 3.4.2 — GPU-accelerated 2D via SDL3                                    |
| Web Rendering        | Browser Canvas 2D via `[JSImport]`/`[JSExport]` interop (`browser-wasm` runtime)                                           |
| Desktop Compilation  | Native AOT — full-trimmed native executable, no JIT                                                                        |
| Persistence          | [Microsoft.Data.Sqlite](https://learn.microsoft.com/dotnet/standard/data/sqlite/) — save slots, player state, world chunks |
| Server               | ASP.NET Core (Kestrel) — WebSocket transport                                                                               |
| PRNG                 | xoshiro256\*\* — deterministic seeded generation                                                                           |

## Design Principles

- **Integer-only game logic** — all game state uses `int`/`long`; floats exist only in the rendering layer
- **Strict visualization boundary** — Core and Server know nothing about rendering
- **Authoritative server** — 20 tick/sec loop; clients send inputs, receive state
- **Deterministic generation** — same seed always produces the same dungeon
- **Offline-capable web client** — PWA with service workers, runs game engine entirely in WASM
- **Native AOT desktop** — desktop client is compiled ahead-of-time for fast startup and no JIT overhead

## License

MIT
