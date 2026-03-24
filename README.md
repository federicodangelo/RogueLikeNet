# RogueLikeNet

A multiplayer ASCII roguelike game built with .NET 10. Features procedurally generated dungeons, real-time combat, ECS architecture, and both desktop and web browser clients.

## Architecture

```
┌─────────────────────────────────────────────────────┐
│                    Clients                          │
│  ┌──────────────────┐   ┌────────────────────────┐  │
│  │ Client.Desktop   │   │ Client.Web (WASM/PWA)  │  │
│  │ Avalonia WinExe  │   │ Avalonia.Browser       │  │
│  └────────┬─────────┘   └──────────┬─────────────┘  │
│           └──────────┬─────────────┘                │
│              ┌───────┴────────┐                     │
│              │  Client.Core   │                     │
│              │  Rendering +   │                     │
│              │  Networking    │                     │
│              └───────┬────────┘                     │
└──────────────────────┼──────────────────────────────┘
          WebSocket    │   or in-process
┌──────────────────────┼──────────────────────────────┐
│              ┌───────┴────────┐                     │
│              │    Protocol    │                     │
│              │  MessagePack   │                     │
│              └───────┬────────┘                     │
│              ┌───────┴────────┐                     │
│              │    Server      │                     │
│              │  ASP.NET Core  │                     │
│              │  GameLoop 20Hz │                     │
│              │  SQLite persist│                     │
│              └───────┬────────┘                     │
│              ┌───────┴────────┐                     │
│              │     Core       │                     │
│              │  ECS (Arch)    │                     │
│              │  Game Logic    │                     │
│              └────────────────┘                     │
└─────────────────────────────────────────────────────┘
```

## Projects

### `src/RogueLikeNet.Core`

