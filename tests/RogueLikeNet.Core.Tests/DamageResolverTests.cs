using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Systems;

namespace RogueLikeNet.Core.Tests;

public class DamageResolverTests
{
    [Fact]
    public void Resolve_DefaultPhysicalDamage_UsesAttackMinusDefense()
    {
        var result = DamageResolver.Resolve(attack: 12, defense: 5);

        Assert.Equal(7, result.Damage);
        Assert.Equal(DamageType.Physical, result.DamageType);
        Assert.False(result.WasResisted);
        Assert.False(result.WasWeakness);
    }

    [Fact]
    public void Resolve_Resistance_ReducesDamageAndFlagsResult()
    {
        var result = DamageResolver.Resolve(attack: 20, defense: 4, damageType: DamageType.Fire, multiplierBase100: 50);

        Assert.Equal(8, result.Damage);
        Assert.True(result.WasResisted);
        Assert.False(result.WasWeakness);
    }

    [Fact]
    public void Resolve_Weakness_IncreasesDamageAndFlagsResult()
    {
        var result = DamageResolver.Resolve(attack: 20, defense: 4, damageType: DamageType.Ice, multiplierBase100: 150);

        Assert.Equal(24, result.Damage);
        Assert.False(result.WasResisted);
        Assert.True(result.WasWeakness);
    }

    [Fact]
    public void Resolve_Immunity_AllowsZeroDamage()
    {
        var result = DamageResolver.Resolve(attack: 20, defense: 4, damageType: DamageType.Poison, multiplierBase100: 0);

        Assert.Equal(0, result.Damage);
        Assert.True(result.WasResisted);
        Assert.False(result.WasWeakness);
    }
}
