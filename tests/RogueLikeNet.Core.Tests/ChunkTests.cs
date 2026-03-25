using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class ChunkTests
{
    [Fact]
    public void Chunk_HasCorrectSize()
    {
        var chunk = new Chunk(0, 0);
        Assert.Equal(64, Chunk.Size);
        Assert.Equal(64, chunk.Tiles.GetLength(0));
        Assert.Equal(64, chunk.Tiles.GetLength(1));
    }

    [Fact]
    public void InBounds_ReturnsTrueForValidCoords()
    {
        var chunk = new Chunk(0, 0);
        Assert.True(chunk.InBounds(0, 0));
        Assert.True(chunk.InBounds(63, 63));
        Assert.False(chunk.InBounds(-1, 0));
        Assert.False(chunk.InBounds(0, 64));
    }

    [Fact]
    public void WorldToChunkCoord_PositiveCoords()
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(65, 130);
        Assert.Equal(1, cx);
        Assert.Equal(2, cy);
    }

    [Fact]
    public void WorldToChunkCoord_NegativeCoords()
    {
        var (cx, cy) = Chunk.WorldToChunkCoord(-1, -64);
        Assert.Equal(-1, cx);
        Assert.Equal(-1, cy);
    }

    [Fact]
    public void WorldToLocal_ConvertsCorrectly()
    {
        var chunk = new Chunk(1, 0);
        Assert.True(chunk.WorldToLocal(64, 0, out int lx, out int ly));
        Assert.Equal(0, lx);
        Assert.Equal(0, ly);
    }

    [Fact]
    public void PackChunkKey_UniquePerCoord()
    {
        long k1 = Chunk.PackChunkKey(0, 0);
        long k2 = Chunk.PackChunkKey(1, 0);
        long k3 = Chunk.PackChunkKey(0, 1);
        Assert.NotEqual(k1, k2);
        Assert.NotEqual(k1, k3);
        Assert.NotEqual(k2, k3);
    }

    [Fact]
    public void GetTile_ReturnsRefToTile()
    {
        var chunk = new Chunk(0, 0);
        ref var tile = ref chunk.GetTile(5, 5);
        tile.Type = TileType.Floor;
        Assert.Equal(TileType.Floor, chunk.Tiles[5, 5].Type);
    }
}
