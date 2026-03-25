using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class MonsterDefinitionsTests
{
    [Fact]
    public void Pick_ReturnsValidMonster()
    {
        var rng = new SeededRandom(42);
        var template = MonsterDefinitions.Pick(rng, 0);
        Assert.True(template.Health > 0);
        Assert.True(template.Attack > 0);
    }

    [Fact]
    public void Pick_HigherDifficulty_UnlocksHarderMonsters()
    {
        var rng = new SeededRandom(12345);
        bool foundHard = false;
        for (int i = 0; i < 100; i++)
        {
            var template = MonsterDefinitions.Pick(rng, 10);
            if (template.Health >= 30) foundHard = true;
        }
        Assert.True(foundHard, "High difficulty should sometimes produce tougher monsters");
    }
}
