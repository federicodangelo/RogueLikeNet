using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

/// <summary>
/// Throttles entity attacks. Each tick the counter decrements;
/// the entity can only attack when it reaches zero, then resets to <see cref="Interval"/>.
/// Lower Interval = faster attack. Interval 0 = attack every tick.
/// </summary>
[Component]
public struct AttackDelay
{
    /// <summary>Ticks between attacks. 0 = attack every tick, 1 = every other tick, etc.</summary>
    public int Interval;

    /// <summary>Current countdown. When 0, the entity may attack.</summary>
    public int Current;

    public AttackDelay(int interval)
    {
        Interval = interval;
        Current = 0;
    }
}
