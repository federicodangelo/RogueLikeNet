using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct CombatStats
{
    public int Attack;
    public int Defense;
    public int Speed;

    public CombatStats(int attack, int defense, int speed)
    {
        Attack = attack;
        Defense = defense;
        Speed = speed;
    }
}
