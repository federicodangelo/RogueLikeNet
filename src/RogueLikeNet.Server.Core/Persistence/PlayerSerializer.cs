using System.Text.Json;
using System.Text.Json.Serialization;
using RogueLikeNet.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.Systems;

namespace RogueLikeNet.Server.Persistence;

// Source-generated JSON context for AOT-compatible serialization of player data.
[JsonSerializable(typeof(List<PlayerSerializer.ItemDataJson>))]
[JsonSerializable(typeof(PlayerSerializer.EquipmentJson))]
[JsonSerializable(typeof(PlayerSerializer.EquipSlotJson))]
[JsonSerializable(typeof(List<PlayerSerializer.EquipSlotJson>))]
[JsonSerializable(typeof(PlayerSerializer.QuickSlotsJson))]
[JsonSerializable(typeof(PlayerSerializer.ActiveQuestJson))]
[JsonSerializable(typeof(List<PlayerSerializer.ActiveQuestJson>))]
[JsonSerializable(typeof(PlayerSerializer.QuestsJson))]
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
    public static PlayerSaveData SerializePlayer(PlayerEntity player, string playerName, string passwordHash = "", string passwordSalt = "")
    {
        var data = new PlayerSaveData
        {
            PlayerName = playerName,
            ServerPlayerId = player.ServerPlayerId,
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
            Hunger = player.Survival.Hunger,
            MaxHunger = player.Survival.MaxHunger,
            Thirst = player.Survival.Thirst,
            MaxThirst = player.Survival.MaxThirst,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
        };

        // Inventory
        var items = player.Inventory.Items?.Select(i => new ItemDataJson
        {
            ItemTypeId = i.ItemTypeId,
            StackCount = i.StackCount,
            Durability = i.Durability,
        }).ToList() ?? [];
        data.InventoryJson = JsonSerializer.Serialize(items, PlayerJsonContext.Default.ListItemDataJson);

        // Equipment
        var equipSlots = new List<EquipSlotJson>();
        for (int slot = 0; slot < Equipment.SlotCount; slot++)
        {
            if (player.Equipment.HasItem(slot))
                equipSlots.Add(new EquipSlotJson { Slot = slot, Item = ToItemJson(player.Equipment[slot]) });
        }
        var equipData = new EquipmentJson { Slots = equipSlots };
        data.EquipmentJson = JsonSerializer.Serialize(equipData, PlayerJsonContext.Default.EquipmentJson);

        // QuickSlots
        var qsData = new QuickSlotsJson
        {
            Slots = new int[player.QuickSlots.Count]
        };
        for (var i = 0; i < player.QuickSlots.Count; i++)
            qsData.Slots[i] = player.QuickSlots[i];

        data.QuickSlotsJson = JsonSerializer.Serialize(qsData, PlayerJsonContext.Default.QuickSlotsJson);

        // Quests
        var questsData = new QuestsJson();
        if (player.Quests.ActiveQuests != null)
        {
            questsData.Active = new List<ActiveQuestJson>(player.Quests.ActiveQuests.Count);
            foreach (var q in player.Quests.ActiveQuests)
            {
                var objs = new int[q.Objectives.Length];
                for (int i = 0; i < objs.Length; i++)
                    objs[i] = q.Objectives[i].Current;
                questsData.Active.Add(new ActiveQuestJson
                {
                    QuestNumericId = q.QuestNumericId,
                    GiverEntityId = q.GiverEntityId,
                    GiverName = q.GiverName,
                    GiverChunkX = q.GiverChunkX,
                    GiverChunkY = q.GiverChunkY,
                    GiverChunkZ = q.GiverChunkZ,
                    ObjectiveCurrent = objs,
                });
            }
        }
        if (player.Quests.CompletedQuestIds != null && player.Quests.CompletedQuestIds.Count > 0)
            questsData.Completed = player.Quests.CompletedQuestIds.ToArray();
        data.QuestsJson = JsonSerializer.Serialize(questsData, PlayerJsonContext.Default.QuestsJson);

        return data;
    }

    /// <summary>
    /// Restores a player entity in the game engine from saved data.
    /// Returns the spawned PlayerEntity.
    /// </summary>
    public static ref PlayerEntity RestorePlayer(GameEngine engine, long connectionId, PlayerSaveData data)
    {
        // Spawn shell player (gets default stats)
        ref var player = ref engine.SpawnPlayer(connectionId, Position.FromCoords(data.PositionX, data.PositionY, data.PositionZ), data.ClassId);

        // Restore persistent player ID
        player.ServerPlayerId = data.ServerPlayerId;

        // Override with saved state
        player.Health.Current = data.HealthCurrent;
        player.Health.Max = data.HealthMax;

        player.ClassData.Level = data.Level;
        player.ClassData.Experience = data.Experience;

        // Restore survival
        player.Survival.Hunger = data.Hunger;
        player.Survival.MaxHunger = data.MaxHunger;
        player.Survival.Thirst = data.Thirst;
        player.Survival.MaxThirst = data.MaxThirst;

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
                    if (GameData.Instance.Items.Get(item.ItemTypeId) != null) // Skip invalid items that don't exist in current game data
                    {
                        player.Inventory.Items.Add(new ItemData
                        {
                            ItemTypeId = item.ItemTypeId,
                            StackCount = item.StackCount,
                            Durability = item.Durability,
                        });
                    }
                }
            }
        }

        // Restore equipment
        if (!string.IsNullOrEmpty(data.EquipmentJson) && data.EquipmentJson != "{}")
        {
            var equipData = JsonSerializer.Deserialize(data.EquipmentJson, PlayerJsonContext.Default.EquipmentJson);
            if (equipData?.Slots != null)
            {
                foreach (var slotData in equipData.Slots)
                {
                    if (slotData.Item != null && slotData.Slot >= 0 && slotData.Slot < Equipment.SlotCount)
                    {
                        var item = FromItemJson(slotData.Item);
                        if (GameData.Instance.Items.Get(item.ItemTypeId) != null) // Skip invalid items that don't exist in current game data
                        {
                            player.Equipment[slotData.Slot] = item;
                        }
                    }
                }
            }
        }

        // Restore quickslots
        if (!string.IsNullOrEmpty(data.QuickSlotsJson) && data.QuickSlotsJson != "{}")
        {
            var qsData = JsonSerializer.Deserialize(data.QuickSlotsJson, PlayerJsonContext.Default.QuickSlotsJson);
            if (qsData != null)
            {
                for (var i = 0; i < player.QuickSlots.Count; i++)
                {
                    player.QuickSlots[i] = -1; // Clear all quickslots first
                    if (qsData.Slots != null && i < qsData.Slots.Length)
                        player.QuickSlots[i] = qsData.Slots[i];
                }

                for (var i = 0; i < QuickSlots.SlotCount; i++)
                {
                    int invIndex = player.QuickSlots[i];
                    if (invIndex < 0 ||
                        player.Inventory.Items == null ||
                        invIndex >= player.Inventory.Items.Count ||
                        player.Inventory.Items[invIndex].ItemTypeId == 0)
                    {
                        player.QuickSlots[i] = -1; // Clear invalid quickslot reference
                    }
                }
            }
        }

        // Recalculate combat stats from class + level + equipment (replaces saved stats)
        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        // Restore health after recalculation (saved health may be lower than max)
        player.Health.Current = Math.Min(data.HealthCurrent, player.Health.Max);

        // Restore quests
        if (!string.IsNullOrEmpty(data.QuestsJson) && data.QuestsJson != "{}")
        {
            var questsData = JsonSerializer.Deserialize(data.QuestsJson, PlayerJsonContext.Default.QuestsJson);
            if (questsData != null)
            {
                player.Quests = PlayerQuests.Empty();
                if (questsData.Active != null)
                {
                    foreach (var aq in questsData.Active)
                    {
                        var questDef = GameData.Instance.Quests.Get(aq.QuestNumericId);
                        if (questDef == null) continue; // Drop quests that no longer exist in current game data
                        var progress = new ObjectiveProgress[questDef.Objectives.Length];
                        for (int i = 0; i < progress.Length; i++)
                        {
                            int target = questDef.Objectives[i].Count;
                            int current = (aq.ObjectiveCurrent != null && i < aq.ObjectiveCurrent.Length)
                                ? aq.ObjectiveCurrent[i] : 0;
                            progress[i] = new ObjectiveProgress { Current = Math.Min(current, target), Target = target };
                        }
                        player.Quests.ActiveQuests!.Add(new ActiveQuest
                        {
                            QuestNumericId = aq.QuestNumericId,
                            GiverEntityId = aq.GiverEntityId,
                            GiverName = aq.GiverName ?? "",
                            GiverChunkX = aq.GiverChunkX,
                            GiverChunkY = aq.GiverChunkY,
                            GiverChunkZ = aq.GiverChunkZ,
                            Objectives = progress,
                        });
                    }
                }
                if (questsData.Completed != null)
                {
                    foreach (var completedId in questsData.Completed)
                    {
                        if (GameData.Instance.Quests.Get(completedId) != null)
                            player.Quests.CompletedQuestIds!.Add(completedId);
                    }
                }
            }
        }

        return ref player;
    }

    private static ItemDataJson ToItemJson(ItemData item) => new()
    {
        ItemTypeId = item.ItemTypeId,
        StackCount = item.StackCount,
        Durability = item.Durability,
    };

    private static ItemData FromItemJson(ItemDataJson j) => new()
    {
        ItemTypeId = j.ItemTypeId,
        StackCount = j.StackCount,
        Durability = j.Durability,
    };

    // JSON DTOs — must be public for source-generated serialization
    public class ItemDataJson
    {
        public int ItemTypeId { get; set; }
        public int StackCount { get; set; }
        public int Durability { get; set; }
    }

    public class EquipSlotJson
    {
        public int Slot { get; set; }
        public ItemDataJson? Item { get; set; }
    }

    public class EquipmentJson
    {
        public List<EquipSlotJson>? Slots { get; set; }
    }

    public class QuickSlotsJson
    {
        public int[]? Slots { get; set; }
    }

    public class ActiveQuestJson
    {
        public int QuestNumericId { get; set; }
        public int GiverEntityId { get; set; }
        public string? GiverName { get; set; }
        public int GiverChunkX { get; set; }
        public int GiverChunkY { get; set; }
        public int GiverChunkZ { get; set; }
        public int[]? ObjectiveCurrent { get; set; }
    }

    public class QuestsJson
    {
        public List<ActiveQuestJson>? Active { get; set; }
        public int[]? Completed { get; set; }
    }
}
