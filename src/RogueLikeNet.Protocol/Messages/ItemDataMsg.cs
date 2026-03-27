using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public struct ItemDataMsg
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
        var av = a.Value;
        var bv = b.Value;
        return
            av.ItemTypeId == bv.ItemTypeId &&
            av.StackCount == bv.StackCount &&
            av.Rarity == bv.Rarity &&
            av.Category == bv.Category &&
            av.BonusAttack == bv.BonusAttack &&
            av.BonusDefense == bv.BonusDefense &&
            av.BonusHealth == bv.BonusHealth;
    }
}
