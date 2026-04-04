using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class ChunkTests
{
    [Fact]
    public void Chunk_HasCorrectSize()
    {
        var chunk = new Chunk(Position.FromCoords(0, 0, Position.DefaultZ));
        Assert.Equal(64, Chunk.Size);
        Assert.Equal(64, chunk.Tiles.GetLength(0));
        Assert.Equal(64, chunk.Tiles.GetLength(1));
    }

    [Fact]
    public void InBounds_ReturnsTrueForValidCoords()
    {
        var chunk = new Chunk(Position.FromCoords(0, 0, Position.DefaultZ));
        Assert.True(chunk.InBounds(0, 0));
        Assert.True(chunk.InBounds(63, 63));
        Assert.False(chunk.InBounds(-1, 0));
        Assert.False(chunk.InBounds(0, 64));
    }

    [Fact]
    public void WorldToChunkCoord_PositiveCoords()
    {
        var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(65, 130, Position.DefaultZ));
        Assert.Equal(1, cx);
        Assert.Equal(2, cy);
    }

    [Fact]
    public void WorldToChunkCoord_NegativeCoords()
    {
        var (cx, cy, _) = Chunk.WorldToChunkCoord(Position.FromCoords(-1, -64, Position.DefaultZ));
        Assert.Equal(-1, cx);
        Assert.Equal(-1, cy);
    }

    [Fact]
    public void WorldToLocal_ConvertsCorrectly()
    {
        var chunk = new Chunk(Position.FromCoords(1, 0, Position.DefaultZ));
        Assert.True(chunk.WorldToLocal(64, 0, out int lx, out int ly));
        Assert.Equal(0, lx);
        Assert.Equal(0, ly);
    }

    [Fact]
    public void PackChunkKey_UniquePerCoord()
    {
        long k1 = Position.PackCoord(0, 0, Position.DefaultZ);
        long k2 = Position.PackCoord(1, 0, Position.DefaultZ);
        long k3 = Position.PackCoord(0, 1, Position.DefaultZ);
        Assert.NotEqual(k1, k2);
        Assert.NotEqual(k1, k3);
        Assert.NotEqual(k2, k3);
    }
}
