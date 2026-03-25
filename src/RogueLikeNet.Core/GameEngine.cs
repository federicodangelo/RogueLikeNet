using Arch.Core;
using RogueLikeNet.Core.Components;
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
        _generator = generator ?? new OverworldGenerator();
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
            ProcessSpawnPoints(chunk, genResult, chunkX, chunkY);

        return chunk;
    }

    private void ProcessSpawnPoints(Chunk chunk, GenerationResult result, int chunkX, int chunkY)
    {
        int worldOffsetX = chunkX * Chunk.Size;
        int worldOffsetY = chunkY * Chunk.Size;

        // Determine difficulty from distance to origin
        int difficulty = Math.Max(Math.Abs(chunkX), Math.Abs(chunkY));

        foreach (var sp in result.SpawnPoints)
        {
            int wx = worldOffsetX + sp.LocalX;
            int wy = worldOffsetY + sp.LocalY;

            switch (sp.Type)
            {
                case SpawnType.Monster:
                    var template = NpcDefinitions.Pick(_worldRng, difficulty);
                    // Scale stats with difficulty
                    int hpScale = 1 + difficulty / 2;
                    SpawnMonster(template.TypeId, wx, wy, template.GlyphId, template.Color,
                        template.Health * hpScale, template.Attack + difficulty,
                        template.Defense + difficulty / 2, template.Speed);
                    break;

                case SpawnType.Item:
                    var (itemDef, rarity) = ItemDefinitions.GenerateLoot(_worldRng, difficulty);
                    SpawnItemOnGround(itemDef, rarity, wx, wy);
                    break;

                case SpawnType.Torch:
                    SpawnTorch(wx, wy);
                    break;
            }
        }
    }

    /// <summary>
    /// Spawns a player entity at the given world position.
    /// Class choice affects starting stats.
    /// </summary>
    public Entity SpawnPlayer(long connectionId, int x, int y, int classId = ClassDefinitions.Warrior)
    {
        var (bonusAtk, bonusDef, bonusHp, bonusSpeed) = ClassDefinitions.GetStartingBonus(classId);
        var skills = ClassDefinitions.GetStartingSkills(classId);

        return _ecsWorld.Create(
            new Position(x, y),
            new Health(100 + bonusHp),
            new CombatStats(10 + bonusAtk, 5 + bonusDef, 10 + bonusSpeed),
            new FOVData(20),
            new TileAppearance(TileDefinitions.GlyphPlayer, TileDefinitions.ColorWhite),
            new PlayerTag { ConnectionId = connectionId },
            new PlayerInput(),
            new ClassData { ClassId = classId, Level = 1 },
            skills,
            new Inventory(20),
            new Equipment()
        );
    }

    /// <summary>
    /// Spawns a monster at the given position.
    /// </summary>
    public Entity SpawnMonster(int monsterTypeId, int x, int y, int glyphId, int color,
        int health = 20, int attack = 5, int defense = 2, int speed = 8)
    {
        // Speed maps to move delay: higher speed → lower delay.
        // Speed 10 = every tick (0 delay), speed 6 = every 3rd tick, etc.
        int moveInterval = Math.Max(0, 10 - speed);

        return _ecsWorld.Create(
            new Position(x, y),
            new Health(health),
            new CombatStats(attack, defense, speed),
            new TileAppearance(glyphId, color),
            new MonsterTag { MonsterTypeId = monsterTypeId },
            new AIState { StateId = AIStates.Idle },
            new MoveDelay(moveInterval)
        );
    }

    /// <summary>
    /// Creates an item entity lying on the ground.
    /// </summary>
    public Entity SpawnItemOnGround(ItemDefinition def, int rarity, int x, int y)
    {
        // Rarity multiplier: each tier adds 50% to base stats
        int rarityMult = 100 + rarity * 50;
        return _ecsWorld.Create(
            new Position(x, y),
            new TileAppearance(def.GlyphId, def.Color),
            new ItemData
            {
                ItemTypeId = def.TypeId,
                Rarity = rarity,
                BonusAttack = def.BaseAttack * rarityMult / 100,
                BonusDefense = def.BaseDefense * rarityMult / 100,
                BonusHealth = def.BaseHealth * rarityMult / 100,
                StackCount = def.Stackable
                    ? (def.Category == ItemDefinitions.CategoryGold ? 10 + _worldRng.Next(50) : 1)
                    : 1,
            },
            new GroundItemTag()
        );
    }

    /// <summary>
    /// Spawns a torch (light source) at the given position.
    /// </summary>
    public Entity SpawnTorch(int x, int y)
    {
        return _ecsWorld.Create(
            new Position(x, y),
            new TileAppearance(TileDefinitions.GlyphTorch, TileDefinitions.ColorTorchFg),
            new LightSource(6, TileDefinitions.ColorTorchFg)
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
        var deathQuery = new QueryDescription().WithAll<DeadTag, MonsterTag, Position>();
        _ecsWorld.Query(in deathQuery, (ref Position pos, ref MonsterTag tag) =>
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
                SpawnItemOnGround(template, rarity, x, y);
            }
        }
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

        // Collect occupied positions to avoid spawning on entities
        var occupied = new HashSet<long>();
        var posQuery = new QueryDescription().WithAll<Position>();
        _ecsWorld.Query(in posQuery, (ref Position p) =>
        {
            occupied.Add(FOVData.PackCoord(p.X, p.Y));
        });

        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
        {
            if (chunk.Tiles[x, y].Type == TileType.Floor && !occupied.Contains(FOVData.PackCoord(x, y)))
                return (x, y);
        }
        return (Chunk.Size / 2, Chunk.Size / 2);
    }

    public void Dispose()
    {
        Arch.Core.World.Destroy(_ecsWorld);
    }

    /// <summary>
    /// Returns HUD data for a player entity (health, stats, inventory, skills).
    /// </summary>
    public PlayerHudData? GetPlayerHudData(Entity playerEntity)
    {
        if (!_ecsWorld.IsAlive(playerEntity)) return null;

        ref var health = ref _ecsWorld.Get<Health>(playerEntity);
        ref var stats = ref _ecsWorld.Get<CombatStats>(playerEntity);

        var hud = new PlayerHudData
        {
            Health = health.Current,
            MaxHealth = health.Max,
            Attack = stats.Attack,
            Defense = stats.Defense,
        };

        if (_ecsWorld.Has<ClassData>(playerEntity))
        {
            ref var classData = ref _ecsWorld.Get<ClassData>(playerEntity);
            hud.Level = classData.Level;
            hud.Experience = classData.Experience;
        }

        if (_ecsWorld.Has<Inventory>(playerEntity))
        {
            ref var inv = ref _ecsWorld.Get<Inventory>(playerEntity);
            hud.InventoryCount = inv.Items?.Count ?? 0;
            hud.InventoryCapacity = inv.Capacity;

            // Build inventory item names, stack counts, rarities
            if (inv.Items != null)
            {
                var names = new List<string>();
                var stacks = new List<int>();
                var rarities = new List<int>();
                foreach (var item in inv.Items)
                {
                    var def = ItemDefinitions.Get(item.ItemTypeId);
                    names.Add(def.Name ?? "Unknown");
                    stacks.Add(item.StackCount);
                    rarities.Add(item.Rarity);
                }
                hud.InventoryNames = names.ToArray();
                hud.InventoryStackCounts = stacks.ToArray();
                hud.InventoryRarities = rarities.ToArray();
            }
        }

        if (_ecsWorld.Has<Equipment>(playerEntity))
        {
            ref var equip = ref _ecsWorld.Get<Equipment>(playerEntity);
            if (equip.HasWeapon)
                hud.EquippedWeaponName = ItemDefinitions.Get(equip.Weapon!.Value.ItemTypeId).Name ?? "";
            if (equip.HasArmor)
                hud.EquippedArmorName = ItemDefinitions.Get(equip.Armor!.Value.ItemTypeId).Name ?? "";
        }

        if (_ecsWorld.Has<SkillSlots>(playerEntity))
        {
            ref var skills = ref _ecsWorld.Get<SkillSlots>(playerEntity);
            hud.SkillIds = [skills.Skill0, skills.Skill1, skills.Skill2, skills.Skill3];
            hud.SkillCooldowns = [skills.Cooldown0, skills.Cooldown1, skills.Cooldown2, skills.Cooldown3];
            hud.SkillNames = [
                SkillDefinitions.GetName(skills.Skill0),
                SkillDefinitions.GetName(skills.Skill1),
                SkillDefinitions.GetName(skills.Skill2),
                SkillDefinitions.GetName(skills.Skill3),
            ];
        }

        // Floor items at player position
        if (_ecsWorld.Has<Position>(playerEntity))
        {
            ref var playerPos = ref _ecsWorld.Get<Position>(playerEntity);
            int px = playerPos.X, py = playerPos.Y;
            var floorNames = new List<string>();
            var floorQuery = new QueryDescription().WithAll<Position, ItemData, GroundItemTag>();
            _ecsWorld.Query(in floorQuery, (ref Position iPos, ref ItemData itemData) =>
            {
                if (iPos.X == px && iPos.Y == py)
                {
                    var def = ItemDefinitions.Get(itemData.ItemTypeId);
                    floorNames.Add(def.Name ?? "Unknown");
                }
            });
            hud.FloorItemNames = floorNames.ToArray();
        }

        return hud;
    }
}

public class PlayerHudData
{
    public int Health;
    public int MaxHealth;
    public int Attack;
    public int Defense;
    public int Level;
    public int Experience;
    public int InventoryCount;
    public int InventoryCapacity;
    public int[] SkillIds = [];
    public int[] SkillCooldowns = [];
    public string[] SkillNames = [];
    public string[] InventoryNames = [];
    public int[] InventoryStackCounts = [];
    public int[] InventoryRarities = [];
    public string[] FloorItemNames = [];
    public string EquippedWeaponName = "";
    public string EquippedArmorName = "";
}
