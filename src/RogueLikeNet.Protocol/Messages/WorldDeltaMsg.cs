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
