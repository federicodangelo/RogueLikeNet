namespace RogueLikeNet.Core.Components;

public struct Mana
{
    public int Current;
    public int Max;

    public Mana(int max)
    {
        Current = max;
        Max = max;
    }

    public readonly bool HasEnough(int cost) => Current >= cost;
}
