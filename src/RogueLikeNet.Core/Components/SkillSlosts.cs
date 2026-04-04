using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Components;

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


    public int GetSkillId(int index) => index switch
    {
        0 => Skill0,
        1 => Skill1,
        2 => Skill2,
        3 => Skill3,
        _ => SkillDefinitions.None,
    };

    public int GetCooldown(int index) => index switch
    {
        0 => Cooldown0,
        1 => Cooldown1,
        2 => Cooldown2,
        3 => Cooldown3,
        _ => 0,
    };

    public void SetCooldown(int index, int value)
    {
        switch (index)
        {
            case 0: Cooldown0 = value; break;
            case 1: Cooldown1 = value; break;
            case 2: Cooldown2 = value; break;
            case 3: Cooldown3 = value; break;
        }
    }
}
