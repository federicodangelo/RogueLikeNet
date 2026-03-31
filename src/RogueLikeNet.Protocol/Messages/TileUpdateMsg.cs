using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class TileUpdateMsg
{
    [Key(0)] public int X { get; set; }
    [Key(1)] public int Y { get; set; }
    [Key(2)] public int Z { get; set; }
    [Key(3)] public byte TileType { get; set; }
    [Key(4)] public int GlyphId { get; set; }
    [Key(5)] public int FgColor { get; set; }
    [Key(6)] public int BgColor { get; set; }
    [Key(7)] public int LightLevel { get; set; }
    [Key(8)] public int PlaceableItemId { get; set; }
    [Key(9)] public int PlaceableItemExtra { get; set; }
}
