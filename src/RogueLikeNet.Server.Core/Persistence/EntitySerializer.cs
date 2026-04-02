using System.Text.Json;
using System.Text.Json.Serialization;
using Arch.Core;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

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

    // ── Serialization helpers ──────────────────────────────────────────

    private static void SerializePosition(Dictionary<string, object> dict, ref Position pos)
    {
        dict["X"] = pos.X;
        dict["Y"] = pos.Y;
        dict["Z"] = pos.Z;
    }

    private static void SerializeHealth(Dictionary<string, object> dict, ref Health hp)
    {
        dict["HealthCurrent"] = hp.Current;
    }

    private static void SerializeAIState(Dictionary<string, object> dict, ref AIState ai)
    {
        dict["AIStateId"] = ai.StateId;
        dict["PatrolX"] = ai.PatrolX;
        dict["PatrolY"] = ai.PatrolY;
        dict["AlertCooldown"] = ai.AlertCooldown;
    }

    private static void SerializeMoveDelay(Dictionary<string, object> dict, ref MoveDelay move)
    {
        dict["MoveCurrent"] = move.Current;
    }

    private static void SerializeAttackDelay(Dictionary<string, object> dict, ref AttackDelay atk)
    {
        dict["AttackCurrent"] = atk.Current;
    }

    private static void SerializeMonsterData(Dictionary<string, object> dict, ref MonsterData md)
    {
        dict["MonsterTypeId"] = md.MonsterTypeId;
        dict["MonsterHealth"] = md.Health;
        dict["MonsterAttack"] = md.Attack;
        dict["MonsterDefense"] = md.Defense;
        dict["MonsterSpeed"] = md.Speed;
    }

    private static void SerializeItemData(Dictionary<string, object> dict, ref ItemData id)
    {
        dict["ItemTypeId"] = id.ItemTypeId;
        dict["Rarity"] = id.Rarity;
        dict["BonusAttack"] = id.BonusAttack;
        dict["BonusDefense"] = id.BonusDefense;
        dict["BonusHealth"] = id.BonusHealth;
        dict["StackCount"] = id.StackCount;
    }

    private static void SerializeResourceNodeData(Dictionary<string, object> dict, ref ResourceNodeData rnd)
    {
        dict["NodeTypeId"] = rnd.NodeTypeId;
    }

    private static void SerializeTownNpcTag(Dictionary<string, object> dict, ref TownNpcTag npc)
    {
        dict["Name"] = npc.Name ?? "";
        dict["TownCenterX"] = npc.TownCenterX;
        dict["TownCenterY"] = npc.TownCenterY;
        dict["WanderRadius"] = npc.WanderRadius;
    }

    private static void SerializeTileAppearance(Dictionary<string, object> dict, ref TileAppearance ta)
    {
        dict["GlyphId"] = ta.GlyphId;
        dict["FgColor"] = ta.FgColor;
        dict["BgColor"] = ta.BgColor;
    }

    private static void SerializeLightSource(Dictionary<string, object> dict, ref LightSource ls)
    {
        dict["LightRadius"] = ls.Radius;
        dict["LightColor"] = ls.ColorRgb;
    }

    // ── Deserialization helpers ────────────────────────────────────────

    private static void RestoreHealth(Arch.Core.World world, Entity entity, Dictionary<string, JsonElement> dict)
    {
        ref var hp = ref world.TryGetRef<Health>(entity, out var found);
        if (!found) return;
        hp.Current = GetInt(dict, "HealthCurrent", hp.Current);
    }

    private static void RestoreAIState(Arch.Core.World world, Entity entity, Dictionary<string, JsonElement> dict)
    {
        ref var ai = ref world.TryGetRef<AIState>(entity, out var found);
        if (!found) return;
        ai.StateId = GetInt(dict, "AIStateId");
        ai.PatrolX = GetInt(dict, "PatrolX");
        ai.PatrolY = GetInt(dict, "PatrolY");
        ai.AlertCooldown = GetInt(dict, "AlertCooldown");
    }

    private static void RestoreMoveDelay(Arch.Core.World world, Entity entity, Dictionary<string, JsonElement> dict)
    {
        ref var move = ref world.TryGetRef<MoveDelay>(entity, out var found);
        if (!found) return;
        move.Current = GetInt(dict, "MoveCurrent");
    }

    private static void RestoreAttackDelay(Arch.Core.World world, Entity entity, Dictionary<string, JsonElement> dict)
    {
        ref var atk = ref world.TryGetRef<AttackDelay>(entity, out var found);
        if (!found) return;
        atk.Current = GetInt(dict, "AttackCurrent");
    }

    // ── Serialization ─────────────────────────────────────────────────

    /// <summary>
    /// Serializes all non-player, non-dead entities within the given chunk bounds to JSON.
    /// Only runtime state is persisted; definition-derived values are rebuilt from IDs on load.
    /// </summary>
    public static string SerializeEntities(Arch.Core.World ecsWorld, int chunkX, int chunkY, int chunkZ)
    {
        int minX = chunkX * Core.World.Chunk.Size;
        int maxX = minX + Core.World.Chunk.Size - 1;
        int minY = chunkY * Core.World.Chunk.Size;
        int maxY = minY + Core.World.Chunk.Size - 1;

        var entities = new List<Dictionary<string, object>>();

        // Monsters
        var monsterQuery = new QueryDescription().WithAll<Position, MonsterData, Health, AIState, MoveDelay, AttackDelay>().WithNone<PlayerTag, DeadTag>();
        ecsWorld.Query(in monsterQuery, (Entity entity, ref Position pos, ref MonsterData md, ref Health hp, ref AIState ai, ref MoveDelay move, ref AttackDelay atk) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            var dict = new Dictionary<string, object> { ["Type"] = TypeMonster };
            SerializePosition(dict, ref pos);
            SerializeMonsterData(dict, ref md);
            SerializeHealth(dict, ref hp);
            SerializeAIState(dict, ref ai);
            SerializeMoveDelay(dict, ref move);
            SerializeAttackDelay(dict, ref atk);
            entities.Add(dict);
        });

        // Ground items (entities with ItemData but NOT in inventory — they have Position)
        var itemQuery = new QueryDescription().WithAll<Position, ItemData>().WithNone<PlayerTag, DeadTag, MonsterData, ResourceNodeData>();
        ecsWorld.Query(in itemQuery, (Entity entity, ref Position pos, ref ItemData id) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            var dict = new Dictionary<string, object> { ["Type"] = TypeGroundItem };
            SerializePosition(dict, ref pos);
            SerializeItemData(dict, ref id);
            entities.Add(dict);
        });

        // Resource nodes
        var nodeQuery = new QueryDescription().WithAll<Position, ResourceNodeData, Health, AttackDelay>().WithNone<PlayerTag, DeadTag>();
        ecsWorld.Query(in nodeQuery, (Entity entity, ref Position pos, ref ResourceNodeData rnd, ref Health hp, ref AttackDelay atk) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            var dict = new Dictionary<string, object> { ["Type"] = TypeResourceNode };
            SerializePosition(dict, ref pos);
            SerializeResourceNodeData(dict, ref rnd);
            SerializeHealth(dict, ref hp);
            SerializeAttackDelay(dict, ref atk);
            entities.Add(dict);
        });

        // Elements (decorations with optional light)
        var elemQueryWithLight = new QueryDescription().WithAll<Position, TileAppearance, LightSource>().WithNone<PlayerTag, DeadTag, MonsterData, ItemData, ResourceNodeData, Health, TownNpcTag>();
        ecsWorld.Query(in elemQueryWithLight, (Entity entity, ref Position pos, ref TileAppearance ta, ref LightSource ls) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            var dict = new Dictionary<string, object> { ["Type"] = TypeElement };
            SerializePosition(dict, ref pos);
            SerializeTileAppearance(dict, ref ta);
            SerializeLightSource(dict, ref ls);
            entities.Add(dict);
        });

        var elemQuery = new QueryDescription().WithAll<Position, TileAppearance>().WithNone<PlayerTag, DeadTag, MonsterData, ItemData, ResourceNodeData, Health, LightSource, TownNpcTag>();
        ecsWorld.Query(in elemQuery, (Entity entity, ref Position pos, ref TileAppearance ta) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            var dict = new Dictionary<string, object> { ["Type"] = TypeElement };
            SerializePosition(dict, ref pos);
            SerializeTileAppearance(dict, ref ta);
            entities.Add(dict);
        });

        // Town NPCs
        var npcQuery = new QueryDescription().WithAll<Position, TownNpcTag, Health, AIState, MoveDelay, AttackDelay>().WithNone<DeadTag>();
        ecsWorld.Query(in npcQuery, (Entity entity, ref Position pos, ref TownNpcTag npc, ref Health hp, ref AIState ai, ref MoveDelay move, ref AttackDelay atk) =>
        {
            if (pos.X < minX || pos.X > maxX || pos.Y < minY || pos.Y > maxY || pos.Z != chunkZ) return;
            var dict = new Dictionary<string, object> { ["Type"] = TypeTownNpc };
            SerializePosition(dict, ref pos);
            SerializeTownNpcTag(dict, ref npc);
            SerializeHealth(dict, ref hp);
            SerializeAIState(dict, ref ai);
            SerializeMoveDelay(dict, ref move);
            SerializeAttackDelay(dict, ref atk);
            entities.Add(dict);
        });

        return JsonSerializer.Serialize(entities, EntityJsonContext.Default.ListDictionaryStringObject);
    }

    // ── Deserialization ───────────────────────────────────────────────

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
        // v1→v2: Resource nodes now use NodeTypeId instead of inline definition values.
        // Reverse-lookup NodeTypeId from ResourceItemTypeId for old saves.
        if (GetString(dict, "Type") == TypeResourceNode && !dict.ContainsKey("NodeTypeId"))
        {
            int resourceItemTypeId = GetInt(dict, "ResourceItemTypeId");
            int nodeTypeId = 0;
            foreach (var def in ResourceNodeDefinitions.All)
            {
                if (def.ResourceItemTypeId == resourceItemTypeId)
                {
                    nodeTypeId = def.NodeTypeId;
                    break;
                }
            }
            dict["NodeTypeId"] = JsonDocument.Parse(nodeTypeId.ToString()).RootElement;
        }
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

        RestoreHealth(engine.EcsWorld, entity, dict);
        RestoreAIState(engine.EcsWorld, entity, dict);
        RestoreMoveDelay(engine.EcsWorld, entity, dict);
        RestoreAttackDelay(engine.EcsWorld, entity, dict);
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
        int nodeTypeId = GetInt(dict, "NodeTypeId");
        var def = ResourceNodeDefinitions.Get(nodeTypeId);
        var entity = engine.SpawnResourceNode(x, y, z, def);

        RestoreHealth(engine.EcsWorld, entity, dict);
        RestoreAttackDelay(engine.EcsWorld, entity, dict);
    }

    private static void DeserializeElement(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        var pos = new Position(x, y, z);
        var ta = new TileAppearance(GetInt(dict, "GlyphId"), GetInt(dict, "FgColor"), GetInt(dict, "BgColor"));

        LightSource? light = dict.ContainsKey("LightRadius")
            ? new LightSource(GetInt(dict, "LightRadius"), GetInt(dict, "LightColor", 0xFFCC66))
            : null;
        engine.SpawnElement(new DungeonElement(pos, ta, light));
    }

    private static void DeserializeTownNpc(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        string name = GetString(dict, "Name", "NPC");
        int tcx = GetInt(dict, "TownCenterX"), tcy = GetInt(dict, "TownCenterY"), radius = GetInt(dict, "WanderRadius");

        var entity = engine.SpawnTownNpc(x, y, z, name, tcx, tcy, radius);

        RestoreHealth(engine.EcsWorld, entity, dict);
        RestoreAIState(engine.EcsWorld, entity, dict);
        RestoreMoveDelay(engine.EcsWorld, entity, dict);
        RestoreAttackDelay(engine.EcsWorld, entity, dict);
    }

    // ── JSON helpers ──────────────────────────────────────────────────

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
