using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Systems;

public readonly struct DamageResolution
{
    public DamageResolution(int damage, DamageType damageType, int multiplierBase100)
    {
        Damage = damage;
        DamageType = damageType;
        MultiplierBase100 = multiplierBase100;
    }

    public int Damage { get; }
    public DamageType DamageType { get; }
    public int MultiplierBase100 { get; }
    public bool WasResisted => MultiplierBase100 < 100;
    public bool WasWeakness => MultiplierBase100 > 100;
}


public static class DamageResolver
{
    public const int NormalMultiplierBase100 = 100;

    public static DamageResolution Resolve(
        int attack,
        int defense,
        DamageType damageType = DamageType.Physical,
        int multiplierBase100 = NormalMultiplierBase100)
    {
        int baseDamage = Math.Max(1, attack - defense);

        if (multiplierBase100 <= 0)
            return new DamageResolution(0, damageType, multiplierBase100);

        int adjustedDamage = baseDamage * multiplierBase100 / NormalMultiplierBase100;
        return new DamageResolution(Math.Max(1, adjustedDamage), damageType, multiplierBase100);
    }

    public static DamageResolution ResolveAgainstNpc(
        int attack,
        int defense,
        DamageType damageType,
        int npcTypeId)
    {
        var npcDefinition = GameData.Instance.Npcs.Get(npcTypeId);
        int multiplier = npcDefinition?.GetDamageMultiplierBase100(damageType) ?? NormalMultiplierBase100;
        return Resolve(attack, defense, damageType, multiplier);
    }
}
