using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Tests;

public class BiomeClimateTests
{
    [Theory]
    [InlineData(-0.6, -0.5, BiomeType.Ice)]     // cold + dry
    [InlineData(-0.6, 0.5, BiomeType.Fungal)]    // cold + wet
    [InlineData(-0.2, -0.5, BiomeType.Stone)]    // cool + dry
    [InlineData(-0.2, 0.0, BiomeType.Arcane)]    // cool + damp
    [InlineData(-0.2, 0.5, BiomeType.Forest)]    // cool + wet
    [InlineData(0.2, -0.5, BiomeType.Ruined)]    // warm + dry
    [InlineData(0.2, 0.0, BiomeType.Crypt)]      // warm + damp
    [InlineData(0.2, 0.5, BiomeType.Sewer)]      // warm + wet
    [InlineData(0.6, -0.5, BiomeType.Lava)]      // hot + dry
    [InlineData(0.6, 0.5, BiomeType.Infernal)]   // hot + wet
    public void GetBiomeFromClimate_MapsCorrectly(double temp, double moist, BiomeType expected)
    {
        Assert.Equal(expected, BiomeDefinitions.GetBiomeFromClimate(temp, moist));
    }

    [Fact]
    public void GetBiomeFromClimate_AllBiomesReachable()
    {
        var found = new HashSet<BiomeType>();
        // Sweep the full range
        for (double t = -1.0; t <= 1.0; t += 0.1)
        for (double m = -1.0; m <= 1.0; m += 0.1)
            found.Add(BiomeDefinitions.GetBiomeFromClimate(t, m));

        Assert.Equal(BiomeDefinitions.BiomeCount, found.Count);
    }
}
