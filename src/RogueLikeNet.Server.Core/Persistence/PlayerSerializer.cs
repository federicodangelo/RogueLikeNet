using System.Text.Json;
using System.Text.Json.Serialization;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;

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
    /// <summary>
    /// Extracts all player state from a PlayerEntity into a PlayerSaveData.
    /// </summary>
    public static PlayerSaveData SerializePlayer(PlayerEntity player, string playerName)
    {
        var data = new PlayerSaveData
        {
            PlayerName = playerName,
            ClassId = player.ClassData.ClassId,
            Level = player.ClassData.Level,
            Experience = player.ClassData.Experience,
            PositionX = player.Position.X,
            PositionY = player.Position.Y,
            PositionZ = player.Position.Z,
            HealthCurrent = player.Health.Current,
            HealthMax = player.Health.Max,
            Attack = player.CombatStats.Attack,
            Defense = player.CombatStats.Defense,
            Speed = player.CombatStats.Speed,
        };

        // Inventory
        var items = player.Inventory.Items?.Select(i => new ItemDataJson
        {
            ItemTypeId = i.ItemTypeId,
            Rarity = i.Rarity,
            BonusAttack = i.BonusAttack,
            BonusDefense = i.BonusDefense,
            BonusHealth = i.BonusHealth,
            StackCount = i.StackCount,
        }).ToList() ?? [];
        data.InventoryJson = JsonSerializer.Serialize(items, PlayerJsonContext.Default.ListItemDataJson);

        // Equipment
        var equipData = new EquipmentJson
        {
            Weapon = player.Equipment.HasWeapon ? ToItemJson(player.Equipment.Weapon!.Value) : null,
            Armor = player.Equipment.HasArmor ? ToItemJson(player.Equipment.Armor!.Value) : null,
        };
        data.EquipmentJson = JsonSerializer.Serialize(equipData, PlayerJsonContext.Default.EquipmentJson);

        // Skills
        var skillData = new SkillSlotsJson
        {
            Skill0 = player.Skills.Skill0,
            Skill1 = player.Skills.Skill1,
            Skill2 = player.Skills.Skill2,
            Skill3 = player.Skills.Skill3,
            Cooldown0 = player.Skills.Cooldown0,
            Cooldown1 = player.Skills.Cooldown1,
            Cooldown2 = player.Skills.Cooldown2,
            Cooldown3 = player.Skills.Cooldown3,
        };
        data.SkillsJson = JsonSerializer.Serialize(skillData, PlayerJsonContext.Default.SkillSlotsJson);

        // QuickSlots
        var qsData = new QuickSlotsJson
        {
            Slot0 = player.QuickSlots.Slot0,
            Slot1 = player.QuickSlots.Slot1,
            Slot2 = player.QuickSlots.Slot2,
            Slot3 = player.QuickSlots.Slot3,
        };
        data.QuickSlotsJson = JsonSerializer.Serialize(qsData, PlayerJsonContext.Default.QuickSlotsJson);

        return data;
    }

    /// <summary>
    /// Restores a player entity in the game engine from saved data.
    /// Returns the spawned PlayerEntity.
    /// </summary>
    public static ref PlayerEntity RestorePlayer(GameEngine engine, long connectionId, PlayerSaveData data)
    {
        // Spawn shell player (gets default stats)
        ref var player = ref engine.SpawnPlayer(connectionId, data.PositionX, data.PositionY, data.PositionZ, data.ClassId);

        // Override with saved state
        player.Health.Current = data.HealthCurrent;
        player.Health.Max = data.HealthMax;

        player.CombatStats.Attack = data.Attack;
        player.CombatStats.Defense = data.Defense;
        player.CombatStats.Speed = data.Speed;

        player.ClassData.Level = data.Level;
        player.ClassData.Experience = data.Experience;

        // Restore inventory
        if (!string.IsNullOrEmpty(data.InventoryJson) && data.InventoryJson != "[]")
        {
            var items = JsonSerializer.Deserialize(data.InventoryJson, PlayerJsonContext.Default.ListItemDataJson);
            if (items != null)
            {
                player.Inventory.Items ??= new();
                player.Inventory.Items.Clear();
                foreach (var item in items)
                {
                    player.Inventory.Items.Add(new ItemData
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
                player.Equipment.Weapon = equipData.Weapon != null ? FromItemJson(equipData.Weapon) : null;
                player.Equipment.Armor = equipData.Armor != null ? FromItemJson(equipData.Armor) : null;
            }
        }

        // Restore skills
        if (!string.IsNullOrEmpty(data.SkillsJson) && data.SkillsJson != "{}")
        {
            var skillData = JsonSerializer.Deserialize(data.SkillsJson, PlayerJsonContext.Default.SkillSlotsJson);
            if (skillData != null)
            {
                player.Skills.Skill0 = skillData.Skill0;
                player.Skills.Skill1 = skillData.Skill1;
                player.Skills.Skill2 = skillData.Skill2;
                player.Skills.Skill3 = skillData.Skill3;
                player.Skills.Cooldown0 = skillData.Cooldown0;
                player.Skills.Cooldown1 = skillData.Cooldown1;
                player.Skills.Cooldown2 = skillData.Cooldown2;
                player.Skills.Cooldown3 = skillData.Cooldown3;
            }
        }

        // Restore quickslots
        if (!string.IsNullOrEmpty(data.QuickSlotsJson) && data.QuickSlotsJson != "{}")
        {
            var qsData = JsonSerializer.Deserialize(data.QuickSlotsJson, PlayerJsonContext.Default.QuickSlotsJson);
            if (qsData != null)
            {
                player.QuickSlots.Slot0 = qsData.Slot0;
                player.QuickSlots.Slot1 = qsData.Slot1;
                player.QuickSlots.Slot2 = qsData.Slot2;
                player.QuickSlots.Slot3 = qsData.Slot3;
            }
        }

        return ref player;
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
