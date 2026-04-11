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
}
