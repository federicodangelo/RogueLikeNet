using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class WorldMapTests
{
    [Fact]
    public void GetOrCreateChunk_GeneratesOnDemand()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator();
        var (chunk, genResult) = map.GetOrCreateChunk(0, 0, gen);
        Assert.NotNull(chunk);
        Assert.Equal(0, chunk.ChunkX);
        Assert.Equal(0, chunk.ChunkY);
        Assert.NotNull(genResult);
    }

    [Fact]
    public void GetOrCreateChunk_ReturnsSameChunk()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator();
        var (c1, _) = map.GetOrCreateChunk(0, 0, gen);
        var (c2, genResult2) = map.GetOrCreateChunk(0, 0, gen);
        Assert.Same(c1, c2);
        Assert.Null(genResult2);
    }

    [Fact]
    public void TryGetChunk_ReturnsNullForMissing()
    {
        var map = new WorldMap(42);
        Assert.Null(map.TryGetChunk(0, 0));
    }

    [Fact]
    public void Seed_ReturnsConstructorValue()
    {
        var map = new WorldMap(12345);
        Assert.Equal(12345, map.Seed);
    }

    [Fact]
    public void GetTile_ReturnsDefaultForMissingChunk()
    {
        var map = new WorldMap(42);
        var tile = map.GetTile(0, 0);
        Assert.Equal(TileType.Void, tile.Type);
    }

    [Fact]
    public void GetTile_ReturnsCorrectTileForLoadedChunk()
    {
        var map = new WorldMap(42);
        var gen = new BspDungeonGenerator();
        map.GetOrCreateChunk(0, 0, gen);
        // After generation, at least some tiles should be non-void
        bool hasNonVoid = false;
        for (int x = 0; x < Chunk.Size && !hasNonVoid; x++)
        for (int y = 0; y < Chunk.Size && !hasNonVoid; y++)
        {
            var tile = map.GetTile(x, y);
            if (tile.Type != TileType.Void) hasNonVoid = true;
        }
        Assert.True(hasNonVoid);
    }

    [Fact]
    public void IsWalkable_ReturnsFalseForMissingChunk()
    {
        var map = new WorldMap(42);
        Assert.False(map.IsWalkable(0, 0));
    }
}
