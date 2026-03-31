using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Components;
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
        var result = gen.Generate(0, 0, Position.DefaultZ);

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
        var result1 = gen.Generate(0, 0, Position.DefaultZ);
        var result2 = gen.Generate(0, 0, Position.DefaultZ);

        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                Assert.Equal(result1.Chunk.Tiles[x, y].Type, result2.Chunk.Tiles[x, y].Type);
    }

    [Fact]
    public void Generate_HasWalls()
    {
        var gen = new BspDungeonGenerator(42);
        var result = gen.Generate(0, 0, Position.DefaultZ);

        int wallCount = 0;
        for (int x = 0; x < Chunk.Size; x++)
            for (int y = 0; y < Chunk.Size; y++)
                if (result.Chunk.Tiles[x, y].Type == TileType.Blocked) wallCount++;

        Assert.True(wallCount > 0, "Dungeon should have walls");
    }

    [Fact]
    public void Generate_AppliesBiomeTints()
    {
        // Find a chunk whose biome is NOT Stone (Stone = neutral, no tint)
        var gen = new BspDungeonGenerator(42);
        Chunk? tintedChunk = null;
        for (int cx = 0; cx < 20; cx++)
        {
            var biome = BiomeDefinitions.GetBiomeForChunk(cx, 0, 42);
            if (biome != BiomeType.Stone)
            {
                var result = gen.Generate(cx, 0, Position.DefaultZ);
                tintedChunk = result.Chunk;
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
        var gen = new BspDungeonGenerator(42);
        int totalDecorations = 0;
        for (int cx = 0; cx < 10; cx++)
        {
            var result = gen.Generate(cx, 0, Position.DefaultZ);
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                {
                    ref var t = ref result.Chunk.Tiles[x, y];
                    if (t.Type == TileType.Floor && t.GlyphId != TileDefinitions.GlyphFloor)
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
            var biome = BiomeDefinitions.GetBiomeForChunk(cx, 0, 42);
            if (BiomeDefinitions.GetLiquid(biome) == null) continue;

            var result = gen.Generate(cx, 0, Position.DefaultZ);
            for (int x = 0; x < Chunk.Size && !foundLiquid; x++)
                for (int y = 0; y < Chunk.Size && !foundLiquid; y++)
                    if (result.Chunk.Tiles[x, y].Type is TileType.Water or TileType.Lava)
                        foundLiquid = true;
        }

        Assert.True(foundLiquid, "Liquid biomes should generate water or lava pools");
    }

    [Fact]
    public void Generate_DecorationsAreWalkable()
    {
        var gen = new BspDungeonGenerator(42);
        for (int cx = 0; cx < 5; cx++)
        {
            var result = gen.Generate(cx, 0, Position.DefaultZ);
            for (int x = 0; x < Chunk.Size; x++)
                for (int y = 0; y < Chunk.Size; y++)
                {
                    ref var tile = ref result.Chunk.Tiles[x, y];
                    if (tile.Type == TileType.Floor && tile.GlyphId != TileDefinitions.GlyphFloor)
                        Assert.True(tile.IsWalkable, $"Decoration at ({x},{y}) should be walkable");
                }
        }
    }
}
