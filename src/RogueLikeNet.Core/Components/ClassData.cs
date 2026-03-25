namespace RogueLikeNet.Core.Components;

public struct ClassData
{
    public int ClassId;
    public int Level;
    public int Experience;
}

public static class ClassIds
{
    public const int Warrior = 0;
    public const int Rogue = 1;
    public const int Mage = 2;
    public const int Ranger = 3;
}

public struct SkillSlots
{
    public int Skill0;
    public int Skill1;
    public int Skill2;
    public int Skill3;
    public int Cooldown0;
    public int Cooldown1;
    public int Cooldown2;
    public int Cooldown3;
}

public static class SkillIds
{
    public const int None = 0;
    // Warrior
    public const int PowerStrike = 1;   // 2x damage, 5 tick cooldown
    public const int ShieldBash = 2;    // stun + small damage, 8 tick cooldown
    // Rogue
    public const int Backstab = 3;      // 3x damage if behind target, 6 tick cooldown
    public const int Dodge = 4;         // avoid next attack, 10 tick cooldown
    // Mage
    public const int Fireball = 5;      // AoE damage 3x3, 8 tick cooldown
    public const int Heal = 6;          // restore 30 HP, 12 tick cooldown
    // Ranger
    public const int PowerShot = 7;     // ranged attack up to 5 tiles, 6 tick cooldown
    public const int Trap = 8;          // place a trap at target tile, 15 tick cooldown
}

public static class SkillDefinitions
{
    public static int GetCooldown(int skillId) => skillId switch
    {
        SkillIds.PowerStrike => 5,
        SkillIds.ShieldBash => 8,
        SkillIds.Backstab => 6,
        SkillIds.Dodge => 10,
        SkillIds.Fireball => 8,
        SkillIds.Heal => 12,
        SkillIds.PowerShot => 6,
        SkillIds.Trap => 15,
        _ => 0,
    };

    public static int GetDamageMultiplier(int skillId) => skillId switch
    {
        SkillIds.PowerStrike => 200, // percent
        SkillIds.ShieldBash => 50,
        SkillIds.Backstab => 300,
        SkillIds.Fireball => 150,
        SkillIds.PowerShot => 180,
        _ => 100,
    };

    public static int GetRange(int skillId) => skillId switch
    {
        SkillIds.Fireball => 5,
        SkillIds.PowerShot => 5,
        SkillIds.Trap => 3,
        _ => 1,
    };
}
