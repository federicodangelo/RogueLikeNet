using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core;

/// <summary>
/// The core game engine. Owns the world map, entity storage, and all systems.
/// No rendering, no networking — pure game logic.
/// Entities are stored as typed classes in per-chunk lists (monsters, items, etc.)
/// with players stored globally in WorldMap.
/// </summary>
public class GameEngine : IDisposable
{
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

    /// <summary>
    /// Optional callback to deserialize raw entity JSON from saved chunks.
    /// Set by the server layer so entities are restored after the chunk is registered in WorldMap.
    /// </summary>
    public Action<string, GameEngine>? RawEntityJsonHandler { get; set; }

    public GameEngine(long worldSeed, IDungeonGenerator? generator = null)
    {
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
        if (result.SpawnPosition.HasValue && result.Chunk.ChunkX == 0 && result.Chunk.ChunkY == 0)
            _generatorSpawnHint = result.SpawnPosition;

        // Deserialize saved entities first (from persistence layer).
        // This runs after the chunk is registered in WorldMap, so Spawn* can find it.
        if (result.RawEntityJson != null)
            RawEntityJsonHandler?.Invoke(result.RawEntityJson, this);

        foreach (var (pos, monster) in result.Monsters)
            SpawnMonster(pos.X, pos.Y, pos.Z, monster);

        foreach (var (pos, item) in result.Items)
            SpawnItemOnGround(item, pos.X, pos.Y, pos.Z);

        foreach (var element in result.Elements)
            SpawnElement(element);

        foreach (var (pos, nodeDef) in result.ResourceNodes)
            SpawnResourceNode(pos.X, pos.Y, pos.Z, nodeDef);

        foreach (var (pos, name, tcx, tcy, radius) in result.TownNpcs)
            SpawnTownNpc(pos.X, pos.Y, pos.Z, name, tcx, tcy, radius);
    }

    // ── Entity creation ──────────────────────────────────────────────

    /// <summary>
    /// Spawns a player entity at the given world position.
    /// Class choice affects starting stats.
    /// </summary>
    public PlayerEntity SpawnPlayer(long connectionId, int x, int y, int z, int classId)
    {
        var def = ClassDefinitions.Get(classId);
        var classStats = def.StartingStats;
        var stats = classStats + ClassDefinitions.BaseStats;
        var moveDelay = Math.Max(0, 10 - (6 + classStats.Speed));
        var attackDelay = Math.Max(0, 10 - (6 + classStats.Speed));

        var player = new PlayerEntity
        {
            Id = _worldMap.AllocateEntityId(),
            ConnectionId = connectionId,
            X = x,
            Y = y,
            Z = z,
            Health = new Health(stats.Health),
            CombatStats = new CombatStats(stats.Attack, stats.Defense, stats.Speed),
            FOV = new FOVData(ClassDefinitions.FOVRadius),
            Appearance = new TileAppearance(TileDefinitions.GlyphPlayer, TileDefinitions.ColorWhite),
            Input = new PlayerInput(),
            ClassData = new ClassData { ClassId = classId, Level = 1 },
            Skills = new SkillSlots { Skill0 = def.StartingSkill0, Skill1 = def.StartingSkill1 },
            Inventory = new Inventory(ClassDefinitions.InventorySlots),
            Equipment = new Equipment(),
            QuickSlots = new QuickSlots(),
            MoveDelay = new MoveDelay(moveDelay),
            AttackDelay = new AttackDelay(attackDelay),
        };
        _worldMap.AddPlayer(player);
        return player;
    }

