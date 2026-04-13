using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Tests;

public class ClassDefinitionsTests
{

    [Fact]
    public void Get_ReturnsCorrectClasses()
    {
        Assert.True(ClassDefinitions.NumClasses >= 4);
        for (int i = 0; i < ClassDefinitions.NumClasses; i++)
        {
            var def = ClassDefinitions.GetDef(i);
            Assert.NotNull(def);
            Assert.False(string.IsNullOrEmpty(def.Name));
        }
    }

    [Fact]
    public void GetStartingStats_ReturnsValidStats()
    {
        for (int i = 0; i < ClassDefinitions.NumClasses; i++)
        {
            var stats = ClassDefinitions.GetStartingStats(i);
            Assert.True(stats.Attack >= -10 && stats.Attack <= 10);
            Assert.True(stats.Defense >= -10 && stats.Defense <= 10);
        }
    }

    [Fact]
    public void GetAsciiArt_ReturnsFiveLines()
    {
        for (int i = 0; i < ClassDefinitions.NumClasses; i++)
        {
            var art = ClassDefinitions.GetAsciiArt(i);
            Assert.NotNull(art);
            Assert.Equal(5, art.Length);
        }
    }

    [Fact]
    public void All_ContainsFourClasses()
    {
        Assert.Equal(4, ClassDefinitions.NumClasses);
    }

    [Fact]
    public void ClassesHaveExpectedNames()
    {
        var names = ClassDefinitions.All.Select(c => c.Name).ToList();
        Assert.Contains("Warrior", names);
        Assert.Contains("Rogue", names);
        Assert.Contains("Mage", names);
        Assert.Contains("Ranger", names);
    }

    [Fact]
    public void Warrior_HasExpectedStats()
    {
        var warrior = ClassDefinitions.All.First(c => c.Id == "warrior");
        var stats = ClassDefinitions.GetStartingStats(warrior.ClassIndex);
        Assert.Equal(3, stats.Attack);
        Assert.Equal(3, stats.Defense);
        Assert.Equal(20, stats.Health);
        Assert.Equal(0, stats.Speed);
    }

    [Fact]
    public void Rogue_HasExpectedStats()
    {
        var rogue = ClassDefinitions.All.First(c => c.Id == "rogue");
        var stats = ClassDefinitions.GetStartingStats(rogue.ClassIndex);
        Assert.Equal(1, stats.Attack);
        Assert.Equal(0, stats.Defense);
        Assert.Equal(0, stats.Health);
        Assert.Equal(2, stats.Speed);
    }

    [Fact]
    public void Mage_HasExpectedStats()
    {
        var mage = ClassDefinitions.All.First(c => c.Id == "mage");
        var stats = ClassDefinitions.GetStartingStats(mage.ClassIndex);
        Assert.Equal(0, stats.Attack);
        Assert.Equal(0, stats.Defense);
        Assert.Equal(-10, stats.Health);
        Assert.Equal(2, stats.Speed);
    }

    [Fact]
    public void Ranger_HasExpectedStats()
    {
        var ranger = ClassDefinitions.All.First(c => c.Id == "ranger");
        var stats = ClassDefinitions.GetStartingStats(ranger.ClassIndex);
        Assert.Equal(2, stats.Attack);
        Assert.Equal(1, stats.Defense);
        Assert.Equal(0, stats.Health);
        Assert.Equal(2, stats.Speed);
    }

    [Fact]
    public void LevelBonuses_ExistForAllClasses()
    {
        for (int i = 0; i < ClassDefinitions.NumClasses; i++)
        {
            var def = ClassDefinitions.GetDef(i);
            Assert.NotNull(def.LevelBonuses);
            Assert.True(def.LevelBonuses.Length > 0);
        }
    }

    [Fact]
    public void PlayerLevels_AreLoaded()
    {
        var levels = GameData.Instance.PlayerLevels;
        Assert.True(levels.Count >= 10);
        Assert.Equal(0, levels.GetXpRequired(1));
        Assert.Equal(100, levels.GetXpRequired(2));
        Assert.Equal(10, levels.MaxLevel);
    }

    [Fact]
    public void PlayerLevels_GetLevelForXp()
    {
        var levels = GameData.Instance.PlayerLevels;
        Assert.Equal(1, levels.GetLevelForXp(0));
        Assert.Equal(1, levels.GetLevelForXp(50));
        Assert.Equal(2, levels.GetLevelForXp(100));
        Assert.Equal(2, levels.GetLevelForXp(299));
        Assert.Equal(3, levels.GetLevelForXp(300));
        Assert.Equal(10, levels.GetLevelForXp(99999));
    }
}
