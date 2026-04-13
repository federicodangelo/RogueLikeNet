using System.Text.Json;
using System.Text.Json.Serialization;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Systems;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Server.Persistence;

// Source-generated JSON context for AOT-compatible serialization of entity data.
[JsonSerializable(typeof(List<Dictionary<string, object>>))]
[JsonSerializable(typeof(List<Dictionary<string, JsonElement>>))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false)]
internal partial class EntityJsonContext : JsonSerializerContext;

/// <summary>
/// Serializes/deserializes chunk entities to/from JSON for persistence.
/// Each entity is stored as a JSON object with a "Type" discriminator and component fields.
/// Uses a patch-up method to handle forward/backward compatibility.
/// </summary>
public static class EntitySerializer
{
    private const string TypeMonster = "Monster";
    private const string TypeGroundItem = "GroundItem";
    private const string TypeResourceNode = "ResourceNode";
    private const string TypeTownNpc = "TownNpc";
    private const string TypeCrop = "Crop";
    private const string TypeAnimal = "Animal";

    // ── Serialization helpers ──────────────────────────────────────────

    private static void SerializePosition(Dictionary<string, object> dict, Position pos)
    {
        dict["X"] = pos.X;
        dict["Y"] = pos.Y;
        dict["Z"] = pos.Z;
    }

    private static void SerializeHealth(Dictionary<string, object> dict, Health hp)
    {
        dict["HealthCurrent"] = hp.Current;
    }

    private static void SerializeAIState(Dictionary<string, object> dict, AIState ai)
    {
        dict["AIStateId"] = ai.StateId;
        dict["PatrolX"] = ai.PatrolX;
        dict["PatrolY"] = ai.PatrolY;
        dict["AlertCooldown"] = ai.AlertCooldown;
    }

    private static void SerializeMoveDelay(Dictionary<string, object> dict, MoveDelay move)
    {
        dict["MoveCurrent"] = move.Current;
    }

    private static void SerializeAttackDelay(Dictionary<string, object> dict, AttackDelay atk)
    {
        dict["AttackCurrent"] = atk.Current;
    }

    private static void SerializeMonsterData(Dictionary<string, object> dict, MonsterData md)
    {
        dict["MonsterTypeId"] = md.MonsterTypeId;
        dict["MonsterHealth"] = md.Health;
        dict["MonsterAttack"] = md.Attack;
        dict["MonsterDefense"] = md.Defense;
        dict["MonsterSpeed"] = md.Speed;
    }

    private static void SerializeItemData(Dictionary<string, object> dict, ItemData id)
    {
        dict["ItemTypeId"] = id.ItemTypeId;
        dict["StackCount"] = id.StackCount;
        dict["Durability"] = id.Durability;
    }

    private static void SerializeResourceNodeData(Dictionary<string, object> dict, ResourceNodeData rnd)
    {
        dict["NodeTypeId"] = rnd.NodeTypeId;
    }

    private static void SerializeTownNpcTag(Dictionary<string, object> dict, TownNpcTag npc)
    {
        dict["Name"] = npc.Name ?? "";
        dict["TownCenterX"] = npc.TownCenterX;
        dict["TownCenterY"] = npc.TownCenterY;
        dict["WanderRadius"] = npc.WanderRadius;
        dict["Role"] = npc.Role.ToString();
    }

    // ── Serialization ─────────────────────────────────────────────────

