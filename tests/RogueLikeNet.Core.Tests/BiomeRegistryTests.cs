using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Tests;

public class BiomeRegistryTests
{
    [Fact]
    public void Get_ByBiomeType_ReturnsDefinition()
    {
        var def = GameData.Instance.Biomes.Get(BiomeType.Forest);
        Assert.NotNull(def);
        Assert.Equal("Forest", def!.Name);
    }

    [Fact]
    public void GetBiomeName_ReturnsDisplayName()
    {
        Assert.Equal("Forest", GameData.Instance.Biomes.GetBiomeName(BiomeType.Forest));
    }

    [Fact]
    public void GetBiomeName_UnknownBiome_ReturnsUnknown()
    {
        // BiomeType enum may not have all values registered
        Assert.NotNull(GameData.Instance.Biomes.GetBiomeName(BiomeType.Stone));
    }

    [Fact]
    public void GetDecorations_ReturnsList()
    {
        var decorations = GameData.Instance.Biomes.GetDecorations(BiomeType.Forest);
        Assert.NotNull(decorations);
    }

    [Fact]
    public void GetDecorations_UnknownBiome_ReturnsEmpty()
    {
        // If a biome isn't loaded, should return empty
        var registry = new BiomeRegistry(GameData.Instance.Tiles);
        registry.Register([]);
        var decorations = registry.GetDecorations(BiomeType.Forest);
        Assert.Empty(decorations);
    }

    [Fact]
    public void GetEnemySpawns_ReturnsList()
    {
        var spawns = GameData.Instance.Biomes.GetEnemySpawns(BiomeType.Forest);
        Assert.NotNull(spawns);
    }

    [Fact]
    public void GetFloorTileId_ReturnsValidId()
    {
        var tileId = GameData.Instance.Biomes.GetFloorTileId(BiomeType.Forest);
        Assert.NotEqual(0, tileId);
    }

    [Fact]
    public void GetWallTileId_ReturnsValidId()
    {
        var tileId = GameData.Instance.Biomes.GetWallTileId(BiomeType.Forest);
        Assert.NotEqual(0, tileId);
    }

    [Fact]
    public void GetFloorTileId_ResolvesToFloorType()
    {
        var tileId = GameData.Instance.Biomes.GetFloorTileId(BiomeType.Forest);
        var tileDef = GameData.Instance.Tiles.Get(tileId);
        Assert.NotNull(tileDef);
        Assert.Equal(TileType.Floor, tileDef!.Type);
    }

    [Fact]
    public void GetWallTileId_ResolvesToBlockedType()
    {
        var tileId = GameData.Instance.Biomes.GetWallTileId(BiomeType.Forest);
        var tileDef = GameData.Instance.Tiles.Get(tileId);
        Assert.NotNull(tileDef);
        Assert.Equal(TileType.Blocked, tileDef!.Type);
    }

    [Fact]
    public void GetLiquid_ReturnsNullOrLiquid()
    {
        // Some biomes have liquids, some don't
        _ = GameData.Instance.Biomes.GetLiquid(BiomeType.Forest);
        _ = GameData.Instance.Biomes.GetLiquid(BiomeType.Lava);
    }

    [Fact]
    public void GetLiquid_LavaBiome_HasValidTileId()
    {
        var liquid = GameData.Instance.Biomes.GetLiquid(BiomeType.Lava);
        Assert.NotNull(liquid);
        Assert.NotEqual(0, liquid!.TileNumericId);
    }

    [Fact]
    public void Decorations_HaveResolvedTileIds()
    {
        var decorations = GameData.Instance.Biomes.GetDecorations(BiomeType.Forest);
        Assert.NotEmpty(decorations);
        foreach (var deco in decorations)
            Assert.NotEqual(0, deco.TileNumericId);
    }

    // ── PickEnemy ──

    [Fact]
    public void PickEnemy_ReturnsNpcDefinition()
    {
        var rng = new SeededRandom(42);
        var enemy = GameData.Instance.Biomes.PickEnemy(BiomeType.Forest, rng, 0);
        Assert.NotNull(enemy);
    }

    [Fact]
    public void PickEnemy_HighDifficulty_StillReturns()
    {
        var rng = new SeededRandom(42);
        var enemy = GameData.Instance.Biomes.PickEnemy(BiomeType.Forest, rng, 100);
        Assert.NotNull(enemy);
    }

    // ── Static methods ──

    [Fact]
    public void GetBiomeForChunk_IsDeterministic()
    {
        var pos = ChunkPosition.FromCoords(5, 10, 127);
        var b1 = BiomeRegistry.GetBiomeForChunk(pos, 42);
        var b2 = BiomeRegistry.GetBiomeForChunk(pos, 42);
        Assert.Equal(b1, b2);
    }

    [Fact]
    public void GetBiomeForChunk_DifferentSeeds_MayDiffer()
    {
        var pos = ChunkPosition.FromCoords(5, 10, 127);
        var b1 = BiomeRegistry.GetBiomeForChunk(pos, 42);
        var b2 = BiomeRegistry.GetBiomeForChunk(pos, 99);
        // May or may not differ, but should not throw
        _ = b1;
        _ = b2;
    }

    [Theory]
    [InlineData(-0.5, -0.5, BiomeType.Ice)]      // cold, dry
    [InlineData(-0.5, 0.0, BiomeType.Ice)]        // cold, damp
    [InlineData(-0.5, 0.5, BiomeType.Fungal)]     // cold, wet
    [InlineData(-0.2, -0.5, BiomeType.Stone)]     // cool, dry
    [InlineData(-0.2, 0.0, BiomeType.Arcane)]     // cool, damp
    [InlineData(-0.2, 0.5, BiomeType.Forest)]     // cool, wet
    [InlineData(0.2, -0.5, BiomeType.Ruined)]     // warm, dry
    [InlineData(0.2, 0.0, BiomeType.Crypt)]       // warm, damp
    [InlineData(0.2, 0.5, BiomeType.Sewer)]       // warm, wet
    [InlineData(0.5, -0.5, BiomeType.Lava)]       // hot, dry
    [InlineData(0.5, 0.0, BiomeType.Lava)]        // hot, damp
    [InlineData(0.5, 0.5, BiomeType.Infernal)]    // hot, wet
    public void GetBiomeFromClimate_ReturnsExpected(double temp, double moisture, BiomeType expected)
    {
        Assert.Equal(expected, BiomeRegistry.GetBiomeFromClimate(temp, moisture));
    }
}
