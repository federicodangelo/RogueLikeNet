using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class PlayerStateMsg
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
    [Key(9)] public InventoryItemMsg[] InventoryItems { get; set; } = [];
    [Key(10)] public InventoryItemMsg? EquippedWeapon { get; set; }
    [Key(11)] public InventoryItemMsg? EquippedArmor { get; set; }
    [Key(12)] public int[] QuickSlotIndices { get; set; } = [];
    [Key(13)] public long PlayerEntityId { get; set; }
}