    /// <summary>
    /// Gives the player 9999 of each resource type. Used for debug mode.
    /// </summary>
    public void GiveDebugResources(PlayerEntity player)
    {
        if (player.Inventory.Items == null) return;

        ReadOnlySpan<int> resourceIds = [ItemDefinitions.Wood, ItemDefinitions.CopperOre, ItemDefinitions.IronOre, ItemDefinitions.GoldOre];
        foreach (int resId in resourceIds)
        {
            player.Inventory.Items.Add(new ItemData
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
    public MonsterEntity SpawnMonster(int x, int y, int z, MonsterData data)
    {
        var def = NpcDefinitions.Get(data.MonsterTypeId);
        int moveInterval = Math.Max(0, 10 - data.Speed);
        var monster = new MonsterEntity
        {
            Id = _worldMap.AllocateEntityId(),
            X = x,
            Y = y,
            Z = z,
            MonsterData = data,
            Health = new Health(data.Health),
            CombatStats = new CombatStats(data.Attack, data.Defense, data.Speed),
            Appearance = new TileAppearance(def.GlyphId, def.Color),
            AI = new AIState { StateId = AIStates.Idle },
            MoveDelay = new MoveDelay(moveInterval),
            AttackDelay = new AttackDelay(moveInterval),
        };

        var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, z);
        var chunk = _worldMap.TryGetChunk(cx, cy, cz);
        if (chunk != null)
        {
            chunk.Monsters.Add(monster);
            chunk.MarkModified();
        }
        return monster;
    }

    /// <summary>
    /// Creates an item entity lying on the ground.
    /// </summary>
    public GroundItemEntity SpawnItemOnGround(ItemDefinition def, int rarity, int x, int y, int z)
    {
        var itemData = ItemDefinitions.GenerateItemData(def, rarity, _worldRng);
        return SpawnItemOnGround(itemData, x, y, z);
    }

    /// <summary>
    /// Creates an item entity on the ground from pre-built ItemData.
    /// </summary>
    public GroundItemEntity SpawnItemOnGround(ItemData itemData, int x, int y, int z)
    {
        var def = ItemDefinitions.Get(itemData.ItemTypeId);
        var item = new GroundItemEntity
        {
            Id = _worldMap.AllocateEntityId(),
            X = x,
            Y = y,
            Z = z,
            Appearance = new TileAppearance(def.GlyphId, def.Color),
            Item = itemData,
        };

        var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, z);
        var chunk = _worldMap.TryGetChunk(cx, cy, cz);
        if (chunk != null)
        {
            chunk.GroundItems.Add(item);
            chunk.MarkModified();
        }
        return item;
    }

    /// <summary>
    /// Spawns a dungeon element (decoration with optional light).
    /// </summary>
    public ElementEntity SpawnElement(DungeonElement element)
    {
        var elem = new ElementEntity
        {
            Id = _worldMap.AllocateEntityId(),
            X = element.Position.X,
            Y = element.Position.Y,
            Z = element.Position.Z,
            Appearance = element.Appearance,
            Light = element.Light,
        };

        var (cx, cy, cz) = Chunk.WorldToChunkCoord(elem.X, elem.Y, elem.Z);
        var chunk = _worldMap.TryGetChunk(cx, cy, cz);
        if (chunk != null)
        {
            chunk.Elements.Add(elem);
            chunk.MarkModified();
        }
        return elem;
    }

    /// <summary>
    /// Spawns a resource node (tree, ore rock) that can be mined.
    /// </summary>
    public ResourceNodeEntity SpawnResourceNode(int x, int y, int z, ResourceNodeDefinition def)
    {
        var node = new ResourceNodeEntity
        {
            Id = _worldMap.AllocateEntityId(),
            X = x,
            Y = y,
            Z = z,
            Health = new Health(def.Health),
            CombatStats = new CombatStats(0, def.Defense, 0),
            Appearance = new TileAppearance(def.GlyphId, def.Color),
            NodeData = new ResourceNodeData
            {
                NodeTypeId = def.NodeTypeId,
                ResourceItemTypeId = def.ResourceItemTypeId,
                MinDrop = def.MinDrop,
                MaxDrop = def.MaxDrop,
            },
            AttackDelay = new AttackDelay(0),
        };

        var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, z);
        var chunk = _worldMap.TryGetChunk(cx, cy, cz);
        if (chunk != null)
        {
            chunk.ResourceNodes.Add(node);
            chunk.MarkModified();
        }
        return node;
    }

    /// <summary>
    /// Spawns a peaceful town NPC that wanders within a radius.
    /// </summary>
    public TownNpcEntity SpawnTownNpc(int x, int y, int z, string name, int townCenterX, int townCenterY, int wanderRadius)
    {
        var npc = new TownNpcEntity
        {
            Id = _worldMap.AllocateEntityId(),
            X = x,
            Y = y,
            Z = z,
            Health = new Health(9999),
            CombatStats = new CombatStats(0, 999, 3),
            Appearance = new TileAppearance(TileDefinitions.GlyphTownNpc, TileDefinitions.ColorTownNpcFg),
            AI = new AIState { StateId = AIStates.Idle },
            MoveDelay = new MoveDelay(5),
            AttackDelay = new AttackDelay(0),
            NpcData = new TownNpcTag
            {
                Name = name,
                TownCenterX = townCenterX,
                TownCenterY = townCenterY,
                WanderRadius = wanderRadius,
                TalkTimer = 0,
                DialogueIndex = 0,
            },
        };

        var (cx, cy, cz) = Chunk.WorldToChunkCoord(x, y, z);
        var chunk = _worldMap.TryGetChunk(cx, cy, cz);
        if (chunk != null)
        {
            chunk.TownNpcs.Add(npc);
            chunk.MarkModified();
        }
        return npc;
    }

