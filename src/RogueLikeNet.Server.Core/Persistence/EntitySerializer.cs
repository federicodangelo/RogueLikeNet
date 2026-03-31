using System.Text.Json;
using System.Text.Json.Serialization;
using Arch.Core;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Server.Persistence;

// Source-generated JSON context for AOT-compatible serialization of entity data.
[JsonSerializable(typeof(List<Dictionary<string, object>>))]
[JsonSerializable(typeof(List<Dictionary<string, JsonElement>>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false)]
internal partial class EntityJsonContext : JsonSerializerContext;

/// <summary>
/// Serializes/deserializes chunk entities to/from JSON for persistence.
/// Each entity is stored as a JSON object with a "Type" discriminator and component fields.
/// Uses a patch-up method to handle forward/backward compatibility.
/// </summary>
public static class EntitySerializer
{
    private static readonly JsonSerializerOptions JsonOptions = EntityJsonContext.Default.Options;

    private const string TypeMonster = "Monster";
    private const string TypeGroundItem = "GroundItem";
    private const string TypeResourceNode = "ResourceNode";
    private const string TypeElement = "Element";
    private const string TypeTownNpc = "TownNpc";

    /// <summary>
    /// Serializes all non-player, non-dead entities within the given chunk bounds to JSON.
    /// </summary>
    public static string SerializeEntities(Arch.Core.World ecsWorld, int chunkX, int chunkY, int chunkZ)
    {
        int minX = chunkX * Core.World.Chunk.Size;
        int maxX = minX + Core.World.Chunk.Size - 1;
        int minY = chunkY * Core.World.Chunk.Size;
        int maxY = minY + Core.World.Chunk.Size - 1;

        var entities = new List<Dictionary<string, object>>();

        // Monsters
        var monsterQuery = new QueryDescription().WithAll<Position, MonsterData, Health, CombatStats, AIState, MoveDelay, AttackDelay, TileAppearance>().WithNone<PlayerTag, DeadTag>();
        ecsWorld.Query(in monsterQuery, (Entity entity, ref Position pos, ref MonsterData md, ref Health hp, ref CombatStats cs, ref AIState ai, ref MoveDelay move, ref AttackDelay atk, ref TileAppearance ta) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            entities.Add(new Dictionary<string, object>
            {
                ["Type"] = TypeMonster,
                ["X"] = pos.X,
                ["Y"] = pos.Y,
                ["Z"] = pos.Z,
                ["MonsterTypeId"] = md.MonsterTypeId,
                ["MonsterHealth"] = md.Health,
                ["MonsterAttack"] = md.Attack,
                ["MonsterDefense"] = md.Defense,
                ["MonsterSpeed"] = md.Speed,
                ["HealthCurrent"] = hp.Current,
                ["HealthMax"] = hp.Max,
                ["Attack"] = cs.Attack,
                ["Defense"] = cs.Defense,
                ["Speed"] = cs.Speed,
                ["AIStateId"] = ai.StateId,
                ["PatrolX"] = ai.PatrolX,
                ["PatrolY"] = ai.PatrolY,
                ["AlertCooldown"] = ai.AlertCooldown,
                ["MoveInterval"] = move.Interval,
                ["MoveCurrent"] = move.Current,
                ["AttackInterval"] = atk.Interval,
                ["AttackCurrent"] = atk.Current,
                ["GlyphId"] = ta.GlyphId,
                ["FgColor"] = ta.FgColor,
                ["BgColor"] = ta.BgColor,
            });
        });

        // Ground items (entities with ItemData but NOT in inventory — they have Position + TileAppearance)
        var itemQuery = new QueryDescription().WithAll<Position, ItemData, TileAppearance>().WithNone<PlayerTag, DeadTag, MonsterData, ResourceNodeData>();
        ecsWorld.Query(in itemQuery, (Entity entity, ref Position pos, ref ItemData id, ref TileAppearance ta) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            entities.Add(new Dictionary<string, object>
            {
                ["Type"] = TypeGroundItem,
                ["X"] = pos.X,
                ["Y"] = pos.Y,
                ["Z"] = pos.Z,
                ["ItemTypeId"] = id.ItemTypeId,
                ["Rarity"] = id.Rarity,
                ["BonusAttack"] = id.BonusAttack,
                ["BonusDefense"] = id.BonusDefense,
                ["BonusHealth"] = id.BonusHealth,
                ["StackCount"] = id.StackCount,
                ["GlyphId"] = ta.GlyphId,
                ["FgColor"] = ta.FgColor,
                ["BgColor"] = ta.BgColor,
            });
        });

        // Resource nodes
        var nodeQuery = new QueryDescription().WithAll<Position, ResourceNodeData, Health, CombatStats, TileAppearance, AttackDelay>().WithNone<PlayerTag, DeadTag>();
        ecsWorld.Query(in nodeQuery, (Entity entity, ref Position pos, ref ResourceNodeData rnd, ref Health hp, ref CombatStats cs, ref TileAppearance ta, ref AttackDelay atk) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            entities.Add(new Dictionary<string, object>
            {
                ["Type"] = TypeResourceNode,
                ["X"] = pos.X,
                ["Y"] = pos.Y,
                ["Z"] = pos.Z,
                ["ResourceItemTypeId"] = rnd.ResourceItemTypeId,
                ["MinDrop"] = rnd.MinDrop,
                ["MaxDrop"] = rnd.MaxDrop,
                ["HealthCurrent"] = hp.Current,
                ["HealthMax"] = hp.Max,
                ["Attack"] = cs.Attack,
                ["Defense"] = cs.Defense,
                ["Speed"] = cs.Speed,
                ["GlyphId"] = ta.GlyphId,
                ["FgColor"] = ta.FgColor,
                ["BgColor"] = ta.BgColor,
                ["AttackInterval"] = atk.Interval,
                ["AttackCurrent"] = atk.Current,
            });
        });

        // Elements (decorations with optional light)
        var elemQueryWithLight = new QueryDescription().WithAll<Position, TileAppearance, LightSource>().WithNone<PlayerTag, DeadTag, MonsterData, ItemData, ResourceNodeData, Health, TownNpcTag>();
        ecsWorld.Query(in elemQueryWithLight, (Entity entity, ref Position pos, ref TileAppearance ta, ref LightSource ls) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            entities.Add(new Dictionary<string, object>
            {
                ["Type"] = TypeElement,
                ["X"] = pos.X,
                ["Y"] = pos.Y,
                ["Z"] = pos.Z,
                ["GlyphId"] = ta.GlyphId,
                ["FgColor"] = ta.FgColor,
                ["BgColor"] = ta.BgColor,
                ["LightRadius"] = ls.Radius,
                ["LightColor"] = ls.ColorRgb,
            });
        });

        var elemQuery = new QueryDescription().WithAll<Position, TileAppearance>().WithNone<PlayerTag, DeadTag, MonsterData, ItemData, ResourceNodeData, Health, LightSource, TownNpcTag>();
        ecsWorld.Query(in elemQuery, (Entity entity, ref Position pos, ref TileAppearance ta) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            entities.Add(new Dictionary<string, object>
            {
                ["Type"] = TypeElement,
                ["X"] = pos.X,
                ["Y"] = pos.Y,
                ["Z"] = pos.Z,
                ["GlyphId"] = ta.GlyphId,
                ["FgColor"] = ta.FgColor,
                ["BgColor"] = ta.BgColor,
            });
        });

        // Town NPCs
        var npcQuery = new QueryDescription().WithAll<Position, TownNpcTag, Health, TileAppearance, AIState, MoveDelay, AttackDelay>().WithNone<DeadTag>();
        ecsWorld.Query(in npcQuery, (Entity entity, ref Position pos, ref TownNpcTag npc, ref Health hp, ref TileAppearance ta, ref AIState ai, ref MoveDelay move, ref AttackDelay atk) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            entities.Add(new Dictionary<string, object>
            {
                ["Type"] = TypeTownNpc,
                ["X"] = pos.X,
                ["Y"] = pos.Y,
                ["Z"] = pos.Z,
                ["Name"] = npc.Name ?? "",
                ["TownCenterX"] = npc.TownCenterX,
                ["TownCenterY"] = npc.TownCenterY,
                ["WanderRadius"] = npc.WanderRadius,
                ["HealthCurrent"] = hp.Current,
                ["HealthMax"] = hp.Max,
                ["GlyphId"] = ta.GlyphId,
                ["FgColor"] = ta.FgColor,
                ["BgColor"] = ta.BgColor,
                ["AIStateId"] = ai.StateId,
                ["MoveInterval"] = move.Interval,
                ["MoveCurrent"] = move.Current,
                ["AttackInterval"] = atk.Interval,
                ["AttackCurrent"] = atk.Current,
            });
        });

        return JsonSerializer.Serialize(entities, EntityJsonContext.Default.ListDictionaryStringObject);
    }

    /// <summary>
    /// Deserializes entities from JSON and spawns them in the game engine.
    /// Applies patch-up for forward/backward compatibility.
    /// </summary>
    public static void DeserializeEntities(string json, GameEngine engine)
    {
        if (string.IsNullOrEmpty(json) || json == "[]") return;

        var entities = JsonSerializer.Deserialize(json, EntityJsonContext.Default.ListDictionaryStringJsonElement);
        if (entities == null) return;

        foreach (var dict in entities)
        {
            PatchUpEntity(dict);
            var type = GetString(dict, "Type", "");
            switch (type)
            {
                case TypeMonster: DeserializeMonster(dict, engine); break;
                case TypeGroundItem: DeserializeGroundItem(dict, engine); break;
                case TypeResourceNode: DeserializeResourceNode(dict, engine); break;
                case TypeElement: DeserializeElement(dict, engine); break;
                case TypeTownNpc: DeserializeTownNpc(dict, engine); break;
            }
        }
    }

    /// <summary>
    /// Patch-up method for handling schema changes between versions.
    /// Add migration logic here when entity fields are added, renamed, or removed.
    /// </summary>
    private static void PatchUpEntity(Dictionary<string, JsonElement> dict)
    {
        // Example future migration:
        // if (!dict.ContainsKey("NewField"))
        //     dict["NewField"] = JsonDocument.Parse("0").RootElement;
    }

    private static void DeserializeMonster(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        var monsterData = new MonsterData
        {
            MonsterTypeId = GetInt(dict, "MonsterTypeId"),
            Health = GetInt(dict, "MonsterHealth"),
            Attack = GetInt(dict, "MonsterAttack"),
            Defense = GetInt(dict, "MonsterDefense"),
            Speed = GetInt(dict, "MonsterSpeed"),
        };

        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        var entity = engine.SpawnMonster(x, y, z, monsterData);

        // Restore runtime state
        ref var hp = ref engine.EcsWorld.Get<Health>(entity);
        hp.Current = GetInt(dict, "HealthCurrent", hp.Current);
        hp.Max = GetInt(dict, "HealthMax", hp.Max);

        ref var ai = ref engine.EcsWorld.Get<AIState>(entity);
        ai.StateId = GetInt(dict, "AIStateId");
        ai.PatrolX = GetInt(dict, "PatrolX");
        ai.PatrolY = GetInt(dict, "PatrolY");
        ai.AlertCooldown = GetInt(dict, "AlertCooldown");

        ref var move = ref engine.EcsWorld.Get<MoveDelay>(entity);
        move.Current = GetInt(dict, "MoveCurrent");

        ref var atk = ref engine.EcsWorld.Get<AttackDelay>(entity);
        atk.Current = GetInt(dict, "AttackCurrent");
    }

    private static void DeserializeGroundItem(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        var itemData = new ItemData
        {
            ItemTypeId = GetInt(dict, "ItemTypeId"),
            Rarity = GetInt(dict, "Rarity"),
            BonusAttack = GetInt(dict, "BonusAttack"),
            BonusDefense = GetInt(dict, "BonusDefense"),
            BonusHealth = GetInt(dict, "BonusHealth"),
            StackCount = GetInt(dict, "StackCount", 1),
        };

        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        engine.SpawnItemOnGround(itemData, x, y, z);
    }

    private static void DeserializeResourceNode(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");

        var entity = engine.EcsWorld.Create(
            new Position(x, y, z),
            new Health { Current = GetInt(dict, "HealthCurrent"), Max = GetInt(dict, "HealthMax") },
            new CombatStats(GetInt(dict, "Attack"), GetInt(dict, "Defense"), GetInt(dict, "Speed")),
            new TileAppearance(GetInt(dict, "GlyphId"), GetInt(dict, "FgColor"), GetInt(dict, "BgColor")),
            new ResourceNodeData
            {
                ResourceItemTypeId = GetInt(dict, "ResourceItemTypeId"),
                MinDrop = GetInt(dict, "MinDrop"),
                MaxDrop = GetInt(dict, "MaxDrop"),
            },
            new AttackDelay { Interval = GetInt(dict, "AttackInterval"), Current = GetInt(dict, "AttackCurrent") }
        );
    }

    private static void DeserializeElement(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        var pos = new Position(x, y, z);
        var ta = new TileAppearance(GetInt(dict, "GlyphId"), GetInt(dict, "FgColor"), GetInt(dict, "BgColor"));

        if (dict.ContainsKey("LightRadius"))
        {
            var light = new LightSource(GetInt(dict, "LightRadius"), GetInt(dict, "LightColor", 0xFFCC66));
            engine.EcsWorld.Create(pos, ta, light);
        }
        else
        {
            engine.EcsWorld.Create(pos, ta);
        }
    }

    private static void DeserializeTownNpc(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");

        engine.EcsWorld.Create(
            new Position(x, y, z),
            new Health { Current = GetInt(dict, "HealthCurrent", 9999), Max = GetInt(dict, "HealthMax", 9999) },
            new CombatStats(0, 999, 3),
            new TileAppearance(GetInt(dict, "GlyphId"), GetInt(dict, "FgColor"), GetInt(dict, "BgColor")),
            new AIState { StateId = GetInt(dict, "AIStateId") },
            new MoveDelay { Interval = GetInt(dict, "MoveInterval", 5), Current = GetInt(dict, "MoveCurrent") },
            new AttackDelay { Interval = GetInt(dict, "AttackInterval"), Current = GetInt(dict, "AttackCurrent") },
            new TownNpcTag
            {
                Name = GetString(dict, "Name", "NPC"),
                TownCenterX = GetInt(dict, "TownCenterX"),
                TownCenterY = GetInt(dict, "TownCenterY"),
                WanderRadius = GetInt(dict, "WanderRadius"),
            }
        );
    }

    private static int GetInt(Dictionary<string, JsonElement> dict, string key, int defaultValue = 0)
    {
        if (dict.TryGetValue(key, out var elem) && elem.ValueKind == JsonValueKind.Number)
            return elem.GetInt32();
        return defaultValue;
    }

    private static string GetString(Dictionary<string, JsonElement> dict, string key, string defaultValue = "")
    {
        if (dict.TryGetValue(key, out var elem) && elem.ValueKind == JsonValueKind.String)
            return elem.GetString() ?? defaultValue;
        return defaultValue;
    }
}
