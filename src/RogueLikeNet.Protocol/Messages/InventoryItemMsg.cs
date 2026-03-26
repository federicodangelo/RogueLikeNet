using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public struct InventoryItemMsg
{
    [Key(0)] public string Name { get; set; }
    [Key(1)] public int StackCount { get; set; }
    [Key(2)] public int Rarity { get; set; }
    [Key(3)] public int Category { get; set; }
}
