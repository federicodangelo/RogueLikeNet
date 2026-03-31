using System.Text.Json;
using System.Text.Json.Serialization;
using Arch.Core;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Server.Persistence;

// Source-generated JSON context for AOT-compatible serialization of player data.
[JsonSerializable(typeof(List<PlayerSerializer.ItemDataJson>))]
[JsonSerializable(typeof(PlayerSerializer.EquipmentJson))]
[JsonSerializable(typeof(PlayerSerializer.SkillSlotsJson))]
[JsonSerializable(typeof(PlayerSerializer.QuickSlotsJson))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = false)]
internal partial class PlayerJsonContext : JsonSerializerContext;

/// <summary>
/// Serializes/deserializes player state to/from PlayerSaveData for persistence.
/// Uses JSON for inventory, equipment, skills, and quickslots.
/// </summary>
public static class PlayerSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = PlayerJsonContext.Default.Options;

    /// <summary>
    /// Extracts all player state from an ECS entity into a PlayerSaveData.
    /// </summary>
    public static PlayerSaveData SerializePlayer(Arch.Core.World ecsWorld, Entity playerEntity, string playerName)
    {
        ref var pos = ref ecsWorld.Get<Position>(playerEntity);
        ref var hp = ref ecsWorld.Get<Health>(playerEntity);
        ref var stats = ref ecsWorld.Get<CombatStats>(playerEntity);
        ref var classData = ref ecsWorld.Get<ClassData>(playerEntity);

        var data = new PlayerSaveData
        {
            PlayerName = playerName,
            ClassId = classData.ClassId,
            Level = classData.Level,
            Experience = classData.Experience,
            PositionX = pos.X,
            PositionY = pos.Y,
            PositionZ = pos.Z,
            HealthCurrent = hp.Current,
            HealthMax = hp.Max,
            Attack = stats.Attack,
            Defense = stats.Defense,
            Speed = stats.Speed,
        };

        // Inventory
        if (ecsWorld.Has<Inventory>(playerEntity))
        {
            ref var inv = ref ecsWorld.Get<Inventory>(playerEntity);
            var items = inv.Items?.Select(i => new ItemDataJson
            {
                ItemTypeId = i.ItemTypeId,
                Rarity = i.Rarity,
                BonusAttack = i.BonusAttack,
                BonusDefense = i.BonusDefense,
                BonusHealth = i.BonusHealth,
                StackCount = i.StackCount,
            }).ToList() ?? [];
            data.InventoryJson = JsonSerializer.Serialize(items, PlayerJsonContext.Default.ListItemDataJson);
        }

        // Equipment
        if (ecsWorld.Has<Equipment>(playerEntity))
        {
            ref var equip = ref ecsWorld.Get<Equipment>(playerEntity);
            var equipData = new EquipmentJson
            {
                Weapon = equip.HasWeapon ? ToItemJson(equip.Weapon!.Value) : null,
                Armor = equip.HasArmor ? ToItemJson(equip.Armor!.Value) : null,
            };
            data.EquipmentJson = JsonSerializer.Serialize(equipData, PlayerJsonContext.Default.EquipmentJson);
        }

        // Skills
        if (ecsWorld.Has<SkillSlots>(playerEntity))
        {
            ref var skills = ref ecsWorld.Get<SkillSlots>(playerEntity);
            var skillData = new SkillSlotsJson
            {
                Skill0 = skills.Skill0,
                Skill1 = skills.Skill1,
                Skill2 = skills.Skill2,
                Skill3 = skills.Skill3,
                Cooldown0 = skills.Cooldown0,
                Cooldown1 = skills.Cooldown1,
                Cooldown2 = skills.Cooldown2,
                Cooldown3 = skills.Cooldown3,
            };
            data.SkillsJson = JsonSerializer.Serialize(skillData, PlayerJsonContext.Default.SkillSlotsJson);
        }

        // QuickSlots
        if (ecsWorld.Has<QuickSlots>(playerEntity))
        {
            ref var qs = ref ecsWorld.Get<QuickSlots>(playerEntity);
            var qsData = new QuickSlotsJson
            {
                Slot0 = qs.Slot0,
                Slot1 = qs.Slot1,
                Slot2 = qs.Slot2,
                Slot3 = qs.Slot3,
            };
            data.QuickSlotsJson = JsonSerializer.Serialize(qsData, PlayerJsonContext.Default.QuickSlotsJson);
        }

        return data;
    }

    /// <summary>
    /// Restores a player entity in the game engine from saved data.
    /// Returns the spawned entity.
    /// </summary>
    public static Entity RestorePlayer(GameEngine engine, long connectionId, PlayerSaveData data)
    {
        // Spawn shell player (gets default stats)
        var entity = engine.SpawnPlayer(connectionId, data.PositionX, data.PositionY, data.PositionZ, data.ClassId);

        // Override with saved state
        ref var hp = ref engine.EcsWorld.Get<Health>(entity);
        hp.Current = data.HealthCurrent;
        hp.Max = data.HealthMax;

        ref var stats = ref engine.EcsWorld.Get<CombatStats>(entity);
        stats.Attack = data.Attack;
        stats.Defense = data.Defense;
        stats.Speed = data.Speed;

        ref var classData = ref engine.EcsWorld.Get<ClassData>(entity);
        classData.Level = data.Level;
        classData.Experience = data.Experience;

        // Restore inventory
        if (!string.IsNullOrEmpty(data.InventoryJson) && data.InventoryJson != "[]")
        {
            var items = JsonSerializer.Deserialize(data.InventoryJson, PlayerJsonContext.Default.ListItemDataJson);
            if (items != null)
            {
                ref var inv = ref engine.EcsWorld.Get<Inventory>(entity);
                inv.Items ??= new();
                inv.Items.Clear();
                foreach (var item in items)
                {
                    inv.Items.Add(new ItemData
                    {
                        ItemTypeId = item.ItemTypeId,
                        Rarity = item.Rarity,
                        BonusAttack = item.BonusAttack,
                        BonusDefense = item.BonusDefense,
                        BonusHealth = item.BonusHealth,
                        StackCount = item.StackCount,
                    });
                }
            }
        }

        // Restore equipment
        if (!string.IsNullOrEmpty(data.EquipmentJson) && data.EquipmentJson != "{}")
        {
            var equipData = JsonSerializer.Deserialize(data.EquipmentJson, PlayerJsonContext.Default.EquipmentJson);
            if (equipData != null)
            {
                ref var equip = ref engine.EcsWorld.Get<Equipment>(entity);
                equip.Weapon = equipData.Weapon != null ? FromItemJson(equipData.Weapon) : null;
                equip.Armor = equipData.Armor != null ? FromItemJson(equipData.Armor) : null;
            }
        }

        // Restore skills
        if (!string.IsNullOrEmpty(data.SkillsJson) && data.SkillsJson != "{}")
        {
            var skillData = JsonSerializer.Deserialize(data.SkillsJson, PlayerJsonContext.Default.SkillSlotsJson);
            if (skillData != null)
            {
                ref var skills = ref engine.EcsWorld.Get<SkillSlots>(entity);
                skills.Skill0 = skillData.Skill0;
                skills.Skill1 = skillData.Skill1;
                skills.Skill2 = skillData.Skill2;
                skills.Skill3 = skillData.Skill3;
                skills.Cooldown0 = skillData.Cooldown0;
                skills.Cooldown1 = skillData.Cooldown1;
                skills.Cooldown2 = skillData.Cooldown2;
                skills.Cooldown3 = skillData.Cooldown3;
            }
        }

        // Restore quickslots
        if (!string.IsNullOrEmpty(data.QuickSlotsJson) && data.QuickSlotsJson != "{}")
        {
            var qsData = JsonSerializer.Deserialize(data.QuickSlotsJson, PlayerJsonContext.Default.QuickSlotsJson);
            if (qsData != null)
            {
                ref var qs = ref engine.EcsWorld.Get<QuickSlots>(entity);
                qs.Slot0 = qsData.Slot0;
                qs.Slot1 = qsData.Slot1;
                qs.Slot2 = qsData.Slot2;
                qs.Slot3 = qsData.Slot3;
            }
        }

        return entity;
    }

    private static ItemDataJson ToItemJson(ItemData item) => new()
    {
        ItemTypeId = item.ItemTypeId,
        Rarity = item.Rarity,
        BonusAttack = item.BonusAttack,
        BonusDefense = item.BonusDefense,
        BonusHealth = item.BonusHealth,
        StackCount = item.StackCount,
    };

    private static ItemData FromItemJson(ItemDataJson j) => new()
    {
        ItemTypeId = j.ItemTypeId,
        Rarity = j.Rarity,
        BonusAttack = j.BonusAttack,
        BonusDefense = j.BonusDefense,
        BonusHealth = j.BonusHealth,
        StackCount = j.StackCount,
    };

    // JSON DTOs — must be public for source-generated serialization
    public class ItemDataJson
    {
        public int ItemTypeId { get; set; }
        public int Rarity { get; set; }
        public int BonusAttack { get; set; }
        public int BonusDefense { get; set; }
        public int BonusHealth { get; set; }
        public int StackCount { get; set; }
    }

    public class EquipmentJson
    {
        public ItemDataJson? Weapon { get; set; }
        public ItemDataJson? Armor { get; set; }
    }

    public class SkillSlotsJson
    {
        public int Skill0 { get; set; }
        public int Skill1 { get; set; }
        public int Skill2 { get; set; }
        public int Skill3 { get; set; }
        public int Cooldown0 { get; set; }
        public int Cooldown1 { get; set; }
        public int Cooldown2 { get; set; }
        public int Cooldown3 { get; set; }
    }

    public class QuickSlotsJson
    {
        public int Slot0 { get; set; }
        public int Slot1 { get; set; }
        public int Slot2 { get; set; }
        public int Slot3 { get; set; }
    }
}
