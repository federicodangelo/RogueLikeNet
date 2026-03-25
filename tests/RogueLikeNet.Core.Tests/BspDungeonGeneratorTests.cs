using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;
using Chunk = RogueLikeNet.Core.World.Chunk;

namespace RogueLikeNet.Core.Tests;

public class BspDungeonGeneratorTests
{
    [Fact]
    public void Generate_ProducesFloorTiles()
    {
        var gen = new BspDungeonGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        int floorCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            if (chunk.Tiles[x, y].Type == TileType.Floor) floorCount++;

        Assert.True(floorCount > 0, "Dungeon should have at least some floor tiles");
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        var gen = new BspDungeonGenerator();
        var chunk1 = new Chunk(0, 0);
        var chunk2 = new Chunk(0, 0);
        gen.Generate(chunk1, 42);
        gen.Generate(chunk2, 42);

        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            Assert.Equal(chunk1.Tiles[x, y].Type, chunk2.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new BspDungeonGenerator();
        var chunk = new Chunk(0, 0);
        gen.Generate(chunk, 42);

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
        for (int y = 0; y < Chunk.Size; y++)
            if (chunk.Tiles[x, y].Type == TileType.Wall) wallCount++;

        Assert.True(wallCount > 0, "Dungeon should have walls");
    }

    [Fact]
    public void Generate_AppliesBiomeTints()
    {
        // Find a chunk whose biome is NOT Stone (Stone = neutral, no tint)
        var gen = new BspDungeonGenerator();
        Chunk? tintedChunk = null;
        for (int cx = 0; cx < 20; cx++)
        {
            var biome = BiomeDefinitions.GetBiomeForChunk(cx, 0, 42);
            if (biome != BiomeType.Stone)
            {
                tintedChunk = new Chunk(cx, 0);
                gen.Generate(tintedChunk, 42);
                break;
            }
        }

        Assert.NotNull(tintedChunk);

        // Find a floor tile and verify its FgColor differs from the base floor color
        bool foundTinted = false;
        for (int x = 0; x < Chunk.Size && !foundTinted; x++)
        for (int y = 0; y < Chunk.Size && !foundTinted; y++)
        {
            if (tintedChunk.Tiles[x, y].Type == TileType.Floor &&
                tintedChunk.Tiles[x, y].FgColor != TileDefinitions.ColorFloorFg)
                foundTinted = true;
        }

        Assert.True(foundTinted, "Non-Stone biome should produce tinted floor colors");
    }

    [Fact]
    public void Generate_PlacesDecorations()
    {
        // Generate many chunks — at least some should have decorations
        var gen = new BspDungeonGenerator();
        int totalDecorations = 0;
        for (int cx = 0; cx < 10; cx++)
        {
            var chunk = new Chunk(cx, 0);
            gen.Generate(chunk, 42);
            for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (chunk.Tiles[x, y].Type == TileType.Decoration)
                    totalDecorations++;
        }

        Assert.True(totalDecorations > 0, "Generator should place decorations in at least some chunks");
    }

    [Fact]
    public void Generate_PlacesLiquidPools()
    {
        // Generate chunks until we hit a biome with liquid (Lava, Sewer, Infernal, etc.)
        var gen = new BspDungeonGenerator();
        bool foundLiquid = false;
        for (int cx = 0; cx < 50 && !foundLiquid; cx++)
        {
            var biome = BiomeDefinitions.GetBiomeForChunk(cx, 0, 42);
            if (BiomeDefinitions.GetLiquid(biome) == null) continue;

            var chunk = new Chunk(cx, 0);
            gen.Generate(chunk, 42);
            for (int x = 0; x < Chunk.Size && !foundLiquid; x++)
            for (int y = 0; y < Chunk.Size && !foundLiquid; y++)
                if (chunk.Tiles[x, y].Type is TileType.Water or TileType.Lava)
                    foundLiquid = true;
        }

        Assert.True(foundLiquid, "Liquid biomes should generate water or lava pools");
    }

    [Fact]
    public void Generate_DecorationsAreWalkable()
    {
        var gen = new BspDungeonGenerator();
        for (int cx = 0; cx < 5; cx++)
        {
            var chunk = new Chunk(cx, 0);
            gen.Generate(chunk, 42);
            for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
            {
                ref var tile = ref chunk.Tiles[x, y];
                if (tile.Type == TileType.Decoration)
                    Assert.True(tile.IsWalkable, $"Decoration at ({x},{y}) should be walkable");
            }
        }
    }
}
