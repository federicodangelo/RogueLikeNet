using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class DirectionalTunnelGeneratorTests
{
    [Fact]
    public void Generate_ProducesFloorTiles()
    {
        var gen = new DirectionalTunnelGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 0, "Tunnel should have floor tiles");
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new DirectionalTunnelGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Blocked) wallCount++;

        Assert.True(wallCount > 0, "Tunnel should have walls");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new DirectionalTunnelGenerator(42);
        var result1 = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var result2 = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                Assert.Equal(result1.Chunk.Tiles[x, y].Type, result2.Chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_ProducesMonsters()
    {
        var gen = new DirectionalTunnelGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        Assert.True(result.Monsters.Count > 0, "Tunnel should produce monsters");
    }
}
