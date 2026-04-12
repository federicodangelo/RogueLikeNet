using System.Runtime.CompilerServices;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;
using PlayerStateData = RogueLikeNet.Core.Data.PlayerStateData;
using InventoryItemData = RogueLikeNet.Core.Data.InventoryItemData;
using RogueLikeNet.Core.Utilities;

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
    private readonly CraftingSystem _craftingSystem;
    private readonly BuildingSystem _buildingSystem;
    private readonly SurvivalSystem _survivalSystem;
    private readonly ActiveEffectsSystem _activeEffectsSystem;
    private readonly FarmingSystem _farmingSystem;
    private readonly AnimalSystem _animalSystem;
    private readonly SeededRandom _worldRng;
    private long _tick;
    private Position? _generatorSpawnHint;

    public WorldMap WorldMap => _worldMap;
    public long CurrentTick => _tick;
    public CombatSystem Combat => _combatSystem;
    public InventorySystem Inventory => _inventorySystem;
    public GameData GameData { get; }

    /// <summary>Debug: when true, player movement ignores tile collision.</summary>
    public bool DebugNoCollision { get; set; }

    /// <summary>Debug: when true, player cannot take damage.</summary>
    public bool DebugInvulnerable { get; set; }

    /// <summary>Debug: when true, player has zero move/attack delay.</summary>
    public bool DebugMaxSpeed { get; set; }

    /// <summary>Debug: when true, crafting skips ingredient and station checks.</summary>
    public bool DebugFreeCrafting { get; set; }

    /// <summary>
    /// Optional callback to deserialize raw entity JSON from saved chunks.
    /// Set by the server layer so entities are restored after the chunk is registered in WorldMap.
    /// </summary>
    public Action<string, GameEngine>? RawEntityJsonHandler { get; set; }

    public GameEngine(long worldSeed, IDungeonGenerator? generator = null)
    {
        GameData = GameData.Instance;
        _worldMap = new WorldMap(worldSeed);
        _generator = generator ?? new OverworldGenerator(worldSeed);
        _movementSystem = new MovementSystem();
        _fovSystem = new FOVSystem();
        _lightingSystem = new LightingSystem();
        _combatSystem = new CombatSystem();
        _aiSystem = new AISystem(worldSeed);
        _inventorySystem = new InventorySystem();
        _craftingSystem = new CraftingSystem();
        _buildingSystem = new BuildingSystem();
        _survivalSystem = new SurvivalSystem();
        _activeEffectsSystem = new ActiveEffectsSystem();
        _farmingSystem = new FarmingSystem();
        _animalSystem = new AnimalSystem();
        _worldRng = new SeededRandom(worldSeed);
    }

    public Chunk? EnsureChunkLoadedOrDoesntExist(ChunkPosition chunkPos)
    {
        if (!_worldMap.ExistsChunk(chunkPos, _generator))
            return null;
        return EnsureChunkLoaded(chunkPos);
    }

    /// <summary>
    /// Ensures the chunk at the given chunk coords is loaded/generated.
    /// Spawns entities from generation results if newly created.
    /// </summary>
    public Chunk EnsureChunkLoaded(ChunkPosition chunkPos)
    {
        var (chunk, genResult) = _worldMap.GetOrCreateChunk(chunkPos, _generator);

        if (genResult != null)
            ProcessGenerationResult(genResult);

        return chunk;
    }

    private void ProcessGenerationResult(GenerationResult result)
    {
        if (result.SpawnPosition.HasValue && result.Chunk.ChunkPosition.X == 0 && result.Chunk.ChunkPosition.Y == 0)
            _generatorSpawnHint = result.SpawnPosition;

        // Deserialize saved entities first (from persistence layer).
        // This runs after the chunk is registered in WorldMap, so Spawn* can find it.
        if (result.RawEntityJson != null)
            RawEntityJsonHandler?.Invoke(result.RawEntityJson, this);

        foreach (var (pos, monster) in result.Monsters)
            SpawnMonster(pos, monster);

        foreach (var (pos, item) in result.Items)
            SpawnItemOnGround(item, pos);

        foreach (var (pos, nodeDef) in result.ResourceNodes)
            SpawnResourceNode(pos, nodeDef);

        foreach (var (pos, animalDef) in result.Animals)
            SpawnAnimal(pos, animalDef);

        foreach (var (pos, itemSeed, growth, watered) in result.Crops)
        {
            if (itemSeed.Seed == null) continue; // Invalid seed, skip
            ref var crop = ref SpawnCrop(pos, itemSeed);
            crop.CropData.GrowthTicksCurrent = growth;
            crop.CropData.IsWatered = watered;
            crop.Appearance = FarmingSystem.GetCropAppearance(crop.CropData.GetGrowthStage(itemSeed.Seed));
        }

        foreach (var (pos, name, tcx, tcy, radius) in result.TownNpcs)
            SpawnTownNpc(pos, name, tcx, tcy, radius);
    }

    // ── Entity creation ──────────────────────────────────────────────

    /// <summary>
    /// Spawns a player entity at the given world position.
    /// Class choice affects starting stats.
    /// </summary>
    public ref PlayerEntity SpawnPlayer(long connectionId, Position pos, int classId)
    {
        var classStats = ClassDefinitions.GetStartingStats(classId);
        var stats = classStats + ClassDefinitions.BaseStats;
        var moveDelay = Math.Max(0, 10 - (6 + classStats.Speed));
        var attackDelay = Math.Max(0, 10 - (6 + classStats.Speed));

        var player = new PlayerEntity(_worldMap.AllocateEntityId())
        {
            ConnectionId = connectionId,
            Position = pos,
            Health = new Health(stats.Health),
            CombatStats = new CombatStats(stats.Attack, stats.Defense, stats.Speed),
            FOV = new FOVData(ClassDefinitions.FOVRadius),
            Appearance = new TileAppearance(RenderConstants.GlyphPlayer, RenderConstants.ColorWhite),
            Input = new PlayerInput(),
            ClassData = new ClassData { ClassId = classId, Level = 1 },
            Inventory = new Inventory(ClassDefinitions.InventorySlots),
            Equipment = new Equipment(),
            QuickSlots = new QuickSlots(),
            MoveDelay = new MoveDelay(moveDelay),
            AttackDelay = new AttackDelay(attackDelay),
            Survival = Components.Survival.Default(),
        };
        return ref _worldMap.AddPlayer(player);
    }

    /// <summary>
    /// Gives the player 9999 of each resource type. Used for debug mode.
    /// </summary>
    public void GiveDebugResources(ref PlayerEntity player)
    {
        ReadOnlySpan<string> resourceIds = ["wood", "copper_ore", "iron_ore", "gold_ore"];
        foreach (string resId in resourceIds)
        {
            player.Inventory.Items.Add(new ItemData
            {
                ItemTypeId = GameData.Instance.Items.GetNumericId(resId),
                StackCount = 9999,
            });
        }
    }

    /// <summary>
    /// Spawns a monster at the given position using fully-populated MonsterData.
    /// </summary>
    public ref MonsterEntity SpawnMonster(Position pos, MonsterData data)
    {
        var def = GameData.Instance.Npcs.Get(data.MonsterTypeId);
        int moveInterval = Math.Max(0, 10 - data.Speed);
        var monster = new MonsterEntity(_worldMap.AllocateEntityId())
        {
            Position = pos,
            MonsterData = data,
            Health = new Health(data.Health),
            CombatStats = new CombatStats(data.Attack, data.Defense, data.Speed),
            Appearance = new TileAppearance(def?.GlyphId ?? 0, def?.FgColor ?? 0),
            AI = new AIState { StateId = AIStates.Idle },
            MoveDelay = new MoveDelay(moveInterval),
            AttackDelay = new AttackDelay(moveInterval),
        };

        return ref _worldMap.GetChunk(Chunk.WorldToChunkCoord(pos)).AddEntity(monster);
    }

    /// <summary>
    /// Creates an item entity lying on the ground.
    /// </summary>
    public ref GroundItemEntity SpawnItemOnGround(Data.ItemDefinition def, Position pos)
    {
        var itemData = LootGenerator.GenerateItemData(def, _worldRng);
        return ref SpawnItemOnGround(itemData, pos);
    }

    /// <summary>
    /// Creates an item entity on the ground from pre-built ItemData.
    /// </summary>
    public ref GroundItemEntity SpawnItemOnGround(ItemData itemData, Position pos)
    {
        var def = GameData.Instance.Items.Get(itemData.ItemTypeId);
        var item = new GroundItemEntity(_worldMap.AllocateEntityId())
        {
            Position = pos,
            Appearance = new TileAppearance(def?.GlyphId ?? 0, def?.FgColor ?? 0),
            Item = itemData,
        };

        var c = Chunk.WorldToChunkCoord(pos);
        return ref _worldMap.GetChunk(c).AddEntity(item);
    }

    /// <summary>
    /// Spawns a resource node (tree, ore rock) that can be mined.
    /// </summary>
    public ref ResourceNodeEntity SpawnResourceNode(Position pos, Data.ResourceNodeDefinition def)
    {
        int resItemId = GameData.Instance.Items.GetNumericId(def.DropItemId);

        var node = new ResourceNodeEntity(_worldMap.AllocateEntityId())
        {
            Position = pos,
            Health = new Health(def.Health),
            CombatStats = new CombatStats(0, def.Defense, 0),
            Appearance = new TileAppearance(def.GlyphId, def.FgColor),
            NodeData = new ResourceNodeData
            {
                NodeTypeId = def.NumericId,
                ResourceItemTypeId = resItemId,
                MinDrop = def.MinDrop,
                MaxDrop = def.MaxDrop,
                RequiredToolType = def.RequiredToolType,
            },
            AttackDelay = new AttackDelay(0),
        };

        var c = Chunk.WorldToChunkCoord(pos);
        return ref _worldMap.GetChunk(c).AddEntity(node);
    }

    /// <summary>
    /// Spawns a peaceful town NPC that wanders within a radius.
    /// </summary>
    public ref TownNpcEntity SpawnTownNpc(Position pos, string name, int townCenterX, int townCenterY, int wanderRadius)
    {
        var npc = new TownNpcEntity(_worldMap.AllocateEntityId())
        {
            Position = pos,
            Health = new Health(9999),
            CombatStats = new CombatStats(0, 999, 3),
            Appearance = new TileAppearance(RenderConstants.GlyphTownNpc, RenderConstants.ColorTownNpcFg),
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

        // Stagger movement so not all NPCs move on the same tick
        npc.MoveDelay.Current = _worldRng.Next(npc.MoveDelay.Interval);

        var c = Chunk.WorldToChunkCoord(pos);
        return ref _worldMap.GetChunk(c).AddEntity(npc);
    }

    /// <summary>
    /// Spawns a farm animal at the given position using an animal definition.
    /// </summary>
    public ref AnimalEntity SpawnAnimal(Position pos, Data.AnimalDefinition def)
    {
        var animal = new AnimalEntity(_worldMap.AllocateEntityId())
        {
            Position = pos,
            Health = new Health(def.Health),
            Appearance = new TileAppearance(def.GlyphId, def.FgColor),
            AnimalData = new AnimalData
            {
                AnimalTypeId = def.NumericId,
                ProduceTicksCurrent = 0,
                IsFed = false,
                FedTicksRemaining = 0,
                BreedCooldownCurrent = def.BreedCooldownTicks,
            },
            AI = new AIState { StateId = AIStates.Idle },
            MoveDelay = new MoveDelay(50),
        };

        // Stagger movement so not all animals move on the same tick
        animal.MoveDelay.Current = _worldRng.Next(animal.MoveDelay.Interval);

        var c = Chunk.WorldToChunkCoord(pos);
        return ref _worldMap.GetChunk(c).AddEntity(animal);
    }

    /// <summary>
    /// Spawns a crop at the given position using a seed item type ID and optional growth state.
    /// </summary>
    public ref CropEntity SpawnCrop(Position pos, ItemDefinition itemDefinition)
    {
        if (itemDefinition.Seed == null)
            throw new ArgumentException($"Item type {itemDefinition.NumericId} is not a valid seed.");

        var crop = new CropEntity(_worldMap.AllocateEntityId())
        {
            Position = pos,
            Appearance = FarmingSystem.GetCropAppearance(0),
            CropData = new CropData
            {
                SeedItemTypeId = itemDefinition.NumericId,
                GrowthTicksCurrent = 0,
                IsWatered = false,
            },
        };

        var ck = Chunk.WorldToChunkCoord(pos);
        return ref _worldMap.GetChunk(ck).AddEntity(crop);
    }

    // ── Tick ──────────────────────────────────────────────────────────

    /// <summary>
    /// Runs one game tick: process inputs -> move -> combat -> AI -> inventory -> skills -> FOV -> lighting.
    /// </summary>
    public void Tick()
    {
        foreach (ref var player in _worldMap.Players)
            player.ActionEvents.Clear();

        _survivalSystem.Update(_worldMap, DebugInvulnerable);
        _activeEffectsSystem.Update(_worldMap);
        _movementSystem.Update(_worldMap, DebugNoCollision, DebugMaxSpeed);
        _combatSystem.Update(_worldMap, DebugInvulnerable);
        _aiSystem.Update(_worldMap);
        _inventorySystem.Update(_worldMap, this);
        _craftingSystem.Update(_worldMap, DebugFreeCrafting);
        _buildingSystem.Update(_worldMap);
        _farmingSystem.Update(_worldMap);
        _animalSystem.Update(_worldMap);
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
        var deadMonsters = new List<(Position pos, int MonsterTypeId)>();
        foreach (var chunk in _worldMap.LoadedChunks)
            foreach (var m in chunk.Monsters)
                if (m.IsDead)
                    deadMonsters.Add((m.Position, m.MonsterData.MonsterTypeId));

        foreach (var (pos, typeId) in deadMonsters)
        {
            // Award XP to closest player
            var npcDef = GameData.Instance.Npcs.Get(typeId);
            if (npcDef != null && npcDef.XpReward > 0)
            {
                ref var closest = ref FindClosestPlayer(pos);
                if (!Unsafe.IsNullRef(ref closest))
                {
                    closest.ClassData.Experience += npcDef.XpReward;
                    ProcessLevelUp(ref closest);
                }
            }

            if (_worldRng.Next(100) < 60)
            {
                int difficulty = typeId;
                var loot = LootGenerator.GenerateLoot(_worldRng, difficulty);
                var drop = FindDropPosition(pos);
                SpawnItemOnGround(loot.Definition, drop);
            }
        }

        var deadNodes = new List<(Position, ResourceNodeData Data)>();
        foreach (var chunk in _worldMap.LoadedChunks)
            foreach (var r in chunk.ResourceNodes)
                if (r.IsDead)
                    deadNodes.Add((r.Position, r.NodeData));

        foreach (var (pos, node) in deadNodes)
        {
            int dropCount = node.MinDrop + _worldRng.Next(Math.Max(1, node.MaxDrop - node.MinDrop + 1));
            var drop = FindDropPosition(pos);
            SpawnItemOnGround(new ItemData
            {
                ItemTypeId = node.ResourceItemTypeId,
                StackCount = dropCount,
            }, drop);
        }
    }

    /// <summary>
    /// Finds an unoccupied position to drop an item, spiraling outward from origin.
    /// Avoids overlapping with other ground items.
    /// </summary>
    public Position FindDropPosition(Position origin)
    {
        foreach (var p in PointsAtDistance.GetPoints(5))
        {
            var pos = Position.FromCoords(origin.X + p.X, origin.Y + p.Y, origin.Z);
            var tile = _worldMap.GetTile(pos);
            var entities = _worldMap.GetAllEntityRefsAt(pos);
            if (tile.IsWalkable &&
                !tile.HasPlaceable &&
                !entities.Any(e => e.Type == EntityType.GroundItem || e.Type == EntityType.ResourceNode))
            {
                return pos;
            }
        }

        return origin;
    }

    /// <summary>
    /// Handle player death: respawn at chunk origin with restored health.
    /// </summary>
    private void ProcessPlayerDeath()
    {
        foreach (ref var player in _worldMap.Players)
        {
            if (!player.IsDead) continue;

            var (sx, sy, sz) = FindSpawnPosition();
            player.Health.Current = player.Health.Max / 2;
            player.Position.X = sx;
            player.Position.Y = sy;
            player.Position.Z = sz;
            player.ClassData.Experience = Math.Max(0, player.ClassData.Experience - player.ClassData.Experience / 4);
        }
    }

    private void CleanupDead()
    {
        foreach (var chunk in _worldMap.LoadedChunks)
            chunk.RemoveDeadOrDestroyedEntities();
    }

    private ref PlayerEntity FindClosestPlayer(Position pos)
    {
        int bestDist = int.MaxValue;
        ref PlayerEntity best = ref Unsafe.NullRef<PlayerEntity>();
        foreach (ref var player in _worldMap.Players)
        {
            if (player.IsDead) continue;
            int dist = Math.Abs(player.Position.X - pos.X) + Math.Abs(player.Position.Y - pos.Y);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = ref player;
            }
        }
        return ref best;
    }

    private void ProcessLevelUp(ref PlayerEntity player)
    {
        var levelTable = GameData.Instance.PlayerLevels;
        int newLevel = levelTable.GetLevelForXp(player.ClassData.Experience);
        if (newLevel <= player.ClassData.Level) return;

        int oldLevel = player.ClassData.Level;
        player.ClassData.Level = newLevel;

        // Apply stat bonuses from level-up
        var oldBonus = ClassDefinitions.GetLevelBonuses(player.ClassData.ClassId, oldLevel);
        var newBonus = ClassDefinitions.GetLevelBonuses(player.ClassData.ClassId, newLevel);

        int deltaAttack = newBonus.Attack - oldBonus.Attack;
        int deltaDefense = newBonus.Defense - oldBonus.Defense;
        int deltaHealth = newBonus.Health - oldBonus.Health;
        int deltaSpeed = newBonus.Speed - oldBonus.Speed;

        player.Health.Max += deltaHealth;
        player.Health.Current = player.Health.Max; // Full heal on level up
        player.CombatStats.Attack += deltaAttack;
        player.CombatStats.Defense += deltaDefense;
        player.CombatStats.Speed += deltaSpeed;

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.LevelUp,
            OldLevel = oldLevel,
            NewLevel = newLevel,
        });
    }

    /// <summary>
    /// Finds a suitable spawn position for the player.
    /// Uses the generator's suggested spawn point when available, otherwise
    /// falls back to a floor-scan of chunk (0,0).
    /// </summary>
    public Position FindSpawnPosition()
    {
        var chunk = EnsureChunkLoaded(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        if (_generatorSpawnHint.HasValue)
        {
            var (hx, hy) = (_generatorSpawnHint.Value.X, _generatorSpawnHint.Value.Y);
            int lx = hx - 0 * Chunk.Size;
            int ly = hy - 0 * Chunk.Size;
            if (lx >= 0 && lx < Chunk.Size && ly >= 0 && ly < Chunk.Size
                && chunk.Tiles[lx, ly].IsWalkable)
            {
                return Position.FromCoords(hx, hy, Position.DefaultZ);
            }
        }

        // Collect enemy positions to enforce safety radius
        var enemyPositions = new List<(int X, int Y)>();
        foreach (var m in chunk.Monsters)
            if (!m.IsDead) enemyPositions.Add((m.Position.X, m.Position.Y));

        var occupied = new HashSet<long>();
        foreach (var player in _worldMap.Players)
            if (!player.IsDead) occupied.Add(Position.PackCoord(player.Position.X, player.Position.Y, player.Position.Z));
        foreach (var m in chunk.Monsters)
            if (!m.IsDead) occupied.Add(Position.PackCoord(m.Position.X, m.Position.Y, m.Position.Z));
        foreach (var n in chunk.TownNpcs)
            if (!n.IsDead) occupied.Add(Position.PackCoord(n.Position.X, n.Position.Y, n.Position.Z));
        foreach (var r in chunk.ResourceNodes)
            if (!r.IsDead) occupied.Add(Position.PackCoord(r.Position.X, r.Position.Y, r.Position.Z));

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

                return Position.FromCoords(x, y, Position.DefaultZ);
            }

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                if (chunk.Tiles[x, y].Type == TileType.Floor && !occupied.Contains(Position.PackCoord(x, y, Position.DefaultZ)))
                    return Position.FromCoords(x, y, Position.DefaultZ);
            }
        return Position.FromCoords(Chunk.Size / 2, Chunk.Size / 2, Position.DefaultZ);
    }

    /// <summary>
    /// Clears all entities within the given chunk (used before unloading).
    /// </summary>
    public void DestroyEntitiesInChunk(ChunkPosition chunkPos)
    {
        var chunk = _worldMap.TryGetChunk(chunkPos);
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
            Hunger = player.Survival.Hunger,
            MaxHunger = player.Survival.MaxHunger,
            Thirst = player.Survival.Thirst,
            MaxThirst = player.Survival.MaxThirst,
            InventoryCount = player.Inventory.Items.Count,
            InventoryCapacity = player.Inventory.Capacity,
            ClassId = player.ClassData.ClassId,
        };

        var items = new List<InventoryItemData>();
        foreach (var item in player.Inventory.Items)
        {
            var def = GameData.Instance.Items.Get(item.ItemTypeId);
            items.Add(new InventoryItemData
            {
                ItemTypeId = item.ItemTypeId,
                StackCount = item.StackCount,
                Category = def?.CategoryInt ?? 0,
            });
        }
        state.InventoryItems = items.ToArray();

        var equippedItems = new List<InventoryItemData>();
        for (int i = 0; i < Equipment.SlotCount; i++)
        {
            if (player.Equipment.HasItem(i))
            {
                var eq = player.Equipment[i];
                var eqDef = GameData.Instance.Items.Get(eq.ItemTypeId);
                equippedItems.Add(new InventoryItemData
                {
                    ItemTypeId = eq.ItemTypeId,
                    StackCount = eq.StackCount,
                    Category = eqDef?.CategoryInt ?? 0,
                    EquipSlot = i,
                });
            }
        }
        state.EquippedItems = equippedItems.ToArray();

        state.QuickSlotIndices = [player.QuickSlots.Slot0, player.QuickSlots.Slot1, player.QuickSlots.Slot2, player.QuickSlots.Slot3, player.QuickSlots.Slot4, player.QuickSlots.Slot5, player.QuickSlots.Slot6, player.QuickSlots.Slot7];

        // Scan nearby tiles for crafting stations
        var nearbyStationsTypes = new HashSet<int>();
        nearbyStationsTypes.Add((int)CraftingStationType.Hand); // Hand is always available
        foreach (var point in PointsAtDistance.GetPoints(CraftingSystem.StationRange))
        {
            var pos = Position.FromCoords(player.Position.X + point.X, player.Position.Y + point.Y, player.Position.Z);
            var tile = _worldMap.GetTile(pos);
            if (tile.PlaceableItemId != 0)
            {
                var stationType = GameData.Instance.Items.GetPlaceableCraftingStationType(tile.PlaceableItemId);
                if (stationType != null)
                    nearbyStationsTypes.Add((int)stationType.Value);
            }
        }
        state.NearbyStationsTypes = nearbyStationsTypes.OrderBy(s => s).ToArray();

        return state;
    }
}
