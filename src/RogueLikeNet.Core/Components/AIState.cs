using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

[Component]
public struct AIState
{
    public int StateId;
    public int TargetEntityId;
    public int PatrolX;
    public int PatrolY;
    public int AlertCooldown;
}

public static class AIStates
{
    public const int Idle = 0;
    public const int Patrol = 1;
    public const int Chase = 2;
    public const int Flee = 3;
    public const int Attack = 4;
}
