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
