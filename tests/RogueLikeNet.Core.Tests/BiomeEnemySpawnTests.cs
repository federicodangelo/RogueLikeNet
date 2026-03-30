using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class BiomeEnemySpawnTests
{
    [Fact]
    public void PickEnemy_ReturnsValidMonster()
    {
        var rng = new SeededRandom(42);
        var def = BiomeDefinitions.PickEnemy(BiomeType.Forest, rng, 0);
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
            var def = BiomeDefinitions.PickEnemy(biome, rng, 10);
            Assert.True(def.Health > 0, $"Biome {biome} returned monster with 0 health");
        }
    }

    [Fact]
    public void PickEnemy_LowDifficulty_NeverReturnsDragons()
    {
        var rng = new SeededRandom(999);
        for (int i = 0; i < 200; i++)
        {
            var def = BiomeDefinitions.PickEnemy(BiomeType.Infernal, rng, 0);
            // Difficulty 0 allows maxTypeId=1 (Goblin and Orc only)
            Assert.True(def.TypeId <= NpcDefinitions.Orc,
                $"Difficulty 0 should not produce {def.Name} (TypeId {def.TypeId})");
        }
    }

    [Fact]
    public void PickEnemy_HighDifficulty_CanReturnDragons()
    {
        var rng = new SeededRandom(42);
        bool foundDragon = false;
        // Lava and Infernal have high dragon weights
        for (int i = 0; i < 200; i++)
        {
            var def = BiomeDefinitions.PickEnemy(BiomeType.Infernal, rng, 10);
            if (def.TypeId == NpcDefinitions.Dragon) foundDragon = true;
        }
        Assert.True(foundDragon, "High difficulty in Infernal biome should sometimes produce dragons");
    }

    [Fact]
    public void PickEnemy_BiomesHaveDifferentDistributions()
    {
        // Forest should produce mostly goblins; Crypt should produce mostly skeletons
        var forestRng = new SeededRandom(42);
        var cryptRng = new SeededRandom(42);
        int forestGoblins = 0, cryptSkeletons = 0;
        const int trials = 500;

        for (int i = 0; i < trials; i++)
        {
            var forestDef = BiomeDefinitions.PickEnemy(BiomeType.Forest, forestRng, 10);
            if (forestDef.TypeId == NpcDefinitions.Goblin) forestGoblins++;

            var cryptDef = BiomeDefinitions.PickEnemy(BiomeType.Crypt, cryptRng, 10);
            if (cryptDef.TypeId == NpcDefinitions.Skeleton) cryptSkeletons++;
        }

        // Forest has 55% goblin weight, Crypt has 60% skeleton weight
        Assert.True(forestGoblins > trials / 4, $"Forest should have many goblins but got {forestGoblins}/{trials}");
        Assert.True(cryptSkeletons > trials / 4, $"Crypt should have many skeletons but got {cryptSkeletons}/{trials}");
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
        var spawns = BiomeDefinitions.GetEnemySpawns(biome);
        Assert.True(spawns.Length > 0, $"Biome {biome} has no enemy spawn entries");

        int totalWeight = 0;
        foreach (var s in spawns) totalWeight += s.Weight;
        Assert.True(totalWeight > 0, $"Biome {biome} has zero total weight");
    }
}
