using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Tests;

public class ClassDataTests
{
    [Theory]
    [InlineData(SkillIds.PowerStrike, 5)]
    [InlineData(SkillIds.ShieldBash, 8)]
    [InlineData(SkillIds.Backstab, 6)]
    [InlineData(SkillIds.Dodge, 10)]
    [InlineData(SkillIds.Fireball, 8)]
    [InlineData(SkillIds.Heal, 12)]
    [InlineData(SkillIds.PowerShot, 6)]
    [InlineData(SkillIds.Trap, 15)]
    [InlineData(SkillIds.None, 0)]
    public void GetCooldown_ReturnsCorrectValue(int skillId, int expected)
    {
        Assert.Equal(expected, SkillDefinitions.GetCooldown(skillId));
    }

    [Theory]
    [InlineData(SkillIds.PowerStrike, 200)]
    [InlineData(SkillIds.ShieldBash, 50)]
    [InlineData(SkillIds.Backstab, 300)]
    [InlineData(SkillIds.Fireball, 150)]
    [InlineData(SkillIds.PowerShot, 180)]
    [InlineData(SkillIds.Heal, 100)] // default case
    [InlineData(SkillIds.Dodge, 100)] // default case
    [InlineData(SkillIds.None, 100)] // default case
    public void GetDamageMultiplier_ReturnsCorrectValue(int skillId, int expected)
    {
        Assert.Equal(expected, SkillDefinitions.GetDamageMultiplier(skillId));
    }

    [Theory]
    [InlineData(SkillIds.Fireball, 5)]
    [InlineData(SkillIds.PowerShot, 5)]
    [InlineData(SkillIds.Trap, 3)]
    [InlineData(SkillIds.PowerStrike, 1)] // default case
    [InlineData(SkillIds.None, 1)] // default case
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
    [InlineData(SkillIds.PowerStrike, "Power Strike", 5, 200, 1)]
    [InlineData(SkillIds.Fireball, "Fireball", 8, 150, 5)]
    [InlineData(SkillIds.Trap, "Trap", 15, 100, 3)]
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
    [InlineData(SkillIds.PowerStrike, "Power Strike")]
    [InlineData(SkillIds.Backstab, "Backstab")]
    [InlineData(SkillIds.Heal, "Heal")]
    [InlineData(SkillIds.None, "")]
    public void GetName_ReturnsCorrectName(int skillId, string expectedName)
    {
        Assert.Equal(expectedName, SkillDefinitions.GetName(skillId));
    }
}
