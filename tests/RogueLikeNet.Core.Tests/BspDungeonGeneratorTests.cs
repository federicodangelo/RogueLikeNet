using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class BspDungeonGeneratorTests
{
    [Fact]
    public void Generate_ProducesFloorTiles()
    {
        var gen = new BspDungeonGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 0, "Dungeon should have at least some floor tiles");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new BspDungeonGenerator(42);
        var result1 = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));
        var result2 = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                Assert.Equal(result1.Chunk.Tiles[x, y].Type, result2.Chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new BspDungeonGenerator(42);
        var result = gen.Generate(ChunkPosition.FromCoords(0, 0, Position.DefaultZ));

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Blocked) wallCount++;

        Assert.True(wallCount > 0, "Dungeon should have walls");
    }

    [Fact]
    public void Generate_UsesBiomeTileIds()
    {
        // Find a chunk whose biome is NOT Stone
        var gen = new BspDungeonGenerator(42);
        Chunk? biomeChunk = null;
        BiomeType foundBiome = BiomeType.Stone;
        for (int cx = 0; cx < 20; cx++)
        {
            var biome = BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx, 0, 0), 42);
            if (biome != BiomeType.Stone)
            {
                var result = gen.Generate(ChunkPosition.FromCoords(cx, 0, Position.DefaultZ));
                biomeChunk = result.Chunk;
                foundBiome = biome;
                break;
            }
        }

        Assert.NotNull(biomeChunk);

        // Floor tiles should use the biome-specific floor tile ID
        int expectedFloorTileId = GameData.Instance.Biomes.GetFloorTileId(foundBiome);
        bool foundBiomeFloor = false;
        for (int x = 0; x < Chunk.Size && !foundBiomeFloor; x++)
            for (int y = 0; y < Chunk.Size && !foundBiomeFloor; y++)
            {
                if (biomeChunk.Tiles[x, y].TileId == expectedFloorTileId)
                    foundBiomeFloor = true;
            }

        Assert.True(foundBiomeFloor, "Non-Stone biome should use biome-specific floor tile IDs");
    }

    [Fact]
    public void Generate_PlacesDecorations()
    {
        // Generate many chunks — at least some should have decorations
        var gen = new BspDungeonGenerator(42);
        int totalDecorations = 0;
        for (int cx = 0; cx < 10; cx++)
        {
            var biome = BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx, 0, 0), 42);
            int floorTileId = GameData.Instance.Biomes.GetFloorTileId(biome);
            int wallTileId = GameData.Instance.Biomes.GetWallTileId(biome);
            var result = gen.Generate(ChunkPosition.FromCoords(cx, 0, Position.DefaultZ));
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                {
                    ref var t = ref result.Chunk.Tiles[x, y];
                    if (t.Type == TileType.Floor && t.TileId != floorTileId)
                        totalDecorations++;
                }
        }

        Assert.True(totalDecorations > 0, "Generator should place decorations in at least some chunks");
    }

    [Fact]
    public void Generate_PlacesLiquidPools()
    {
        // Generate chunks until we hit a biome with liquid (Lava, Sewer, Infernal, etc.)
        var gen = new BspDungeonGenerator(42);
        bool foundLiquid = false;
        for (int cx = 0; cx < 50 && !foundLiquid; cx++)
        {
            var biome = BiomeRegistry.GetBiomeForChunk(ChunkPosition.FromCoords(cx, 0, 0), 42);
            if (GameData.Instance.Biomes.GetLiquid(biome) == null) continue;

            var result = gen.Generate(ChunkPosition.FromCoords(cx, 0, Position.DefaultZ));
            for (int x = 0; x < Chunk.Size && !foundLiquid; x++)
                for (int y = 0; y < Chunk.Size && !foundLiquid; y++)
                    if (result.Chunk.Tiles[x, y].Type is TileType.Water or TileType.Lava)
                        foundLiquid = true;
        }

        Assert.True(foundLiquid, "Liquid biomes should generate water or lava pools");
    }
}
