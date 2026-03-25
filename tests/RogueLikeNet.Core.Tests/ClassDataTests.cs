using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Tests;

public class ClassDataTests
{
    [Theory]
    [InlineData(SkillDefinitions.PowerStrike, 5)]
    [InlineData(SkillDefinitions.ShieldBash, 8)]
    [InlineData(SkillDefinitions.Backstab, 6)]
    [InlineData(SkillDefinitions.Dodge, 10)]
    [InlineData(SkillDefinitions.Fireball, 8)]
    [InlineData(SkillDefinitions.Heal, 12)]
    [InlineData(SkillDefinitions.PowerShot, 6)]
    [InlineData(SkillDefinitions.Trap, 15)]
    [InlineData(SkillDefinitions.None, 0)]
    public void GetCooldown_ReturnsCorrectValue(int skillId, int expected)
    {
        Assert.Equal(expected, SkillDefinitions.GetCooldown(skillId));
    }

    [Theory]
    [InlineData(SkillDefinitions.PowerStrike, 200)]
    [InlineData(SkillDefinitions.ShieldBash, 50)]
    [InlineData(SkillDefinitions.Backstab, 300)]
    [InlineData(SkillDefinitions.Fireball, 150)]
    [InlineData(SkillDefinitions.PowerShot, 180)]
    [InlineData(SkillDefinitions.Heal, 100)] // default case
    [InlineData(SkillDefinitions.Dodge, 100)] // default case
    [InlineData(SkillDefinitions.None, 100)] // default case
    public void GetDamageMultiplier_ReturnsCorrectValue(int skillId, int expected)
    {
        Assert.Equal(expected, SkillDefinitions.GetDamageMultiplier(skillId));
    }

    [Theory]
    [InlineData(SkillDefinitions.Fireball, 5)]
    [InlineData(SkillDefinitions.PowerShot, 5)]
    [InlineData(SkillDefinitions.Trap, 3)]
    [InlineData(SkillDefinitions.PowerStrike, 1)] // default case
    [InlineData(SkillDefinitions.None, 1)] // default case
    public void GetRange_ReturnsCorrectValue(int skillId, int expected)
    {
        Assert.Equal(expected, SkillDefinitions.GetRange(skillId));
    }

    [Fact]
    public void All_ContainsAllSkills()
    {
        Assert.Equal(8, SkillDefinitions.All.Length);
    }

    [Theory]
    [InlineData(SkillDefinitions.PowerStrike, "Power Strike", 5, 200, 1)]
    [InlineData(SkillDefinitions.Fireball, "Fireball", 8, 150, 5)]
    [InlineData(SkillDefinitions.Trap, "Trap", 15, 100, 3)]
    public void Get_ReturnsCorrectSkill(int skillId, string name, int cooldown, int dmgMult, int range)
    {
        var skill = SkillDefinitions.Get(skillId);
        Assert.Equal(skillId, skill.SkillId);
        Assert.Equal(name, skill.Name);
        Assert.Equal(cooldown, skill.Cooldown);
        Assert.Equal(dmgMult, skill.DamageMultiplier);
        Assert.Equal(range, skill.Range);
    }

    [Theory]
    [InlineData(SkillDefinitions.PowerStrike, "Power Strike")]
    [InlineData(SkillDefinitions.Backstab, "Backstab")]
    [InlineData(SkillDefinitions.Heal, "Heal")]
    [InlineData(SkillDefinitions.None, "")]
    public void GetName_ReturnsCorrectName(int skillId, string expectedName)
    {
        Assert.Equal(expectedName, SkillDefinitions.GetName(skillId));
    }
}
