using RogueLikeNet.Core.Data;
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
    public void All_ContainsMonsters()
    {
        Assert.True(NpcDefinitions.All.Length > 0, "NPC registry should have entries");
    }

    [Fact]
    public void Get_ByRegistry_ReturnsCorrectDefinition()
    {
        var npcReg = GameData.Instance.Npcs;
        var goblin = npcReg.Get("goblin");
        Assert.NotNull(goblin);
        Assert.Equal("Goblin", goblin.Name);
        Assert.True(goblin.Health > 0);
        Assert.True(goblin.Attack > 0);

        var def = NpcDefinitions.Get(goblin.NumericId);
        Assert.Equal(goblin.Name, def.Name);
        Assert.Equal(goblin.Health, def.Health);
        Assert.Equal(goblin.Attack, def.Attack);
    }

    [Fact]
    public void Get_InvalidTypeId_ReturnsDefault()
    {
        var def = NpcDefinitions.Get(9999);
        Assert.Equal(0, def.TypeId);
        Assert.Null(def.Name);
    }
}
