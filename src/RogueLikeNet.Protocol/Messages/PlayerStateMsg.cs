using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class PlayerStateMsg : IEquatable<PlayerStateMsg>
{
    [Key(0)] public int Health { get; set; }
    [Key(1)] public int MaxHealth { get; set; }
    [Key(2)] public int Attack { get; set; }
    [Key(3)] public int Defense { get; set; }
    [Key(4)] public int Level { get; set; }
    [Key(5)] public int Experience { get; set; }
    [Key(6)] public int InventoryCount { get; set; }
    [Key(7)] public int InventoryCapacity { get; set; }
    [Key(9)] public ItemDataMsg[] InventoryItems { get; set; } = [];
    [Key(10)] public ItemDataMsg[] EquippedItems { get; set; } = [];
    [Key(11)] public int[] QuickSlotIndices { get; set; } = [];
    [Key(12)] public long PlayerEntityId { get; set; }
    [Key(13)] public int Hunger { get; set; }
    [Key(14)] public int MaxHunger { get; set; }
    [Key(15)] public int Thirst { get; set; }
    [Key(16)] public int MaxThirst { get; set; }
    [Key(17)] public int[] NearbyStationsTypes { get; set; } = [];
    [Key(18)] public int ClassId { get; set; }
    [Key(19)] public int BonusAttack { get; set; }
    [Key(20)] public int BonusDefense { get; set; }
    [Key(21)] public int Mana { get; set; }
    [Key(22)] public int MaxMana { get; set; }
    [Key(23)] public PlayerQuestStateMsg? Quests { get; set; }

    public static bool Equals(PlayerStateMsg? a, PlayerStateMsg? b)
    {
        if (a is null) return b is null;
        if (b is null) return false;
        if (a.Health != b.Health) return false;
        if (a.MaxHealth != b.MaxHealth) return false;
        if (a.Attack != b.Attack) return false;
        if (a.Defense != b.Defense) return false;
        if (a.BonusAttack != b.BonusAttack) return false;
        if (a.BonusDefense != b.BonusDefense) return false;
        if (a.Level != b.Level) return false;
        if (a.Experience != b.Experience) return false;
        if (a.InventoryCount != b.InventoryCount) return false;
        if (a.InventoryCapacity != b.InventoryCapacity) return false;
        if (!a.InventoryItems.SequenceEqual(b.InventoryItems)) return false;
        if (!a.EquippedItems.SequenceEqual(b.EquippedItems)) return false;
        if (!a.QuickSlotIndices.SequenceEqual(b.QuickSlotIndices)) return false;
        if (a.PlayerEntityId != b.PlayerEntityId) return false;
        if (a.Hunger != b.Hunger) return false;
        if (a.MaxHunger != b.MaxHunger) return false;
        if (a.Thirst != b.Thirst) return false;
        if (a.MaxThirst != b.MaxThirst) return false;
        if (!a.NearbyStationsTypes.SequenceEqual(b.NearbyStationsTypes)) return false;
        if (a.ClassId != b.ClassId) return false;
        if (a.Mana != b.Mana) return false;
        if (a.MaxMana != b.MaxMana) return false;
        if (!QuestStateEquals(a.Quests, b.Quests)) return false;

        return true;
    }

    private static bool QuestStateEquals(PlayerQuestStateMsg? a, PlayerQuestStateMsg? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (!a.CompletedQuestIds.SequenceEqual(b.CompletedQuestIds)) return false;
        if (!a.QuestGiverEntityIds.SequenceEqual(b.QuestGiverEntityIds)) return false;
        if (a.Active.Length != b.Active.Length) return false;
        for (int i = 0; i < a.Active.Length; i++)
        {
            var aq = a.Active[i];
            var bq = b.Active[i];
            if (aq.QuestNumericId != bq.QuestNumericId) return false;
            if (aq.GiverEntityId != bq.GiverEntityId) return false;
            if (aq.Objectives.Length != bq.Objectives.Length) return false;
            for (int j = 0; j < aq.Objectives.Length; j++)
            {
                if (aq.Objectives[j].Current != bq.Objectives[j].Current) return false;
                if (aq.Objectives[j].Target != bq.Objectives[j].Target) return false;
            }
        }
        return true;
    }

    public bool Equals(PlayerStateMsg? other)
    {
        return Equals(this, other);
    }
}
