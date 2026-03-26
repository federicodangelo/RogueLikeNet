using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class ChunkDataMsg
{
    [Key(0)] public int ChunkX { get; set; }
    [Key(1)] public int ChunkY { get; set; }
    /// <summary>Flat array of Size*Size tile types</summary>
    [Key(2)] public byte[] TileTypes { get; set; } = [];
    [Key(3)] public int[] TileGlyphs { get; set; } = [];
    [Key(4)] public int[] TileFgColors { get; set; } = [];
    [Key(5)] public int[] TileBgColors { get; set; } = [];
}
