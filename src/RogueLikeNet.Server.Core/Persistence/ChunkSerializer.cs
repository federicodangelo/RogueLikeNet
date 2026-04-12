using System.IO;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Server.Persistence;

/// <summary>
/// Serializes/deserializes chunk tile data using BinaryWriter/BinaryReader.
/// </summary>
public static class ChunkSerializer
{
    private const byte TileFormatVersion = 1;

    public static byte[] SerializeTiles(TileInfo[,] tiles)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(TileFormatVersion);
        bw.Write(Chunk.Size);
        bw.Write(Chunk.Size);

        for (int x = 0; x < Chunk.Size; x++)
        {
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref tiles[x, y];
                bw.Write(tile.TileId);
                bw.Write(tile.PlaceableItemId);
                bw.Write(tile.PlaceableItemExtra);
            }
        }

        return ms.ToArray();
    }

    public static TileInfo[,] DeserializeTiles(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        var version = br.ReadByte();
        var sizeX = br.ReadInt32();
        var sizeY = br.ReadInt32();

        var tiles = new TileInfo[sizeX, sizeY];

        for (int x = 0; x < sizeX; x++)
        {
            for (int y = 0; y < sizeY; y++)
            {
                tiles[x, y] = new TileInfo
                {
                    TileId = br.ReadInt32(),
                    PlaceableItemId = br.ReadInt32(),
                    PlaceableItemExtra = br.ReadInt32(),
                };
            }
        }

        return tiles;
    }

    private const byte ExploredFormatVersion = 1;
    private const int BitmaskSize = Chunk.Size * Chunk.Size / 8; // 512 bytes

    /// <summary>
    /// Serializes per-player explored bitmasks.
    /// Format: [version:1][count:4] then for each entry [playerId:4][bitmask:512].
    /// </summary>
    public static byte[] SerializeExploredData(Dictionary<int, byte[]>? exploredByPlayer)
    {
        if (exploredByPlayer == null || exploredByPlayer.Count == 0)
            return [];

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(ExploredFormatVersion);
        bw.Write(exploredByPlayer.Count);
        foreach (var (playerId, bitmask) in exploredByPlayer)
        {
            bw.Write(playerId);
            bw.Write(bitmask, 0, BitmaskSize);
        }

        return ms.ToArray();
    }

    /// <summary>
    /// Deserializes per-player explored bitmasks from binary data. Returns null if empty.
    /// </summary>
    public static Dictionary<int, byte[]>? DeserializeExploredData(byte[] data)
    {
        if (data.Length == 0)
            return null;

        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        var version = br.ReadByte();
        var count = br.ReadInt32();
        if (count == 0)
            return null;

        var result = new Dictionary<int, byte[]>(count);
        for (int i = 0; i < count; i++)
        {
            var playerId = br.ReadInt32();
            var bitmask = br.ReadBytes(BitmaskSize);
            result[playerId] = bitmask;
        }

        return result;
    }
}
