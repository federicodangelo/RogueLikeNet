using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Tests;

public class ClassDefinitionsTests
{
    [Theory]
    [InlineData(ClassDefinitions.Warrior, "Warrior")]
    [InlineData(ClassDefinitions.Rogue, "Rogue")]
    [InlineData(ClassDefinitions.Mage, "Mage")]
    [InlineData(ClassDefinitions.Ranger, "Ranger")]
    public void Get_ReturnsCorrectClass(int classId, string expectedName)
    {
        var def = ClassDefinitions.Get(classId);
        Assert.Equal(classId, def.ClassId);
        Assert.Equal(expectedName, def.Name);
    }

    [Theory]
    [InlineData(ClassDefinitions.Warrior)]
    [InlineData(ClassDefinitions.Rogue)]
    [InlineData(ClassDefinitions.Mage)]
    [InlineData(ClassDefinitions.Ranger)]
    public void GetStartingStats_ReturnsValidStats(int classId)
    {
        var stats = ClassDefinitions.GetStartingStats(classId);
        // All classes should have some combination of stats
        Assert.True(stats.Attack >= -10 && stats.Attack <= 10);
        Assert.True(stats.Defense >= -10 && stats.Defense <= 10);
    }

    [Theory]
    [InlineData(ClassDefinitions.Warrior)]
    [InlineData(ClassDefinitions.Rogue)]
    [InlineData(ClassDefinitions.Mage)]
    [InlineData(ClassDefinitions.Ranger)]
    public void GetStartingSkills_ReturnsTwoSkills(int classId)
    {
        var skills = ClassDefinitions.GetStartingSkills(classId);
        Assert.NotEqual(SkillDefinitions.None, skills.Skill0);
        Assert.NotEqual(SkillDefinitions.None, skills.Skill1);
    }

    [Theory]
    [InlineData(ClassDefinitions.Warrior)]
    [InlineData(ClassDefinitions.Rogue)]
    [InlineData(ClassDefinitions.Mage)]
    [InlineData(ClassDefinitions.Ranger)]
    public void GetAsciiArt_ReturnsFiveLines(int classId)
    {
        var art = ClassDefinitions.GetAsciiArt(classId);
        Assert.NotNull(art);
        Assert.Equal(5, art.Length);
    }

    [Fact]
    public void All_ContainsFourClasses()
    {
        Assert.Equal(ClassDefinitions.NumClasses, ClassDefinitions.All.Length);
    }

    [Fact]
    public void Warrior_HasExpectedStats()
    {
        var stats = ClassDefinitions.GetStartingStats(ClassDefinitions.Warrior);
        Assert.Equal(3, stats.Attack);
        Assert.Equal(3, stats.Defense);
        Assert.Equal(20, stats.Health);
        Assert.Equal(0, stats.Speed);
    }

    [Fact]
    public void Rogue_HasExpectedStats()
    {
        var stats = ClassDefinitions.GetStartingStats(ClassDefinitions.Rogue);
        Assert.Equal(1, stats.Attack);
        Assert.Equal(0, stats.Defense);
        Assert.Equal(0, stats.Health);
        Assert.Equal(4, stats.Speed);
    }

    [Fact]
    public void Mage_HasExpectedStats()
    {
        var stats = ClassDefinitions.GetStartingStats(ClassDefinitions.Mage);
        Assert.Equal(0, stats.Attack);
        Assert.Equal(0, stats.Defense);
        Assert.Equal(-10, stats.Health);
        Assert.Equal(2, stats.Speed);
    }

    [Fact]
    public void Ranger_HasExpectedStats()
    {
        var stats = ClassDefinitions.GetStartingStats(ClassDefinitions.Ranger);
        Assert.Equal(2, stats.Attack);
        Assert.Equal(1, stats.Defense);
        Assert.Equal(0, stats.Health);
        Assert.Equal(2, stats.Speed);
    }

    [Fact]
    public void Warrior_Skills_ArePowerStrikeAndShieldBash()
    {
        var skills = ClassDefinitions.GetStartingSkills(ClassDefinitions.Warrior);
        Assert.Equal(SkillDefinitions.PowerStrike, skills.Skill0);
        Assert.Equal(SkillDefinitions.ShieldBash, skills.Skill1);
    }

    [Fact]
    public void Mage_Skills_AreFireballAndHeal()
    {
        var skills = ClassDefinitions.GetStartingSkills(ClassDefinitions.Mage);
        Assert.Equal(SkillDefinitions.Fireball, skills.Skill0);
        Assert.Equal(SkillDefinitions.Heal, skills.Skill1);
    }
}
