using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class DirectionalTunnelGeneratorTests
{
    [Fact]
    public void Generate_ProducesFloorTiles()
    {
        var gen = new DirectionalTunnelGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            if (chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 0, "Tunnel should have floor tiles");
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new DirectionalTunnelGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            if (chunk.Tiles[x, y].Type == TileType.Wall) wallCount++;

        Assert.True(wallCount > 0, "Tunnel should have walls");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new DirectionalTunnelGenerator();
        var chunk1 = new Chunk(0, 0);
        var chunk2 = new Chunk(0, 0);
        gen.Generate(chunk1, 42);
        gen.Generate(chunk2, 42);

        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            Assert.Equal(chunk1.Tiles[x, y].Type, chunk2.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_ProducesSpawnPoints()
    {
        var gen = new DirectionalTunnelGenerator();
        var chunk = new Chunk(0, 0);
        var result = gen.Generate(chunk, 42);

        Assert.True(result.SpawnPoints.Count > 0, "Tunnel should produce spawn points");
    }
}
