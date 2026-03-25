namespace RogueLikeNet.Core.Components;

/// <summary>
/// Throttles entity movement. Each tick the counter decrements;
/// the entity can only move when it reaches zero, then resets to <see cref="Interval"/>.
/// Lower Interval = faster movement. Players always have Interval 0 (move every tick).
/// </summary>
public struct MoveDelay
{
    /// <summary>Ticks between moves. 0 = move every tick, 1 = every other tick, etc.</summary>
    public int Interval;

    /// <summary>Current countdown. When 0, the entity may move.</summary>
    public int Current;

    public MoveDelay(int interval)
    {
        Interval = interval;
        Current = 0;
    }
}
