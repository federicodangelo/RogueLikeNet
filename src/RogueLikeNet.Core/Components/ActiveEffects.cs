namespace RogueLikeNet.Core.Components;

public enum EffectType
{
    Hungry,
    Thirsty,
}

public struct ActiveEffect
{
    public EffectType Type;

    /// <summary>
    /// Speed multiplier as a percentage (base 100).
    /// 100 = normal speed, 50 = half speed, 200 = double speed.
    /// Values below 100 slow the player down; above 100 speed them up.
    /// </summary>
    public int SpeedMultiplierBase100;

    public ActiveEffect(EffectType type, int speedMultiplierBase100)
    {
        Type = type;
        SpeedMultiplierBase100 = speedMultiplierBase100;
    }
}

public struct ActiveEffects
{
    public const int MaxEffects = 8;

    private ActiveEffect _e0, _e1, _e2, _e3, _e4, _e5, _e6, _e7;
    public int Count;

    public void Clear() => Count = 0;

    public void Add(ActiveEffect effect)
    {
        if (Count >= MaxEffects) return;
        switch (Count)
        {
            case 0: _e0 = effect; break;
            case 1: _e1 = effect; break;
            case 2: _e2 = effect; break;
            case 3: _e3 = effect; break;
            case 4: _e4 = effect; break;
            case 5: _e5 = effect; break;
            case 6: _e6 = effect; break;
            case 7: _e7 = effect; break;
        }
        Count++;
    }

    private readonly ActiveEffect Get(int index) => index switch
    {
        0 => _e0,
        1 => _e1,
        2 => _e2,
        3 => _e3,
        4 => _e4,
        5 => _e5,
        6 => _e6,
        7 => _e7,
        _ => default,
    };

    /// <summary>
    /// Returns the combined speed multiplier (base 100) from all active effects.
    /// The result is the product of all individual multipliers divided by 100 per extra effect.
    /// E.g. two effects at 50 each => 50 * 50 / 100 = 25.
    /// </summary>
    public readonly int CombinedSpeedMultiplierBase100
    {
        get
        {
            if (Count == 0) return 100;
            int result = 100;
            for (int i = 0; i < Count; i++)
            {
                result = result * Get(i).SpeedMultiplierBase100 / 100;
            }
            return result;
        }
    }

    public readonly bool HasEffect(EffectType type)
    {
        for (int i = 0; i < Count; i++)
        {
            if (Get(i).Type == type) return true;
        }
        return false;
    }
}
