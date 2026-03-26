using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

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
