namespace RogueLikeNet.Core.Components;

public enum EffectType
{
    Hungry,
    Thirsty,
    StatsBoost,
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

    /// <summary>Flat attack bonus from this effect.</summary>
    public int AttackBonus;

    /// <summary>Flat defense bonus from this effect.</summary>
    public int DefenseBonus;

    /// <summary>
    /// Remaining ticks for temporary effects. 0 = permanent (logic-based, rebuilt each tick).
    /// </summary>
    public int RemainingTicks;

    public readonly bool IsTemporary => RemainingTicks > 0;

    public ActiveEffect(EffectType type, int speedMultiplierBase100)
    {
        Type = type;
        SpeedMultiplierBase100 = speedMultiplierBase100;
    }

    public ActiveEffect(EffectType type, int speedMultiplierBase100, int attackBonus, int defenseBonus, int remainingTicks)
    {
        Type = type;
        SpeedMultiplierBase100 = speedMultiplierBase100;
        AttackBonus = attackBonus;
        DefenseBonus = defenseBonus;
        RemainingTicks = remainingTicks;
    }
}

public struct ActiveEffects
{
    public const int MaxEffects = 8;

    private ActiveEffect _e0, _e1, _e2, _e3, _e4, _e5, _e6, _e7;
    public int Count;

    public void Clear() => Count = 0;

    /// <summary>
    /// Removes all permanent (non-temporary) effects, keeping temporary ones intact.
    /// </summary>
    public void ClearPermanent()
    {
        int dst = 0;
        for (int src = 0; src < Count; src++)
        {
            var e = Get(src);
            if (e.IsTemporary)
            {
                Set(dst, e);
                dst++;
            }
        }
        Count = dst;
    }

    /// <summary>
    /// Ticks down all temporary effects and removes expired ones.
    /// Returns true if any effect was removed (stats should be recalculated).
    /// </summary>
    public bool TickAndRemoveExpired()
    {
        bool removed = false;
        int dst = 0;
        for (int src = 0; src < Count; src++)
        {
            var e = Get(src);
            if (e.IsTemporary)
            {
                e.RemainingTicks--;
                if (e.RemainingTicks <= 0)
                {
                    removed = true;
                    continue;
                }
            }
            Set(dst, e);
            dst++;
        }
        Count = dst;
        return removed;
    }

    public void Add(ActiveEffect effect)
    {
        if (Count >= MaxEffects) return;
        Set(Count, effect);
        Count++;
    }

    public readonly ActiveEffect Get(int index) => index switch
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

    private void Set(int index, ActiveEffect value)
    {
        switch (index)
        {
            case 0: _e0 = value; break;
            case 1: _e1 = value; break;
            case 2: _e2 = value; break;
            case 3: _e3 = value; break;
            case 4: _e4 = value; break;
            case 5: _e5 = value; break;
            case 6: _e6 = value; break;
            case 7: _e7 = value; break;
        }
    }

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

    /// <summary>Sum of AttackBonus from all active effects.</summary>
    public readonly int CombinedAttackBonus
    {
        get
        {
            int result = 0;
            for (int i = 0; i < Count; i++)
                result += Get(i).AttackBonus;
            return result;
        }
    }

    /// <summary>Sum of DefenseBonus from all active effects.</summary>
    public readonly int CombinedDefenseBonus
    {
        get
        {
            int result = 0;
            for (int i = 0; i < Count; i++)
                result += Get(i).DefenseBonus;
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

    /// <summary>True if any temporary effect is active.</summary>
    public readonly bool HasTemporaryEffects
    {
        get
        {
            for (int i = 0; i < Count; i++)
                if (Get(i).IsTemporary) return true;
            return false;
        }
    }
}
