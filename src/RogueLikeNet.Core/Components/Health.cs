namespace RogueLikeNet.Core.Components;

public struct Health
{
    public int Current;
    public int Max;

    public Health(int max)
    {
        Current = max;
        Max = max;
    }

    public bool IsAlive => Current > 0;
}
