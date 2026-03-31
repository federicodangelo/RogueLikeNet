using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class ChunkDataMsg
{
    [Key(0)] public int ChunkX { get; set; }
    [Key(1)] public int ChunkY { get; set; }
    [Key(2)] public int ChunkZ { get; set; }
    [Key(3)] public byte[] TileTypes { get; set; } = [];
    [Key(4)] public int[] TileGlyphs { get; set; } = [];
    [Key(5)] public int[] TileFgColors { get; set; } = [];
    [Key(6)] public int[] TileBgColors { get; set; } = [];
    [Key(7)] public int[] TilePlaceableItemIds { get; set; } = [];
    [Key(8)] public int[] TilePlaceableItemExtras { get; set; } = [];
}
