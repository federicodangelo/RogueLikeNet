using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class WorldDeltaMsg
{
    [Key(0)] public long FromTick { get; set; }
    [Key(1)] public long ToTick { get; set; }
    [Key(2)] public TileUpdateMsg[] TileUpdates { get; set; } = [];
    [Key(3)] public EntityUpdateMsg[] EntityUpdates { get; set; } = [];
    [Key(4)] public CombatEventMsg[] CombatEvents { get; set; } = [];
    [Key(5)] public ChunkDataMsg[] Chunks { get; set; } = [];
    [Key(6)] public PlayerStateMsg? PlayerState { get; set; }
}

[MessagePackObject]
public class TileUpdateMsg
{
    [Key(0)] public int X { get; set; }
    [Key(1)] public int Y { get; set; }
    [Key(2)] public byte TileType { get; set; }
    [Key(3)] public int GlyphId { get; set; }
    [Key(4)] public int FgColor { get; set; }
    [Key(5)] public int BgColor { get; set; }
    [Key(6)] public int LightLevel { get; set; }
}

[MessagePackObject]
public class EntityUpdateMsg
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public int X { get; set; }
    [Key(2)] public int Y { get; set; }
    [Key(3)] public int GlyphId { get; set; }
    [Key(4)] public int FgColor { get; set; }
    [Key(5)] public int Health { get; set; }
    [Key(6)] public int MaxHealth { get; set; }
    [Key(7)] public bool Removed { get; set; }
    [Key(8)] public int LightRadius { get; set; }
    [Key(9)] public string? ItemName { get; set; }
}

[MessagePackObject]
public class CombatEventMsg
{
    [Key(0)] public int AttackerX { get; set; }
    [Key(1)] public int AttackerY { get; set; }
    [Key(2)] public int TargetX { get; set; }
    [Key(3)] public int TargetY { get; set; }
    [Key(4)] public int Damage { get; set; }
    [Key(5)] public bool TargetDied { get; set; }
}

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
    [Key(8)] public int[] SkillIds { get; set; } = [];
    [Key(9)] public int[] SkillCooldowns { get; set; } = [];
    [Key(10)] public string[] InventoryNames { get; set; } = [];
    // Key(11) reserved (was FloorItemNames, now derived from entity data)
    [Key(12)] public string[] SkillNames { get; set; } = [];
    [Key(13)] public string EquippedWeaponName { get; set; } = "";
    [Key(14)] public string EquippedArmorName { get; set; } = "";
    [Key(15)] public int[] InventoryStackCounts { get; set; } = [];
    [Key(16)] public int[] InventoryRarities { get; set; } = [];
    [Key(17)] public int[] InventoryCategories { get; set; } = [];
    [Key(18)] public int[] QuickSlotIndices { get; set; } = [];
    [Key(19)] public long PlayerEntityId { get; set; }
}


