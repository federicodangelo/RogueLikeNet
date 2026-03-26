using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
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
