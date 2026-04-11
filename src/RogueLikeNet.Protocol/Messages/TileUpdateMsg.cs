using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class TileUpdateMsg
{
    [Key(0)] public int X { get; set; }
    [Key(1)] public int Y { get; set; }
    [Key(2)] public int Z { get; set; }
    [Key(3)] public int TileId { get; set; }
    [Key(4)] public int PlaceableItemId { get; set; }
    [Key(5)] public int PlaceableItemExtra { get; set; }
}
