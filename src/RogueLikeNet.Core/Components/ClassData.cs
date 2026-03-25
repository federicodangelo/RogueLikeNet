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
    public static readonly SkillDefinition[] All =
    [
        new(SkillIds.PowerStrike, "Power Strike", 5, 200, 1),
        new(SkillIds.ShieldBash,  "Shield Bash",  8, 50,  1),
        new(SkillIds.Backstab,    "Backstab",     6, 300, 1),
        new(SkillIds.Dodge,       "Dodge",       10, 100, 1),
        new(SkillIds.Fireball,    "Fireball",     8, 150, 5),
        new(SkillIds.Heal,        "Heal",        12, 100, 1),
        new(SkillIds.PowerShot,   "Power Shot",   6, 180, 5),
        new(SkillIds.Trap,        "Trap",        15, 100, 3),
    ];

    public static SkillDefinition Get(int skillId) =>
        Array.Find(All, d => d.SkillId == skillId);

    public static string GetName(int skillId) => Get(skillId).Name ?? "";

    public static int GetCooldown(int skillId) => Get(skillId).Cooldown;
    public static int GetDamageMultiplier(int skillId) => Get(skillId).DamageMultiplier is 0 ? 100 : Get(skillId).DamageMultiplier;
    public static int GetRange(int skillId) => Get(skillId).Range is 0 ? 1 : Get(skillId).Range;
}

public readonly record struct SkillDefinition(
    int SkillId, string Name, int Cooldown, int DamageMultiplier, int Range);
