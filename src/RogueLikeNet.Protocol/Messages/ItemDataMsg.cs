using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

// This needs to be a struct, otherwise it doesn't play well with MessagePackSerializer AOT compilation.
[MessagePackObject]
public class ItemDataMsg : IEquatable<ItemDataMsg>
{
    [Key(0)] public int ItemTypeId { get; set; }
    [Key(1)] public int StackCount { get; set; }
    [Key(2)] public int Category { get; set; }
    [Key(3)] public int EquipSlot { get; set; }

    public static bool Equals(ItemDataMsg? a, ItemDataMsg? b)
    {
        if (a is null) return b is null;
        if (b is null) return false;
        return
            a.ItemTypeId == b.ItemTypeId &&
            a.StackCount == b.StackCount &&
            a.Category == b.Category &&
            a.EquipSlot == b.EquipSlot;
    }

    public bool Equals(ItemDataMsg? other)
    {
        return Equals(this, other);
    }
}
