using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Generation;

namespace RogueLikeNet.Core.Tests;

public class MonsterDefinitionsTests
{
    [Fact]
    public void Pick_ReturnsValidMonster()
    {
        var rng = new SeededRandom(42);
        var template = NpcDefinitions.Pick(rng, 0);
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
            var template = NpcDefinitions.Pick(rng, 10);
            if (template.Health >= 30) foundHard = true;
        }
        Assert.True(foundHard, "High difficulty should sometimes produce tougher monsters");
    }

    [Fact]
    public void All_ContainsAllMonsters()
    {
        Assert.Equal(4, NpcDefinitions.All.Length);
    }

    [Theory]
    [InlineData(NpcDefinitions.Goblin, "Goblin", 15, 4, 1)]
    [InlineData(NpcDefinitions.Orc, "Orc", 30, 7, 3)]
    [InlineData(NpcDefinitions.Skeleton, "Skeleton", 20, 5, 2)]
    [InlineData(NpcDefinitions.Dragon, "Dragon", 100, 15, 8)]
    public void Get_ReturnsCorrectDefinition(int typeId, string name, int health, int attack, int defense)
    {
        var def = NpcDefinitions.Get(typeId);
        Assert.Equal(typeId, def.TypeId);
        Assert.Equal(name, def.Name);
        Assert.Equal(health, def.Health);
        Assert.Equal(attack, def.Attack);
        Assert.Equal(defense, def.Defense);
    }

    [Fact]
    public void Get_InvalidTypeId_ReturnsDefault()
    {
        var def = NpcDefinitions.Get(9999);
        Assert.Equal(0, def.TypeId);
        Assert.Null(def.Name);
    }
}
