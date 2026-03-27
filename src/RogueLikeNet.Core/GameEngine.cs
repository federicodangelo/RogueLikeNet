using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
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
    private readonly InventorySystem _inventorySystem;
    private readonly SkillSystem _skillSystem;
    private readonly SeededRandom _worldRng;
    private long _tick;

    public Arch.Core.World EcsWorld => _ecsWorld;
    public WorldMap WorldMap => _worldMap;
    public long CurrentTick => _tick;
    public CombatSystem Combat => _combatSystem;
    public InventorySystem Inventory => _inventorySystem;

    public GameEngine(long worldSeed, IDungeonGenerator? generator = null)
    {
        _ecsWorld = Arch.Core.World.Create();
        _worldMap = new WorldMap(worldSeed);
        _generator = generator ?? new OverworldGenerator(worldSeed);
        _movementSystem = new MovementSystem();
        _fovSystem = new FOVSystem();
        _lightingSystem = new LightingSystem();
        _combatSystem = new CombatSystem();
        _aiSystem = new AISystem();
        _inventorySystem = new InventorySystem();
        _skillSystem = new SkillSystem();
        _worldRng = new SeededRandom(worldSeed);
    }

    /// <summary>
    /// Ensures the chunk at the given chunk coords is loaded/generated.
    /// Spawns entities from generation results if newly created.
    /// </summary>
    public Chunk EnsureChunkLoaded(int chunkX, int chunkY)
    {
        var (chunk, genResult) = _worldMap.GetOrCreateChunk(chunkX, chunkY, _generator);

        if (genResult != null)
            ProcessGenerationResult(genResult);

        return chunk;
    }

    private void ProcessGenerationResult(GenerationResult result)
    {
        foreach (var (pos, monster) in result.Monsters)
        {
            SpawnMonster(pos.X, pos.Y, monster);
        }

        foreach (var (pos, item) in result.Items)
        {
            SpawnItemOnGround(item, pos.X, pos.Y);
        }

        foreach (var element in result.Elements)
        {
            SpawnElement(element);
        }
    }

    /// <summary>
    /// Spawns a player entity at the given world position.
    /// Class choice affects starting stats.
    /// </summary>
    public Entity SpawnPlayer(long connectionId, int x, int y, int classId)
    {
        var stats = ClassDefinitions.GetStartingStats(classId);
        var skills = ClassDefinitions.GetStartingSkills(classId);

        return _ecsWorld.Create(
            new Position(x, y),
            new Health(100 + stats.Health),
            new CombatStats(10 + stats.Attack, 5 + stats.Defense, 10 + stats.Speed),
            new FOVData(ClassDefinitions.FOVRadius),
            new TileAppearance(TileDefinitions.GlyphPlayer, TileDefinitions.ColorWhite),
            new PlayerTag { ConnectionId = connectionId },
            new PlayerInput(),
            new ClassData { ClassId = classId, Level = 1 },
            skills,
            new Inventory(ClassDefinitions.InventorySlots),
            new Equipment(),
            new QuickSlots(),
            new MoveDelay(Math.Max(0, 10 - (6 + stats.Speed))),
            new AttackDelay(Math.Max(0, 10 - (6 + stats.Speed)))
        );
    }

    /// <summary>
    /// Spawns a monster at the given position using fully-populated MonsterData.
    /// </summary>
    public Entity SpawnMonster(int x, int y, MonsterData data)
    {
        var def = NpcDefinitions.Get(data.MonsterTypeId);

        // Speed maps to move delay: higher speed → lower delay.
        // Speed 10 = every tick (0 delay), speed 6 = every 3rd tick, etc.
        int moveInterval = Math.Max(0, 10 - data.Speed);

        return _ecsWorld.Create(
            new Position(x, y),
            new Health(data.Health),
            new CombatStats(data.Attack, data.Defense, data.Speed),
            new TileAppearance(def.GlyphId, def.Color),
            data,
            new AIState { StateId = AIStates.Idle },
            new MoveDelay(moveInterval),
            new AttackDelay(moveInterval)
        );
    }

    /// <summary>
    /// Creates an item entity lying on the ground.
    /// </summary>
    public Entity SpawnItemOnGround(ItemDefinition def, int rarity, int x, int y)
    {
        var itemData = ItemDefinitions.GenerateItemData(def, rarity, _worldRng);
        return SpawnItemOnGround(itemData, x, y);
    }

    /// <summary>
    /// Creates an item entity on the ground from pre-built ItemData.
    /// </summary>
    public Entity SpawnItemOnGround(ItemData itemData, int x, int y)
    {
        var def = ItemDefinitions.Get(itemData.ItemTypeId);
        return _ecsWorld.Create(
            new Position(x, y),
            new TileAppearance(def.GlyphId, def.Color),
            itemData
        );
    }

    /// <summary>
    /// Spawns a dungeon element (decoration with optional light).
    /// </summary>
    public Entity SpawnElement(DungeonElement element)
    {
        if (element.Light is { } light)
        {
            return _ecsWorld.Create(
                element.Position,
                element.Appearance,
                light
            );
        }
        return _ecsWorld.Create(
            element.Position,
            element.Appearance
        );
    }

    /// <summary>
    /// Runs one game tick: process inputs → move → combat → AI → inventory → skills → FOV → lighting.
    /// </summary>
    public void Tick()
    {
        _movementSystem.Update(_ecsWorld, _worldMap);
        _combatSystem.Update(_ecsWorld);
        _aiSystem.Update(_ecsWorld, _worldMap);
        _inventorySystem.Update(_ecsWorld, _worldMap);
        _skillSystem.Update(_ecsWorld);
        _fovSystem.Update(_ecsWorld, _worldMap);
        _lightingSystem.Update(_ecsWorld, _worldMap);

        ProcessLootDrops();
        ProcessPlayerDeath();
        CleanupDead();

        _tick++;
    }

    /// <summary>
    /// Drop loot when monsters die (before entity destruction).
    /// </summary>
    private void ProcessLootDrops()
    {
        var deadMonsters = new List<(int X, int Y, int MonsterTypeId)>();
        var deathQuery = new QueryDescription().WithAll<DeadTag, MonsterData, Position>();
        _ecsWorld.Query(in deathQuery, (ref Position pos, ref MonsterData tag) =>
        {
            deadMonsters.Add((pos.X, pos.Y, tag.MonsterTypeId));
        });

        foreach (var (x, y, typeId) in deadMonsters)
        {
            // 60% chance to drop loot
            if (_worldRng.Next(100) < 60)
            {
                int difficulty = typeId; // rough mapping: harder monster = higher difficulty
                var (template, rarity) = ItemDefinitions.GenerateLoot(_worldRng, difficulty);
                var (dropX, dropY) = FindDropPosition(_ecsWorld, x, y);
                SpawnItemOnGround(template, rarity, dropX, dropY);
            }
        }
    }

    /// <summary>
    /// Finds an unoccupied position to drop an item, spiraling outward from origin.
    /// Avoids overlapping with other ground items.
    /// </summary>
    public static (int X, int Y) FindDropPosition(Arch.Core.World world, int originX, int originY)
    {
        // Collect positions of all ground items
        var occupied = new HashSet<long>();
        var groundQuery = new QueryDescription().WithAll<Position, ItemData>();
        world.Query(in groundQuery, (ref Position gPos) =>
        {
            occupied.Add(Position.PackCoord(gPos.X, gPos.Y));
        });

        // Try origin first, then spiral outward up to radius 5
        if (!occupied.Contains(Position.PackCoord(originX, originY)))
            return (originX, originY);

        for (int r = 1; r <= 5; r++)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; // only check ring
                    int x = originX + dx;
                    int y = originY + dy;
                    if (!occupied.Contains(Position.PackCoord(x, y)))
                        return (x, y);
                }
        }

        return (originX, originY); // fallback
    }

    /// <summary>
    /// Handle player death: respawn at chunk origin with restored health.
    /// </summary>
    private void ProcessPlayerDeath()
    {
        var deadPlayers = new List<Entity>();
        var playerDeathQuery = new QueryDescription().WithAll<DeadTag, Health, PlayerTag, Position>();
        _ecsWorld.Query(in playerDeathQuery, (Entity entity) =>
        {
            deadPlayers.Add(entity);
        });

        foreach (var entity in deadPlayers)
        {
            ref var health = ref _ecsWorld.Get<Health>(entity);
            ref var pos = ref _ecsWorld.Get<Position>(entity);

            // Respawn: restore half health, move to spawn
            var (sx, sy) = FindSpawnPosition();
            health.Current = health.Max / 2;
            pos.X = sx;
            pos.Y = sy;

            // Remove DeadTag so player stays alive
            _ecsWorld.Remove<DeadTag>(entity);

            // Lose some experience on death
            if (_ecsWorld.Has<ClassData>(entity))
            {
                ref var classData = ref _ecsWorld.Get<ClassData>(entity);
                classData.Experience = Math.Max(0, classData.Experience - classData.Experience / 4);
            }
        }
    }

    private void CleanupDead()
    {
        var toDestroy = new List<Entity>();
        var deadQuery = new QueryDescription().WithAll<DeadTag, MonsterData>();
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

        // Collect enemy positions to enforce safety radius
        var enemyPositions = new List<(int X, int Y)>();
        var enemyQuery = new QueryDescription().WithAll<Position, MonsterData>();
        _ecsWorld.Query(in enemyQuery, (ref Position p) =>
        {
            enemyPositions.Add((p.X, p.Y));
        });

        // Collect occupied positions to avoid spawning on entities
        var occupied = new HashSet<long>();
        var posQuery = new QueryDescription().WithAll<Position>();
        _ecsWorld.Query(in posQuery, (ref Position p) =>
        {
            occupied.Add(Position.PackCoord(p.X, p.Y));
        });

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                if (chunk.Tiles[x, y].Type != TileType.Floor) continue;
                if (occupied.Contains(Position.PackCoord(x, y))) continue;

                // Require 2-tile clear floor radius (all tiles within Chebyshev 2 must be floor)
                bool clearArea = true;
                for (int dx = -2; dx <= 2 && clearArea; dx++)
                    for (int dy = -2; dy <= 2 && clearArea; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= Chunk.Size || ny >= Chunk.Size)
                        { clearArea = false; continue; }
                        if (chunk.Tiles[nx, ny].Type != TileType.Floor)
                            clearArea = false;
                    }
                if (!clearArea) continue;

                // Require no enemies within 5-tile Chebyshev radius
                bool enemyNearby = false;
                foreach (var (ex, ey) in enemyPositions)
                {
                    if (Math.Max(Math.Abs(ex - x), Math.Abs(ey - y)) <= 5)
                    { enemyNearby = true; break; }
                }
                if (enemyNearby) continue;

                return (x, y);
            }
        // Fallback: just find any free floor tile
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                if (chunk.Tiles[x, y].Type == TileType.Floor && !occupied.Contains(Position.PackCoord(x, y)))
                    return (x, y);
            }
        return (Chunk.Size / 2, Chunk.Size / 2);
    }

    public void Dispose()
    {
        Arch.Core.World.Destroy(_ecsWorld);
    }

    /// <summary>
    /// Returns data for a player entity (health, stats, inventory, skills).
    /// </summary>
    public PlayerStateData? GetPlayerStateData(Entity playerEntity)
    {
        if (!_ecsWorld.IsAlive(playerEntity)) return null;

        ref var health = ref _ecsWorld.Get<Health>(playerEntity);
        ref var stats = ref _ecsWorld.Get<CombatStats>(playerEntity);

        var state = new PlayerStateData
        {
            Health = health.Current,
            MaxHealth = health.Max,
            Attack = stats.Attack,
            Defense = stats.Defense,
        };

        if (_ecsWorld.Has<ClassData>(playerEntity))
        {
            ref var classData = ref _ecsWorld.Get<ClassData>(playerEntity);
            state.Level = classData.Level;
            state.Experience = classData.Experience;
        }

        if (_ecsWorld.Has<Inventory>(playerEntity))
        {
            ref var inv = ref _ecsWorld.Get<Inventory>(playerEntity);
            state.InventoryCount = inv.Items?.Count ?? 0;
            state.InventoryCapacity = inv.Capacity;

            // Build inventory items
            if (inv.Items != null)
            {
                var items = new List<InventoryItemData>();
                foreach (var item in inv.Items)
                {
                    var def = ItemDefinitions.Get(item.ItemTypeId);
                    items.Add(new InventoryItemData
                    {
                        ItemTypeId = item.ItemTypeId,
                        StackCount = item.StackCount,
                        Rarity = item.Rarity,
                        Category = def.Category,
                        BonusAttack = item.BonusAttack,
                        BonusDefense = item.BonusDefense,
                        BonusHealth = item.BonusHealth,
                    });
                }
                state.InventoryItems = items.ToArray();
            }
        }

        if (_ecsWorld.Has<Equipment>(playerEntity))
        {
            ref var equip = ref _ecsWorld.Get<Equipment>(playerEntity);
            if (equip.HasWeapon)
            {
                var w = equip.Weapon!.Value;
                var wDef = ItemDefinitions.Get(w.ItemTypeId);
                state.EquippedWeapon = new InventoryItemData
                {
                    ItemTypeId = w.ItemTypeId,
                    StackCount = w.StackCount,
                    Rarity = w.Rarity,
                    Category = wDef.Category,
                    BonusAttack = w.BonusAttack,
                    BonusDefense = w.BonusDefense,
                    BonusHealth = w.BonusHealth,
                };
            }
            if (equip.HasArmor)
            {
                var a = equip.Armor!.Value;
                var aDef = ItemDefinitions.Get(a.ItemTypeId);
                state.EquippedArmor = new InventoryItemData
                {
                    ItemTypeId = a.ItemTypeId,
                    StackCount = a.StackCount,
                    Rarity = a.Rarity,
                    Category = aDef.Category,
                    BonusAttack = a.BonusAttack,
                    BonusDefense = a.BonusDefense,
                    BonusHealth = a.BonusHealth,
                };
            }
        }

        if (_ecsWorld.Has<QuickSlots>(playerEntity))
        {
            ref var qs = ref _ecsWorld.Get<QuickSlots>(playerEntity);
            state.QuickSlotIndices = [qs.Slot0, qs.Slot1, qs.Slot2, qs.Slot3];
        }

        if (_ecsWorld.Has<SkillSlots>(playerEntity))
        {
            ref var skills = ref _ecsWorld.Get<SkillSlots>(playerEntity);
            state.Skills = [
                new SkillSlotData { Id = skills.Skill0, Cooldown = skills.Cooldown0, Name = SkillDefinitions.GetName(skills.Skill0) },
                new SkillSlotData { Id = skills.Skill1, Cooldown = skills.Cooldown1, Name = SkillDefinitions.GetName(skills.Skill1) },
                new SkillSlotData { Id = skills.Skill2, Cooldown = skills.Cooldown2, Name = SkillDefinitions.GetName(skills.Skill2) },
                new SkillSlotData { Id = skills.Skill3, Cooldown = skills.Cooldown3, Name = SkillDefinitions.GetName(skills.Skill3) },
            ];
        }

        // Floor items at player position — now derived client-side from entity data
        return state;
    }
}
