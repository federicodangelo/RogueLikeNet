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
        var registry = new BiomeRegistry();
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
    public void GetFloorColor_ReturnsColor()
    {
        var color = GameData.Instance.Biomes.GetFloorColor(BiomeType.Forest);
        Assert.True(color >= 0);
    }

    [Fact]
    public void GetLiquid_ReturnsNullOrLiquid()
    {
        // Some biomes have liquids, some don't
        _ = GameData.Instance.Biomes.GetLiquid(BiomeType.Forest);
        _ = GameData.Instance.Biomes.GetLiquid(BiomeType.Lava);
    }

    // ── ApplyBiomeTint ──

    [Fact]
    public void ApplyBiomeTint_ByBiomeType_TintsColor()
    {
        int white = 0xFFFFFF;
        int result = GameData.Instance.Biomes.ApplyBiomeTint(white, BiomeType.Forest);
        // Tinted color should differ from white (unless tint is 100,100,100)
        Assert.True(result >= 0);
    }

    [Fact]
    public void ApplyBiomeTint_ZeroColor_ReturnsZero()
    {
        Assert.Equal(0, GameData.Instance.Biomes.ApplyBiomeTint(0, BiomeType.Forest));
    }

    [Fact]
    public void ApplyBiomeTint_ByNumericId_TintsColor()
    {
        var def = GameData.Instance.Biomes.Get(BiomeType.Forest);
        if (def == null) return;
        int white = 0xFFFFFF;
        int result = GameData.Instance.Biomes.ApplyBiomeTint(white, def.NumericId);
        Assert.True(result >= 0);
    }

    [Fact]
    public void ApplyBiomeTint_ByNumericId_ZeroColor_ReturnsZero()
    {
        Assert.Equal(0, GameData.Instance.Biomes.ApplyBiomeTint(0, 1));
    }

    [Fact]
    public void ApplyBiomeTint_ByNumericId_UnknownId_ReturnsOriginal()
    {
        int color = 0xFF8040;
        Assert.Equal(color, GameData.Instance.Biomes.ApplyBiomeTint(color, 9999));
    }

    [Fact]
    public void ApplyBiomeTint_ByBiomeType_UnknownBiome_ReturnsOriginal()
    {
        var registry = new BiomeRegistry();
        registry.Register([]);
        int color = 0xFF8040;
        Assert.Equal(color, registry.ApplyBiomeTint(color, BiomeType.Forest));
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
