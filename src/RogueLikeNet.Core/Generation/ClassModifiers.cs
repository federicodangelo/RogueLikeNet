using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Generation;

/// <summary>
/// Class-specific stat bonuses and starting skill loadouts.
/// </summary>
public static class ClassModifiers
{
    /// <summary>Returns (bonusAttack, bonusDefense, bonusHealth, bonusSpeed).</summary>
    public static (int Atk, int Def, int Hp, int Spd) GetStartingBonus(int classId) => classId switch
    {
        ClassIds.Warrior => (3, 3, 20, 0),
        ClassIds.Rogue   => (1, 0, 0, 4),
        ClassIds.Mage    => (0, 0, -10, 2),
        ClassIds.Ranger  => (2, 1, 0, 2),
        _ => (0, 0, 0, 0),
    };

    public static SkillSlots GetStartingSkills(int classId) => classId switch
    {
        ClassIds.Warrior => new SkillSlots
        {
            Skill0 = SkillIds.PowerStrike,
            Skill1 = SkillIds.ShieldBash,
        },
        ClassIds.Rogue => new SkillSlots
        {
            Skill0 = SkillIds.Backstab,
            Skill1 = SkillIds.Dodge,
        },
        ClassIds.Mage => new SkillSlots
        {
            Skill0 = SkillIds.Fireball,
            Skill1 = SkillIds.Heal,
        },
        ClassIds.Ranger => new SkillSlots
        {
            Skill0 = SkillIds.PowerShot,
            Skill1 = SkillIds.Trap,
        },
        _ => default,
    };
}