    // ── Tick ──────────────────────────────────────────────────────────

    /// <summary>
    /// Runs one game tick: process inputs -> move -> combat -> AI -> inventory -> skills -> FOV -> lighting.
    /// </summary>
    public void Tick()
    {
        _movementSystem.Update(_worldMap, DebugNoCollision, DebugMaxSpeed);
        _combatSystem.Update(_worldMap, DebugInvulnerable);
        _aiSystem.Update(_worldMap);
        _inventorySystem.Update(_worldMap, this);
        _craftingSystem.Update(_worldMap);
        _buildingSystem.Update(_worldMap, this);
        _skillSystem.Update(_worldMap);
        _worldMap.Update();
        _fovSystem.Update(_worldMap);
        _lightingSystem.Update(_worldMap);

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
        foreach (var chunk in _worldMap.LoadedChunks)
            foreach (var m in chunk.Monsters)
                if (m.IsDead)
                    deadMonsters.Add((m.X, m.Y, m.Z, m.MonsterData.MonsterTypeId));

        foreach (var (x, y, z, typeId) in deadMonsters)
        {
            if (_worldRng.Next(100) < 60)
            {
                int difficulty = typeId;
                var (template, rarity) = ItemDefinitions.GenerateLoot(_worldRng, difficulty);
                var (dropX, dropY, dropZ) = FindDropPosition(x, y, z);
                SpawnItemOnGround(template, rarity, dropX, dropY, dropZ);
            }
        }

        var deadNodes = new List<(int X, int Y, int Z, ResourceNodeData Data)>();
        foreach (var chunk in _worldMap.LoadedChunks)
            foreach (var r in chunk.ResourceNodes)
                if (r.IsDead)
                    deadNodes.Add((r.X, r.Y, r.Z, r.NodeData));

        foreach (var (x, y, z, node) in deadNodes)
        {
            int dropCount = node.MinDrop + _worldRng.Next(Math.Max(1, node.MaxDrop - node.MinDrop + 1));
            var (dropX, dropY, dropZ) = FindDropPosition(x, y, z);
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
    public (int X, int Y, int Z) FindDropPosition(int originX, int originY, int originZ)
    {
        var occupied = new HashSet<long>();
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(originX, originY, originZ);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                var chunk = _worldMap.TryGetChunk(cx + dx, cy + dy, cz);
                if (chunk == null) continue;
                foreach (var item in chunk.GroundItems)
                    if (!item.IsDead) occupied.Add(Position.PackCoord(item.X, item.Y, item.Z));
            }

        if (!occupied.Contains(Position.PackCoord(originX, originY, originZ)))
            return (originX, originY, originZ);

        for (int r = 1; r <= 5; r++)
        {
            for (int ddx = -r; ddx <= r; ddx++)
                for (int ddy = -r; ddy <= r; ddy++)
                {
                    if (Math.Abs(ddx) != r && Math.Abs(ddy) != r) continue;
                    int x = originX + ddx;
                    int y = originY + ddy;
                    if (!occupied.Contains(Position.PackCoord(x, y, originZ)))
                        return (x, y, originZ);
                }
        }

        return (originX, originY, originZ);
    }

    /// <summary>
    /// Handle player death: respawn at chunk origin with restored health.
    /// </summary>
    private void ProcessPlayerDeath()
    {
        foreach (var player in _worldMap.Players.Values)
        {
            if (!player.IsDead) continue;

            var (sx, sy, sz) = FindSpawnPosition();
            player.Health.Current = player.Health.Max / 2;
            player.X = sx;
            player.Y = sy;
            player.Z = sz;
            player.IsDead = false;
            player.ClassData.Experience = Math.Max(0, player.ClassData.Experience - player.ClassData.Experience / 4);
        }
    }

    private void CleanupDead()
    {
        foreach (var chunk in _worldMap.LoadedChunks)
        {
            foreach (var m in chunk.Monsters)
                if (m.IsDead) _worldMap.SetTileChunkDirty(m.X, m.Y, m.Z);
            foreach (var r in chunk.ResourceNodes)
                if (r.IsDead) _worldMap.SetTileChunkDirty(r.X, r.Y, r.Z);
            foreach (var i in chunk.GroundItems)
                if (i.IsDead) _worldMap.SetTileChunkDirty(i.X, i.Y, i.Z);

            chunk.RemoveDeadEntities();
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

        if (_generatorSpawnHint.HasValue)
        {
            var (hx, hy) = (_generatorSpawnHint.Value.X, _generatorSpawnHint.Value.Y);
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
        foreach (var m in chunk.Monsters)
            if (!m.IsDead) enemyPositions.Add((m.X, m.Y));

        var occupied = new HashSet<long>();
        foreach (var player in _worldMap.Players.Values)
            if (!player.IsDead) occupied.Add(Position.PackCoord(player.X, player.Y, player.Z));
        foreach (var m in chunk.Monsters)
            if (!m.IsDead) occupied.Add(Position.PackCoord(m.X, m.Y, m.Z));
        foreach (var n in chunk.TownNpcs)
            if (!n.IsDead) occupied.Add(Position.PackCoord(n.X, n.Y, n.Z));
        foreach (var r in chunk.ResourceNodes)
            if (!r.IsDead) occupied.Add(Position.PackCoord(r.X, r.Y, r.Z));

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                if (chunk.Tiles[x, y].Type != TileType.Floor) continue;
                if (occupied.Contains(Position.PackCoord(x, y, Position.DefaultZ))) continue;

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

                bool enemyNearby = false;
                foreach (var (ex, ey) in enemyPositions)
                {
                    if (Math.Max(Math.Abs(ex - x), Math.Abs(ey - y)) <= 5)
                    { enemyNearby = true; break; }
                }
                if (enemyNearby) continue;

                return (x, y, Position.DefaultZ);
            }

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                if (chunk.Tiles[x, y].Type == TileType.Floor && !occupied.Contains(Position.PackCoord(x, y, Position.DefaultZ)))
                    return (x, y, Position.DefaultZ);
            }
        return (Chunk.Size / 2, Chunk.Size / 2, Position.DefaultZ);
    }

    /// <summary>
    /// Clears all entities within the given chunk (used before unloading).
    /// </summary>
    public void DestroyEntitiesInChunk(int chunkX, int chunkY, int chunkZ)
    {
        var chunk = _worldMap.TryGetChunk(chunkX, chunkY, chunkZ);
        chunk?.ClearEntities();
    }

    public void Dispose()
    {
        // No ECS world to dispose
    }

    /// <summary>
    /// Returns data for a player entity (health, stats, inventory, skills).
    /// </summary>
    public PlayerStateData? GetPlayerStateData(PlayerEntity player)
    {
        if (player.IsDead) return null;

        var state = new PlayerStateData
        {
            Health = player.Health.Current,
            MaxHealth = player.Health.Max,
            Attack = player.CombatStats.Attack,
            Defense = player.CombatStats.Defense,
            Level = player.ClassData.Level,
            Experience = player.ClassData.Experience,
        };

        if (player.Inventory.Items != null)
        {
            state.InventoryCount = player.Inventory.Items.Count;
            state.InventoryCapacity = player.Inventory.Capacity;

            var items = new List<InventoryItemData>();
            foreach (var item in player.Inventory.Items)
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

        if (player.Equipment.HasWeapon)
        {
            var w = player.Equipment.Weapon!.Value;
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
        if (player.Equipment.HasArmor)
        {
            var a = player.Equipment.Armor!.Value;
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

        state.QuickSlotIndices = [player.QuickSlots.Slot0, player.QuickSlots.Slot1, player.QuickSlots.Slot2, player.QuickSlots.Slot3];

        state.Skills = [
            new SkillSlotData { Id = player.Skills.Skill0, Cooldown = player.Skills.Cooldown0, Name = SkillDefinitions.GetName(player.Skills.Skill0) },
            new SkillSlotData { Id = player.Skills.Skill1, Cooldown = player.Skills.Cooldown1, Name = SkillDefinitions.GetName(player.Skills.Skill1) },
            new SkillSlotData { Id = player.Skills.Skill2, Cooldown = player.Skills.Cooldown2, Name = SkillDefinitions.GetName(player.Skills.Skill2) },
            new SkillSlotData { Id = player.Skills.Skill3, Cooldown = player.Skills.Cooldown3, Name = SkillDefinitions.GetName(player.Skills.Skill3) },
        ];

        return state;
    }
}
