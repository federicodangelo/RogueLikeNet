using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class ChunkDataMsg
{
    [Key(0)] public int ChunkX { get; set; }
    [Key(1)] public int ChunkY { get; set; }
    [Key(2)] public int ChunkZ { get; set; }
    [Key(3)] public int[] TileIds { get; set; } = [];
    [Key(4)] public int[] TilePlaceableItemIds { get; set; } = [];
    [Key(5)] public int[] TilePlaceableItemExtras { get; set; } = [];
}
