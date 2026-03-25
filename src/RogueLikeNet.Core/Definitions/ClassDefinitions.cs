using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Definitions;

/// <summary>
/// Class-specific stat bonuses and starting skill loadouts.
/// </summary>
public static class ClassDefinitions
{
    public const int Warrior = 0;
    public const int Rogue = 1;
    public const int Mage = 2;
    public const int Ranger = 3;

    public static readonly ClassDefinition[] All =
    [
        new(Warrior, "Warrior", 3, 3, 20, 0, SkillDefinitions.PowerStrike, SkillDefinitions.ShieldBash),
        new(Rogue,   "Rogue",   1, 0, 0, 4, SkillDefinitions.Backstab, SkillDefinitions.Dodge),
        new(Mage,    "Mage",    0, 0, -10, 2, SkillDefinitions.Fireball, SkillDefinitions.Heal),
        new(Ranger,  "Ranger",  2, 1, 0, 2, SkillDefinitions.PowerShot, SkillDefinitions.Trap),
    ];

    public static ClassDefinition Get(int classId) =>
        Array.Find(All, d => d.ClassId == classId);

    /// <summary>Returns (bonusAttack, bonusDefense, bonusHealth, bonusSpeed).</summary>
    public static (int Atk, int Def, int Hp, int Spd) GetStartingBonus(int classId)
    {
        var def = Get(classId);
        return (def.BonusAttack, def.BonusDefense, def.BonusHealth, def.BonusSpeed);
    }

    public static SkillSlots GetStartingSkills(int classId)
    {
        var def = Get(classId);
        return new SkillSlots()
        {
            Skill0 = def.StartingSkill0,
            Skill1 = def.StartingSkill1,
        };
    }
}

public readonly record struct ClassDefinition(
    int ClassId, string Name, int BonusAttack, int BonusDefense, int BonusHealth, int BonusSpeed, int StartingSkill0, int StartingSkill1
);
