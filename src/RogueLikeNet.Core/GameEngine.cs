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
    private readonly CraftingSystem _craftingSystem;
    private readonly BuildingSystem _buildingSystem;
    private readonly SeededRandom _worldRng;
    private long _tick;
    private (int X, int Y, int Z)? _generatorSpawnHint;

    public Arch.Core.World EcsWorld => _ecsWorld;
    public WorldMap WorldMap => _worldMap;
    public long CurrentTick => _tick;
    public CombatSystem Combat => _combatSystem;
    public InventorySystem Inventory => _inventorySystem;

    /// <summary>Debug: when true, player movement ignores tile collision.</summary>
    public bool DebugNoCollision { get; set; }

    /// <summary>Debug: when true, player cannot take damage.</summary>
    public bool DebugInvulnerable { get; set; }

    /// <summary>Debug: when true, player has zero move/attack delay.</summary>
    public bool DebugMaxSpeed { get; set; }

    public GameEngine(long worldSeed, IDungeonGenerator? generator = null)
    {
        _ecsWorld = Arch.Core.World.Create();
        _worldMap = new WorldMap(worldSeed);
        _generator = generator ?? new OverworldGenerator(worldSeed);
        _movementSystem = new MovementSystem();
        _fovSystem = new FOVSystem();
        _lightingSystem = new LightingSystem();
        _combatSystem = new CombatSystem();
        _aiSystem = new AISystem(worldSeed);
        _inventorySystem = new InventorySystem();
        _skillSystem = new SkillSystem();
        _craftingSystem = new CraftingSystem();
        _buildingSystem = new BuildingSystem();
        _worldRng = new SeededRandom(worldSeed);
    }

    public Chunk? EnsureChunkLoadedOrDoesntExist(int chunkX, int chunkY, int chunkZ)
    {
        if (!_worldMap.ExistsChunk(chunkX, chunkY, chunkZ, _generator))
            return null;
        return EnsureChunkLoaded(chunkX, chunkY, chunkZ);
    }

    /// <summary>
    /// Ensures the chunk at the given chunk coords is loaded/generated.
    /// Spawns entities from generation results if newly created.
    /// </summary>
    public Chunk EnsureChunkLoaded(int chunkX, int chunkY, int chunkZ)
    {
        var (chunk, genResult) = _worldMap.GetOrCreateChunk(chunkX, chunkY, chunkZ, _generator);

        if (genResult != null)
            ProcessGenerationResult(genResult);

        return chunk;
    }

    private void ProcessGenerationResult(GenerationResult result)
    {
        // Store the spawn hint from chunk (0,0) — each subsequent chunk may overwrite
        // only if it's the origin chunk (generators only set it for chunkX==0, chunkY==0).
        if (result.SpawnPosition.HasValue && result.Chunk.ChunkX == 0 && result.Chunk.ChunkY == 0)
            _generatorSpawnHint = result.SpawnPosition;

        foreach (var (pos, monster) in result.Monsters)
        {
            SpawnMonster(pos.X, pos.Y, pos.Z, monster);
        }

        foreach (var (pos, item) in result.Items)
        {
            SpawnItemOnGround(item, pos.X, pos.Y, pos.Z);
        }

        foreach (var element in result.Elements)
        {
            SpawnElement(element);
        }

        foreach (var (pos, nodeDef) in result.ResourceNodes)
        {
            SpawnResourceNode(pos.X, pos.Y, pos.Z, nodeDef);
        }

        foreach (var (pos, name, tcx, tcy, radius) in result.TownNpcs)
        {
            SpawnTownNpc(pos.X, pos.Y, pos.Z, name, tcx, tcy, radius);
        }
    }

    /// <summary>
    /// Spawns a player entity at the given world position.
    /// Class choice affects starting stats.
    /// </summary>
    public Entity SpawnPlayer(long connectionId, int x, int y, int z, int classId)
    {
        var def = ClassDefinitions.Get(classId);
        var classStats = def.StartingStats; ;
        var stats = classStats + ClassDefinitions.BaseStats;

        // Speed maps to delay: higher speed → lower delay
        var moveDelay = Math.Max(0, 10 - (6 + classStats.Speed));
        var attackDelay = Math.Max(0, 10 - (6 + classStats.Speed));

        return _ecsWorld.Create(
            new Position(x, y, z),
            new Health(stats.Health),
            new CombatStats(stats.Attack, stats.Defense, stats.Speed),
            new FOVData(ClassDefinitions.FOVRadius),
            new TileAppearance(TileDefinitions.GlyphPlayer, TileDefinitions.ColorWhite),
            new PlayerTag { ConnectionId = connectionId },
            new PlayerInput(),
            new ClassData { ClassId = classId, Level = 1 },
            new SkillSlots { Skill0 = def.StartingSkill0, Skill1 = def.StartingSkill1 },
            new Inventory(ClassDefinitions.InventorySlots),
            new Equipment(),
            new QuickSlots(),
            new MoveDelay(moveDelay),
            new AttackDelay(attackDelay)
        );
    }

    /// <summary>
    /// Gives the player 9999 of each resource type. Used for debug mode.
    /// </summary>
    public void GiveDebugResources(Entity playerEntity)
    {
        if (!_ecsWorld.IsAlive(playerEntity)) return;
        ref var inv = ref _ecsWorld.Get<Inventory>(playerEntity);
        if (inv.Items == null) return;

        ReadOnlySpan<int> resourceIds = [ItemDefinitions.Wood, ItemDefinitions.CopperOre, ItemDefinitions.IronOre, ItemDefinitions.GoldOre];
        foreach (int resId in resourceIds)
        {
            inv.Items.Add(new ItemData
            {
                ItemTypeId = resId,
                Rarity = ItemDefinitions.RarityCommon,
                StackCount = 9999,
            });
        }
    }

    /// <summary>
    /// Spawns a monster at the given position using fully-populated MonsterData.
    /// </summary>
    public Entity SpawnMonster(int x, int y, int z, MonsterData data)
    {
        var def = NpcDefinitions.Get(data.MonsterTypeId);

        // Speed maps to move delay: higher speed → lower delay.
        // Speed 10 = every tick (0 delay), speed 6 = every 3rd tick, etc.
        int moveInterval = Math.Max(0, 10 - data.Speed);

        return _ecsWorld.Create(
            new Position(x, y, z),
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
    public Entity SpawnItemOnGround(ItemDefinition def, int rarity, int x, int y, int z)
    {
        var itemData = ItemDefinitions.GenerateItemData(def, rarity, _worldRng);
        return SpawnItemOnGround(itemData, x, y, z);
    }

    /// <summary>
    /// Creates an item entity on the ground from pre-built ItemData.
    /// </summary>
    public Entity SpawnItemOnGround(ItemData itemData, int x, int y, int z)
    {
        var def = ItemDefinitions.Get(itemData.ItemTypeId);
        return _ecsWorld.Create(
            new Position(x, y, z),
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
    /// Spawns a resource node (tree, ore rock) that can be mined.
    /// </summary>
    public Entity SpawnResourceNode(int x, int y, int z, ResourceNodeDefinition def)
    {
        return _ecsWorld.Create(
            new Position(x, y, z),
            new Health(def.Health),
            new CombatStats(0, def.Defense, 0),
            new TileAppearance(def.GlyphId, def.Color),
            new ResourceNodeData
            {
                ResourceItemTypeId = def.ResourceItemTypeId,
                MinDrop = def.MinDrop,
                MaxDrop = def.MaxDrop,
            },
            new AttackDelay(0)
        );
    }

    /// <summary>
    /// Spawns a peaceful town NPC that wanders within a radius.
    /// </summary>
    public Entity SpawnTownNpc(int x, int y, int z, string name, int townCenterX, int townCenterY, int wanderRadius)
    {
        return _ecsWorld.Create(
            new Position(x, y, z),
            new Health(9999), // Effectively unkillable
            new CombatStats(0, 999, 3),
            new TileAppearance(TileDefinitions.GlyphTownNpc, TileDefinitions.ColorTownNpcFg),
            new AIState { StateId = AIStates.Idle },
            new MoveDelay(5),
            new AttackDelay(0),
            new TownNpcTag
            {
                Name = name,
                TownCenterX = townCenterX,
                TownCenterY = townCenterY,
                WanderRadius = wanderRadius,
                TalkTimer = 0,
                DialogueIndex = 0,
            }
        );
    }

    /// <summary>
    /// Runs one game tick: process inputs → move → combat → AI → inventory → skills → FOV → lighting.
    /// </summary>
    public void Tick()
    {
        _movementSystem.Update(_ecsWorld, _worldMap, DebugNoCollision, DebugMaxSpeed);
        _combatSystem.Update(_ecsWorld, DebugInvulnerable);
        _aiSystem.Update(_ecsWorld, _worldMap);
        _inventorySystem.Update(_ecsWorld, _worldMap);
        _craftingSystem.Update(_ecsWorld);
        _buildingSystem.Update(_ecsWorld, _worldMap);
        _skillSystem.Update(_ecsWorld);
        _worldMap.Update(_ecsWorld);
        _fovSystem.Update(_ecsWorld, _worldMap);
        _lightingSystem.Update(_ecsWorld, _worldMap);

        ProcessLootDrops();
        ProcessPlayerDeath();
        CleanupDead();

        _tick++;
    }

    /// <summary>
    /// Drop loot when monsters die (before entity destruction).
    /// Also drop resources when resource nodes are destroyed.
    /// </summary>
    private void ProcessLootDrops()
    {
        var deadMonsters = new List<(int X, int Y, int Z, int MonsterTypeId)>();
        var deathQuery = new QueryDescription().WithAll<DeadTag, MonsterData, Position>();
        _ecsWorld.Query(in deathQuery, (ref Position pos, ref MonsterData tag) =>
        {
            deadMonsters.Add((pos.X, pos.Y, pos.Z, tag.MonsterTypeId));
        });

        foreach (var (x, y, z, typeId) in deadMonsters)
        {
            // 60% chance to drop loot
            if (_worldRng.Next(100) < 60)
            {
                int difficulty = typeId; // rough mapping: harder monster = higher difficulty
                var (template, rarity) = ItemDefinitions.GenerateLoot(_worldRng, difficulty);
                var (dropX, dropY, dropZ) = FindDropPosition(_ecsWorld, x, y, z);
                SpawnItemOnGround(template, rarity, dropX, dropY, dropZ);
            }
        }

        // Resource node drops
        var deadNodes = new List<(int X, int Y, int Z, ResourceNodeData Data)>();
        var nodeDeathQuery = new QueryDescription().WithAll<DeadTag, ResourceNodeData, Position>();
        _ecsWorld.Query(in nodeDeathQuery, (ref Position pos, ref ResourceNodeData node) =>
        {
            deadNodes.Add((pos.X, pos.Y, pos.Z, node));
        });

        foreach (var (x, y, z, node) in deadNodes)
        {
            int dropCount = node.MinDrop + _worldRng.Next(Math.Max(1, node.MaxDrop - node.MinDrop + 1));
            var resourceDef = ItemDefinitions.Get(node.ResourceItemTypeId);
            var (dropX, dropY, dropZ) = FindDropPosition(_ecsWorld, x, y, z);
            SpawnItemOnGround(new ItemData
            {
                ItemTypeId = node.ResourceItemTypeId,
                Rarity = ItemDefinitions.RarityCommon,
                StackCount = dropCount,
            }, dropX, dropY, dropZ);
        }
    }

    /// <summary>
    /// Finds an unoccupied position to drop an item, spiraling outward from origin.
    /// Avoids overlapping with other ground items.
    /// </summary>
    public static (int X, int Y, int Z) FindDropPosition(Arch.Core.World world, int originX, int originY, int originZ)
    {
        // Collect positions of all ground items
        var occupied = new HashSet<long>();
        var groundQuery = new QueryDescription().WithAll<Position, ItemData>();
        world.Query(in groundQuery, (ref Position gPos) =>
        {
            occupied.Add(Position.PackCoord(gPos.X, gPos.Y, gPos.Z));
        });

        // Try origin first, then spiral outward up to radius 5
        if (!occupied.Contains(Position.PackCoord(originX, originY, originZ)))
            return (originX, originY, originZ);

        for (int r = 1; r <= 5; r++)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Math.Abs(dx) != r && Math.Abs(dy) != r) continue; // only check ring
                    int x = originX + dx;
                    int y = originY + dy;
                    if (!occupied.Contains(Position.PackCoord(x, y, originZ)))
                        return (x, y, originZ);
                }
        }

        return (originX, originY, originZ); // fallback
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
            var (sx, sy, sz) = FindSpawnPosition();
            health.Current = health.Max / 2;
            pos.X = sx;
            pos.Y = sy;
            pos.Z = sz;

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
        var deadNodeQuery = new QueryDescription().WithAll<DeadTag, ResourceNodeData>();
        _ecsWorld.Query(in deadNodeQuery, (Entity entity) =>
        {
            toDestroy.Add(entity);
        });
        foreach (var entity in toDestroy)
        {
            _ecsWorld.Destroy(entity);
        }
    }

    /// <summary>
    /// Finds a suitable spawn position for the player.
    /// Uses the generator's suggested spawn point when available, otherwise
    /// falls back to a floor-scan of chunk (0,0).
    /// </summary>
    public (int X, int Y, int Z) FindSpawnPosition()
    {
        var chunk = EnsureChunkLoaded(0, 0, Position.DefaultZ);

        // Use the generator's hint if it points at a walkable floor tile
        if (_generatorSpawnHint.HasValue)
        {
            var (hx, hy) = (_generatorSpawnHint.Value.X, _generatorSpawnHint.Value.Y);
            // Convert world coords to local chunk coords
            int lx = hx - 0 * Chunk.Size;
            int ly = hy - 0 * Chunk.Size;
            if (lx >= 0 && lx < Chunk.Size && ly >= 0 && ly < Chunk.Size
                && chunk.Tiles[lx, ly].IsWalkable)
            {
                return (hx, hy, Position.DefaultZ);
            }
        }

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
            occupied.Add(Position.PackCoord(p.X, p.Y, p.Z));
        });

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                if (chunk.Tiles[x, y].Type != TileType.Floor) continue;
                if (occupied.Contains(Position.PackCoord(x, y, Position.DefaultZ))) continue;

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

                return (x, y, Position.DefaultZ);
            }
        // Fallback: just find any free floor tile
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                if (chunk.Tiles[x, y].Type == TileType.Floor && !occupied.Contains(Position.PackCoord(x, y, Position.DefaultZ)))
                    return (x, y, Position.DefaultZ);
            }
        return (Chunk.Size / 2, Chunk.Size / 2, Position.DefaultZ);
    }

    /// <summary>
    /// Destroys all non-player entities within the given chunk bounds.
    /// Used before unloading a chunk to clean up ECS entities.
    /// </summary>
    public void DestroyEntitiesInChunk(int chunkX, int chunkY, int chunkZ)
    {
        int minX = chunkX * Chunk.Size;
        int maxX = minX + Chunk.Size - 1;
        int minY = chunkY * Chunk.Size;
        int maxY = minY + Chunk.Size - 1;

        var toDestroy = new List<Entity>();
        var query = new QueryDescription().WithAll<Position>().WithNone<PlayerTag>();
        _ecsWorld.Query(in query, (Entity entity, ref Position pos) =>
        {
            if (pos.X >= minX && pos.X <= maxX && pos.Y >= minY && pos.Y <= maxY && pos.Z == chunkZ)
                toDestroy.Add(entity);
        });

        foreach (var entity in toDestroy)
        {
            if (_ecsWorld.IsAlive(entity))
                _ecsWorld.Destroy(entity);
        }
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