    /// <summary>
    /// Serializes all non-player, non-dead entities within the given chunk to JSON.
    /// Only runtime state is persisted; definition-derived values are rebuilt from IDs on load.
    /// </summary>
    public static string SerializeEntities(Chunk chunk)
    {
        var entities = new List<Dictionary<string, object>>();

        // Monsters
        foreach (var m in chunk.Monsters)
        {
            if (m.IsDead) continue;
            var dict = new Dictionary<string, object> { ["Type"] = TypeMonster };
            SerializePosition(dict, m.Position);
            SerializeMonsterData(dict, m.MonsterData);
            SerializeHealth(dict, m.Health);
            SerializeAIState(dict, m.AI);
            SerializeMoveDelay(dict, m.MoveDelay);
            SerializeAttackDelay(dict, m.AttackDelay);
            entities.Add(dict);
        }

        // Ground items
        foreach (var gi in chunk.GroundItems)
        {
            if (gi.IsDestroyed) continue;
            var dict = new Dictionary<string, object> { ["Type"] = TypeGroundItem };
            SerializePosition(dict, gi.Position);
            SerializeItemData(dict, gi.Item);
            entities.Add(dict);
        }

        // Resource nodes
        foreach (var r in chunk.ResourceNodes)
        {
            if (r.IsDead) continue;
            var dict = new Dictionary<string, object> { ["Type"] = TypeResourceNode };
            SerializePosition(dict, r.Position);
            SerializeResourceNodeData(dict, r.NodeData);
            SerializeHealth(dict, r.Health);
            SerializeAttackDelay(dict, r.AttackDelay);
            entities.Add(dict);
        }

        // Town NPCs
        foreach (var n in chunk.TownNpcs)
        {
            if (n.IsDead) continue;
            var dict = new Dictionary<string, object> { ["Type"] = TypeTownNpc };
            SerializePosition(dict, n.Position);
            SerializeTownNpcTag(dict, n.NpcData);
            SerializeHealth(dict, n.Health);
            SerializeAIState(dict, n.AI);
            SerializeMoveDelay(dict, n.MoveDelay);
            SerializeAttackDelay(dict, n.AttackDelay);
            entities.Add(dict);
        }

        // Crops
        foreach (var c in chunk.Crops)
        {
            if (c.IsDestroyed) continue;
            var dict = new Dictionary<string, object> { ["Type"] = TypeCrop };
            SerializePosition(dict, c.Position);
            dict["SeedItemTypeId"] = c.CropData.SeedItemTypeId;
            dict["GrowthTicksCurrent"] = c.CropData.GrowthTicksCurrent;
            dict["IsWatered"] = c.CropData.IsWatered;
            entities.Add(dict);
        }

        // Animals
        foreach (var a in chunk.Animals)
        {
            if (a.IsDead) continue;
            var dict = new Dictionary<string, object> { ["Type"] = TypeAnimal };
            SerializePosition(dict, a.Position);
            dict["AnimalTypeId"] = a.AnimalData.AnimalTypeId;
            SerializeHealth(dict, a.Health);
            dict["ProduceTicksCurrent"] = a.AnimalData.ProduceTicksCurrent;
            dict["IsFed"] = a.AnimalData.IsFed;
            dict["FedTicksRemaining"] = a.AnimalData.FedTicksRemaining;
            dict["BreedCooldownCurrent"] = a.AnimalData.BreedCooldownCurrent;
            SerializeAIState(dict, a.AI);
            SerializeMoveDelay(dict, a.MoveDelay);
            entities.Add(dict);
        }

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
                case TypeTownNpc: DeserializeTownNpc(dict, engine); break;
                case TypeCrop: DeserializeCrop(dict, engine); break;
                case TypeAnimal: DeserializeAnimal(dict, engine); break;
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
        if (GetString(dict, "Type") == TypeResourceNode && !dict.ContainsKey("NodeTypeId"))
        {
            int resourceItemTypeId = GetInt(dict, "ResourceItemTypeId");
            int nodeTypeId = 0;
            foreach (var def in GameData.Instance.ResourceNodes.All)
            {
                int resItemId = GameData.Instance.Items.GetNumericId(def.DropItemId);
                if (resItemId == resourceItemTypeId)
                {
                    nodeTypeId = def.NumericId;
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
        ref var monster = ref engine.SpawnMonster(Position.FromCoords(x, y, z), monsterData);

        // Restore runtime state
        monster.Health.Current = GetInt(dict, "HealthCurrent", monster.Health.Current);
        monster.AI.StateId = GetInt(dict, "AIStateId");
        monster.AI.PatrolX = GetInt(dict, "PatrolX");
        monster.AI.PatrolY = GetInt(dict, "PatrolY");
        monster.AI.AlertCooldown = GetInt(dict, "AlertCooldown");
        monster.MoveDelay.Current = GetInt(dict, "MoveCurrent");
        monster.AttackDelay.Current = GetInt(dict, "AttackCurrent");
    }

    private static void DeserializeGroundItem(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        var itemData = new ItemData
        {
            ItemTypeId = GetInt(dict, "ItemTypeId"),
            StackCount = GetInt(dict, "StackCount", 1),
            Durability = GetInt(dict, "Durability"),
        };

        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        engine.SpawnItemOnGround(itemData, Position.FromCoords(x, y, z));
    }

    private static void DeserializeResourceNode(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        int nodeTypeId = GetInt(dict, "NodeTypeId");
        var def = GameData.Instance.ResourceNodes.Get(nodeTypeId);
        if (def == null) return;
        ref var node = ref engine.SpawnResourceNode(Position.FromCoords(x, y, z), def);

        // Restore runtime state
        node.Health.Current = GetInt(dict, "HealthCurrent", node.Health.Current);
        node.AttackDelay.Current = GetInt(dict, "AttackCurrent");
    }

    private static void DeserializeTownNpc(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        string name = GetString(dict, "Name", "NPC");
        int tcx = GetInt(dict, "TownCenterX"), tcy = GetInt(dict, "TownCenterY"), radius = GetInt(dict, "WanderRadius");
        string role = GetString(dict, "Role", "Villager");
        var npcRole = Enum.TryParse<TownNpcRole>(role, true, out var parsed) ? parsed : TownNpcRole.Villager;

        ref var npc = ref engine.SpawnTownNpc(Position.FromCoords(x, y, z), name, tcx, tcy, radius, npcRole);

        // Restore runtime state
        npc.Health.Current = GetInt(dict, "HealthCurrent", npc.Health.Current);
        npc.AI.StateId = GetInt(dict, "AIStateId");
        npc.AI.PatrolX = GetInt(dict, "PatrolX");
        npc.AI.PatrolY = GetInt(dict, "PatrolY");
        npc.AI.AlertCooldown = GetInt(dict, "AlertCooldown");
        npc.MoveDelay.Current = GetInt(dict, "MoveCurrent");
        npc.AttackDelay.Current = GetInt(dict, "AttackCurrent");
    }

    private static void DeserializeCrop(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        var pos = Position.FromCoords(x, y, z);
        int seedItemTypeId = GetInt(dict, "SeedItemTypeId");
        int growthTicksCurrent = GetInt(dict, "GrowthTicksCurrent");
        bool isWatered = GetBool(dict, "IsWatered");
        var def = GameData.Instance.Items.Get(seedItemTypeId);
        if (def == null || def.Seed == null) return;

        ref var crop = ref engine.SpawnCrop(pos, def);

        // Restore runtime state
        crop.CropData.GrowthTicksCurrent = growthTicksCurrent;
        crop.CropData.IsWatered = isWatered;
        crop.Appearance = FarmingSystem.GetCropAppearance(crop.CropData.GetGrowthStage(def.Seed));
    }

    private static void DeserializeAnimal(Dictionary<string, JsonElement> dict, GameEngine engine)
    {
        int x = GetInt(dict, "X"), y = GetInt(dict, "Y"), z = GetInt(dict, "Z");
        int animalTypeId = GetInt(dict, "AnimalTypeId");
        var def = GameData.Instance.Animals.Get(animalTypeId);
        if (def == null) return;

        ref var animal = ref engine.SpawnAnimal(Position.FromCoords(x, y, z), def);

        // Restore runtime state
        animal.Health.Current = GetInt(dict, "HealthCurrent", animal.Health.Current);
        animal.AnimalData.ProduceTicksCurrent = GetInt(dict, "ProduceTicksCurrent");
        animal.AnimalData.IsFed = GetBool(dict, "IsFed");
        animal.AnimalData.FedTicksRemaining = GetInt(dict, "FedTicksRemaining");
        animal.AnimalData.BreedCooldownCurrent = GetInt(dict, "BreedCooldownCurrent");
        animal.AI.StateId = GetInt(dict, "AIStateId");
        animal.MoveDelay.Current = GetInt(dict, "MoveCurrent");
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

    private static bool GetBool(Dictionary<string, JsonElement> dict, string key, bool defaultValue = false)
    {
        if (dict.TryGetValue(key, out var elem))
        {
            if (elem.ValueKind == JsonValueKind.True) return true;
            if (elem.ValueKind == JsonValueKind.False) return false;
        }
        return defaultValue;
    }
}
