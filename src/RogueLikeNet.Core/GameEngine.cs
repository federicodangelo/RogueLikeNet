using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core;

/// <summary>
/// The core game engine. Owns the ECS world, world map, and all systems.
/// No rendering, no networking — pure game logic.
/// </summary>
public class GameEngine : IDisposable
{
    private readonly Arch.Core.World _ecsWorld;
    private readonly WorldMap _worldMap;
    private readonly IDungeonGenerator _generator;
    private readonly MovementSystem _movementSystem;
    private readonly FOVSystem _fovSystem;
    private readonly LightingSystem _lightingSystem;
    private readonly CombatSystem _combatSystem;
    private readonly AISystem _aiSystem;
    private long _tick;

    public Arch.Core.World EcsWorld => _ecsWorld;
    public WorldMap WorldMap => _worldMap;
    public long CurrentTick => _tick;
    public CombatSystem Combat => _combatSystem;

    public GameEngine(long worldSeed)
    {
        _ecsWorld = Arch.Core.World.Create();
        _worldMap = new WorldMap(worldSeed);
        _generator = new BspDungeonGenerator();
        _movementSystem = new MovementSystem();
        _fovSystem = new FOVSystem();
        _lightingSystem = new LightingSystem();
        _combatSystem = new CombatSystem();
        _aiSystem = new AISystem();
    }

    /// <summary>
    /// Ensures the chunk at the given chunk coords is loaded/generated.
    /// </summary>
    public Chunk EnsureChunkLoaded(int chunkX, int chunkY)
    {
        return _worldMap.GetOrCreateChunk(chunkX, chunkY, _generator);
    }

    /// <summary>
    /// Spawns a player entity at the given world position.
    /// Returns the entity reference.
    /// </summary>
    public Entity SpawnPlayer(long connectionId, int x, int y)
    {
        return _ecsWorld.Create(
            new Position(x, y),
            new Health(100),
            new CombatStats(10, 5, 10),
            new FOVData(10),
            new TileAppearance(TileDefinitions.GlyphPlayer, TileDefinitions.ColorWhite),
            new PlayerTag { ConnectionId = connectionId },
            new PlayerInput(),
            new ClassData { ClassId = ClassIds.Warrior, Level = 1 },
            new Inventory(20)
        );
    }

    /// <summary>
    /// Spawns a monster at the given position.
    /// </summary>
    public Entity SpawnMonster(int monsterTypeId, int x, int y, int glyphId, int color,
        int health = 20, int attack = 5, int defense = 2, int speed = 8)
    {
        return _ecsWorld.Create(
            new Position(x, y),
            new Health(health),
            new CombatStats(attack, defense, speed),
            new TileAppearance(glyphId, color),
            new MonsterTag { MonsterTypeId = monsterTypeId },
            new AIState { StateId = AIStates.Idle }
        );
    }

    /// <summary>
    /// Runs one game tick: process inputs → move → combat → AI → FOV → lighting.
    /// </summary>
    public void Tick()
    {
        _movementSystem.Update(_ecsWorld, _worldMap);
        _combatSystem.Update(_ecsWorld);
        _aiSystem.Update(_ecsWorld, _worldMap);
        _fovSystem.Update(_ecsWorld, _worldMap);
        _lightingSystem.Update(_ecsWorld, _worldMap);

        // Cleanup dead entities
        CleanupDead();

        _tick++;
    }

    private void CleanupDead()
    {
        var toDestroy = new List<Entity>();
        var deadQuery = new QueryDescription().WithAll<DeadTag, MonsterTag>();
        _ecsWorld.Query(in deadQuery, (Entity entity) =>
        {
            toDestroy.Add(entity);
        });
        foreach (var entity in toDestroy)
        {
            _ecsWorld.Destroy(entity);
        }
    }

    /// <summary>
    /// Finds a suitable spawn position in the first room of chunk (0,0).
    /// </summary>
    public (int X, int Y) FindSpawnPosition()
    {
        var chunk = EnsureChunkLoaded(0, 0);
        // Find first floor tile
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
        {
            if (chunk.Tiles[x, y].Type == TileType.Floor)
                return (x, y);
        }
        return (Chunk.Size / 2, Chunk.Size / 2);
    }

    public void Dispose()
    {
        Arch.Core.World.Destroy(_ecsWorld);
    }
}