The pure game logic library. Zero dependencies on networking, rendering, or persistence. Uses the [Arch](https://github.com/genaray/Arch) ECS framework.

| Folder | Description |
|--------|-------------|
| `Components/` | ECS components — all use `int`/`long` values: `Position`, `Health`, `CombatStats`, `FOVData`, `LightSource`, `Inventory`, `ClassData`, `AIState`, `PlayerInput`, `TileAppearance`, `Tags` |
| `Algorithms/` | Custom integer-only algorithms: **ShadowCast FOV** (8-octant recursive), **A\* Pathfinding** (Manhattan heuristic), **Bresenham** line/LOS |
| `Systems/` | ECS systems: `MovementSystem`, `CombatSystem`, `AISystem`, `FOVSystem`, `LightingSystem` |
| `Generation/` | Procedural content: `BspDungeonGenerator` (BSP tree rooms+corridors), `SeededRandom` (xoshiro256\*\*), `TileDefinitions` (CP437 glyphs) |
| `World/` | World structure: `Chunk` (64×64 tiles), `WorldMap` (chunk dictionary), `TileInfo` |
| `GameEngine.cs` | Orchestrates all systems, manages the ECS world and world map |

### `src/RogueLikeNet.Protocol`

Network message definitions using [MessagePack](https://github.com/MessagePack-CSharp/MessagePack-CSharp) binary serialization.

| File | Description |
|------|-------------|
| `Messages/NetworkEnvelope.cs` | Type-discriminated message wrapper + `MessageTypes` constants |
| `Messages/ClientInputMsg.cs` | Client → Server: player actions (move, attack, use item) |
| `Messages/WorldSnapshotMsg.cs` | Server → Client: full world state (chunks + entities) |
| `Messages/WorldDeltaMsg.cs` | Server → Client: incremental updates (entity moves, combat events) |
| `Messages/AuthMsg.cs` | Authentication request/response + chat messages |
| `NetSerializer.cs` | Serialize/deserialize helpers with `UntrustedData` security |

### `src/RogueLikeNet.Server`

Authoritative game server built on ASP.NET Core with WebSocket transport.

| File | Description |
|------|-------------|
| `Program.cs` | Server entry point — WebSocket endpoint at `/ws`, health check at `/` |
| `GameLoop.cs` | 20 tick/sec authoritative loop, manages connections, processes inputs, broadcasts deltas |
| `PlayerConnection.cs` | Per-player server state with concurrent input queue |
| `WebSocketHandler.cs` | WebSocket middleware — read loop, message dispatch |
| `Persistence/GameDbContext.cs` | EF Core context with SQLite — player accounts, characters, world chunks |
| `Persistence/GamePersistence.cs` | Save/load operations for player and world data |

**Default URL:** `http://localhost:5090` (WebSocket at `ws://localhost:5090/ws`)

### `src/RogueLikeNet.Client.Core`

Shared client library — the **only** layer that contains rendering code (SkiaSharp) and float operations.

| File | Description |
|------|-------------|
| `Rendering/TileRenderer.cs` | SkiaSharp renderer — CP437 character map, tile coloring, lighting |
| `Rendering/GameRenderControl.cs` | Avalonia `Control` — 30fps render loop, keyboard input (WASD/arrows) |
| `Networking/IGameServerConnection.cs` | Connection abstraction (`ConnectAsync`, `SendInputAsync`, events) |
| `Networking/WebSocketServerConnection.cs` | WebSocket client implementation for remote server play |
| `State/ClientGameState.cs` | Client-side world state — applies snapshots and deltas |

### `src/RogueLikeNet.Client.Desktop`

Avalonia desktop application. Can run in **standalone mode** (embedded server) or connect to a remote server.

| File | Description |
|------|-------------|
| `Program.cs` | Avalonia entry point with platform detection |
| `App.axaml` / `App.axaml.cs` | Application setup with Fluent dark theme |
| `MainWindow.cs` | Main window hosting `GameRenderControl`, starts embedded server |
| `EmbeddedServerConnection.cs` | In-process bridge between `GameLoop` and `IGameServerConnection` |

### `src/RogueLikeNet.Client.Web`

Avalonia Browser (WebAssembly) client. Runs the game engine **locally in the browser** for offline play (PWA). Can also connect to a remote server via WebSocket.

| File | Description |
|------|-------------|
| `Program.cs` | Avalonia Browser entry point |
| `App.axaml` / `App.axaml.cs` | Application setup (Fluent theme, single-view lifecycle) |
| `MainView.cs` | Main view hosting `GameRenderControl` |
| `EmbeddedServerConnection.cs` | Local `GameEngine` running in WASM — no server dependency |
| `wwwroot/` | Static files: `index.html`, `manifest.json`, service workers for offline caching |

### `tests/`

| Project | Coverage |
|---------|----------|
| `RogueLikeNet.Core.Tests` | Position, Health, Chunk, SeededRandom, BSP dungeon generation, Bresenham, A\*, ShadowCast FOV, WorldMap, GameEngine |
| `RogueLikeNet.Protocol.Tests` | MessagePack round-trip, envelope wrapping, snapshot/delta serialization |
| `RogueLikeNet.Server.Tests` | GameLoop lifecycle, connections, player spawning, input queuing |

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

Starts with an embedded server — no separate server needed. Use WASD or arrow keys to move, Space to wait.

### Run the web client

The web client requires publishing to WebAssembly first, then serving the static files:

```bash
dotnet publish src/RogueLikeNet.Client.Web -c Release
# Then serve the output with any static file server
```

## Scripts

Platform-specific scripts are provided in the `scripts/` folder:

| Script | Description |
|--------|-------------|
| `scripts/build.ps1` / `scripts/build.sh` | Build the entire solution |
| `scripts/test.ps1` / `scripts/test.sh` | Run all unit tests |
| `scripts/run-server.ps1` / `scripts/run-server.sh` | Start the dedicated multiplayer server |
| `scripts/run-desktop.ps1` / `scripts/run-desktop.sh` | Launch the desktop client (standalone) |
| `scripts/run-web.ps1` / `scripts/run-web.sh` | Publish and serve the web client locally |

## Controls

| Key | Action |
|-----|--------|
| `W` / `↑` | Move north |
| `S` / `↓` | Move south |
| `A` / `←` | Move west |
| `D` / `→` | Move east |
| `Space` | Wait one turn |

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Game Logic | [Arch ECS](https://github.com/genaray/Arch) 2.0 — Entity Component System |
| Serialization | [MessagePack-CSharp](https://github.com/MessagePack-CSharp/MessagePack-CSharp) 3.1 — binary protocol |
| UI Framework | [Avalonia](https://avaloniaui.net/) 11.2 — cross-platform (Desktop + WASM) |
| Rendering | SkiaSharp — 2D tile rendering with CP437 character set |
| Persistence | EF Core + SQLite — player accounts, characters, world chunks |
| Server | ASP.NET Core (Kestrel) — WebSocket transport |
| PRNG | xoshiro256\*\* — deterministic seeded generation |

## Design Principles

- **Integer-only game logic** — all game state uses `int`/`long`; floats exist only in the rendering layer
- **Strict visualization boundary** — Core and Server know nothing about rendering
- **Authoritative server** — 20 tick/sec loop; clients send inputs, receive state
- **Deterministic generation** — same seed always produces the same dungeon
- **Offline-capable web client** — PWA with service workers, runs game engine in WASM

## License

MIT
