using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

// This needs to be a struct, otherwise it doesn't play well with MessagePackSerializer AOT compilation.
[MessagePackObject]
public class ItemDataMsg
{
    [Key(0)] public int ItemTypeId { get; set; }
    [Key(1)] public int StackCount { get; set; }
    [Key(2)] public int Rarity { get; set; }
    [Key(3)] public int Category { get; set; }
    [Key(4)] public int BonusAttack { get; set; }
    [Key(5)] public int BonusDefense { get; set; }
    [Key(6)] public int BonusHealth { get; set; }

    public static bool Equals(ItemDataMsg? a, ItemDataMsg? b)
    {
        if (a is null) return b is null;
        if (b is null) return false;
        return
            a.ItemTypeId == b.ItemTypeId &&
            a.StackCount == b.StackCount &&
            a.Rarity == b.Rarity &&
            a.Category == b.Category &&
            a.BonusAttack == b.BonusAttack &&
            a.BonusDefense == b.BonusDefense &&
            a.BonusHealth == b.BonusHealth;
    }
}
