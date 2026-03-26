using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class WorldSnapshotMsg
{
    [Key(0)] public long WorldTick { get; set; }
    [Key(1)] public ChunkDataMsg[] Chunks { get; set; } = [];
    [Key(2)] public EntityMsg[] Entities { get; set; } = [];
    [Key(3)] public long PlayerEntityId { get; set; }
    [Key(4)] public int PlayerX { get; set; }
    [Key(5)] public int PlayerY { get; set; }
    [Key(6)] public PlayerStateMsg? PlayerState { get; set; }
    [Key(7)] public FloorItemsMsg? FloorItems { get; set; }
}

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

[MessagePackObject]
public class EntityMsg
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public int X { get; set; }
    [Key(2)] public int Y { get; set; }
    [Key(3)] public int GlyphId { get; set; }
    [Key(4)] public int FgColor { get; set; }
    [Key(5)] public int Health { get; set; }
    [Key(6)] public int MaxHealth { get; set; }
    [Key(7)] public int LightRadius { get; set; }
}
