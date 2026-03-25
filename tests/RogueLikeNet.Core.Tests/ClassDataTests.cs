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
}
