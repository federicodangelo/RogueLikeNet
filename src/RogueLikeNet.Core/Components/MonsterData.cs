namespace RogueLikeNet.Core.Components;

/// <summary>
/// Component for monster entities — carries all stats so spawning requires no external definition lookup.
/// </summary>
public struct MonsterData
{
    public int MonsterTypeId;
    public int Health;
    public int Attack;
    public int Defense;
    public int Speed;
}
