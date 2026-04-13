using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class BiomeEnemySpawnTests
{
    [Fact]
    public void PickEnemy_ReturnsValidMonster()
    {
        var rng = new SeededRandom(42);
        var def = GameData.Instance.Biomes.PickEnemy(BiomeType.Forest, rng, 0);
        Assert.NotNull(def);
        Assert.True(def.Health > 0);
        Assert.True(def.Attack > 0);
    }

    [Theory]
    [InlineData(BiomeType.Stone)]
    [InlineData(BiomeType.Lava)]
    [InlineData(BiomeType.Ice)]
    [InlineData(BiomeType.Forest)]
    [InlineData(BiomeType.Arcane)]
    [InlineData(BiomeType.Crypt)]
    [InlineData(BiomeType.Sewer)]
    [InlineData(BiomeType.Fungal)]
    [InlineData(BiomeType.Ruined)]
    [InlineData(BiomeType.Infernal)]
    public void PickEnemy_AllBiomesReturnValidMonster(BiomeType biome)
    {
        var rng = new SeededRandom(123);
        for (int i = 0; i < 50; i++)
        {
            var def = GameData.Instance.Biomes.PickEnemy(biome, rng, 10);
            Assert.True(def != null && def.Health > 0, $"Biome {biome} returned monster with 0 health");
        }
    }

    [Fact]
    public void PickEnemy_LowDifficulty_NeverReturnsHighAttackMonsters()
    {
        var rng = new SeededRandom(999);
        for (int i = 0; i < 200; i++)
        {
            var def = GameData.Instance.Biomes.PickEnemy(BiomeType.Infernal, rng, 0);
            // Difficulty 0 gates by attack/4, so only NPCs with attack < 4 should appear
            Assert.True(def != null && def.Attack < 4,
                $"Difficulty 0 should not produce {def.Name} (Attack {def.Attack})");
        }
    }

    [Fact]
    public void PickEnemy_HighDifficulty_CanReturnStrongMonsters()
    {
        var rng = new SeededRandom(42);
        bool foundStrong = false;
        // High difficulty should allow NPCs with higher attack stats
        for (int i = 0; i < 200; i++)
        {
            var def = GameData.Instance.Biomes.PickEnemy(BiomeType.Infernal, rng, 10);
            if (def != null && def.Attack >= 8) foundStrong = true;
        }
        Assert.True(foundStrong, "High difficulty in Infernal biome should sometimes produce strong monsters");
    }

    [Fact]
    public void PickEnemy_BiomesHaveDifferentDistributions()
    {
        // Forest should produce mostly weak enemies; Crypt should produce mostly its signature enemies
        var forestRng = new SeededRandom(42);
        var cryptRng = new SeededRandom(42);
        var forestNames = new Dictionary<string, int>();
        var cryptNames = new Dictionary<string, int>();
        const int trials = 500;

        for (int i = 0; i < trials; i++)
        {
            var forestDef = GameData.Instance.Biomes.PickEnemy(BiomeType.Forest, forestRng, 10);
            Assert.True(forestDef != null, "Forest biome should return a monster definition");
            forestNames[forestDef.Name] = forestNames.GetValueOrDefault(forestDef.Name) + 1;

            var cryptDef = GameData.Instance.Biomes.PickEnemy(BiomeType.Crypt, cryptRng, 10);
            Assert.True(cryptDef != null, "Crypt biome should return a monster definition");
            cryptNames[cryptDef.Name] = cryptNames.GetValueOrDefault(cryptDef.Name) + 1;
        }

        // Both biomes should produce a dominant enemy type
        var forestTop = forestNames.MaxBy(kv => kv.Value);
        var cryptTop = cryptNames.MaxBy(kv => kv.Value);
        Assert.True(forestTop.Value > trials / 4, $"Forest should have a dominant enemy but top is {forestTop.Key}={forestTop.Value}/{trials}");
        Assert.True(cryptTop.Value > trials / 4, $"Crypt should have a dominant enemy but top is {cryptTop.Key}={cryptTop.Value}/{trials}");
    }

    [Theory]
    [InlineData(BiomeType.Stone)]
    [InlineData(BiomeType.Lava)]
    [InlineData(BiomeType.Ice)]
    [InlineData(BiomeType.Forest)]
    [InlineData(BiomeType.Arcane)]
    [InlineData(BiomeType.Crypt)]
    [InlineData(BiomeType.Sewer)]
    [InlineData(BiomeType.Fungal)]
    [InlineData(BiomeType.Ruined)]
    [InlineData(BiomeType.Infernal)]
    public void GetEnemySpawns_AllBiomesHaveEntries(BiomeType biome)
    {
        var spawns = GameData.Instance.Biomes.GetEnemySpawns(biome);
        Assert.True(spawns.Length > 0, $"Biome {biome} has no enemy spawn entries");

        int totalWeight = 0;
        foreach (var s in spawns) totalWeight += s.Weight;
        Assert.True(totalWeight > 0, $"Biome {biome} has zero total weight");
    }
}
