using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public struct InventoryItemMsg
{
    [Key(0)] public int ItemTypeId { get; set; }
    [Key(1)] public int StackCount { get; set; }
    [Key(2)] public int Rarity { get; set; }
    [Key(3)] public int Category { get; set; }
    [Key(4)] public int BonusAttack { get; set; }
    [Key(5)] public int BonusDefense { get; set; }
    [Key(6)] public int BonusHealth { get; set; }
}
