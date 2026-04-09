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
    [Key(8)] public SkillSlotMsg[] Skills { get; set; } = [];
    [Key(9)] public ItemDataMsg[] InventoryItems { get; set; } = [];
    [Key(10)] public ItemDataMsg[] EquippedItems { get; set; } = [];
    [Key(11)] public int[] QuickSlotIndices { get; set; } = [];
    [Key(12)] public long PlayerEntityId { get; set; }
    [Key(13)] public int Hunger { get; set; }
    [Key(14)] public int MaxHunger { get; set; }

    public static bool Equals(PlayerStateMsg? a, PlayerStateMsg? b)
    {
        if (a is null) return b is null;
        if (b is null) return false;
        if (a.Health != b.Health) return false;
        if (a.MaxHealth != b.MaxHealth) return false;
        if (a.Attack != b.Attack) return false;
        if (a.Defense != b.Defense) return false;
        if (a.Level != b.Level) return false;
        if (a.Experience != b.Experience) return false;
        if (a.InventoryCount != b.InventoryCount) return false;
        if (a.InventoryCapacity != b.InventoryCapacity) return false;
        if (!a.Skills.SequenceEqual(b.Skills)) return false;
        if (!a.InventoryItems.SequenceEqual(b.InventoryItems)) return false;
        if (!a.EquippedItems.SequenceEqual(b.EquippedItems)) return false;
        if (!a.QuickSlotIndices.SequenceEqual(b.QuickSlotIndices)) return false;
        if (a.PlayerEntityId != b.PlayerEntityId) return false;
        if (a.Hunger != b.Hunger) return false;
        if (a.MaxHunger != b.MaxHunger) return false;

        return true;
    }

    public bool Equals(PlayerStateMsg? other)
    {
        return Equals(this, other);
    }
}
