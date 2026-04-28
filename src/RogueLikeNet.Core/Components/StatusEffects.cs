using RogueLikeNet.Core.Data;

namespace RogueLikeNet.Core.Components;

public enum StatusEffectType
{
    None = -1,
    Burning = 0,
    Poisoned = 1,
    Chilled = 2,
}

public struct StatusEffect
{
    public StatusEffectType Type;
    public DamageType DamageType;
    public int DamagePerTick;
    public int TickInterval;
    public int TickCounter;
    public int RemainingTicks;
    public int SpeedMultiplierBase100;
    public int SourcePlayerEntityId;

    public readonly bool DealsDamage => DamagePerTick > 0 && TickInterval > 0;
}

public struct StatusEffects
{
    public const int MaxEffects = 4;

    private StatusEffect _e0, _e1, _e2, _e3;
    public int Count;

    public readonly bool HasAny => Count > 0;

    public void Clear() => Count = 0;

    public readonly StatusEffect Get(int index) => index switch
    {
        0 => _e0,
        1 => _e1,
        2 => _e2,
        3 => _e3,
        _ => default,
    };

    public void Set(int index, StatusEffect effect)
    {
        switch (index)
        {
            case 0: _e0 = effect; break;
            case 1: _e1 = effect; break;
            case 2: _e2 = effect; break;
            case 3: _e3 = effect; break;
        }
    }

    public void RemoveAt(int index)
    {
        for (int i = index; i < Count - 1; i++)
            Set(i, Get(i + 1));
        Count--;
    }

    public void AddOrRefresh(StatusEffect effect)
    {
        for (int i = 0; i < Count; i++)
        {
            var current = Get(i);
            if (current.Type != effect.Type) continue;

            if (effect.DamagePerTick >= current.DamagePerTick)
            {
                current.DamagePerTick = effect.DamagePerTick;
                current.SourcePlayerEntityId = effect.SourcePlayerEntityId;
            }

            current.DamageType = effect.DamageType;
            current.TickInterval = effect.TickInterval;
            current.TickCounter = current.TickCounter <= 0 ? effect.TickInterval : current.TickCounter;
            current.RemainingTicks = Math.Max(current.RemainingTicks, effect.RemainingTicks);
            current.SpeedMultiplierBase100 = effect.SpeedMultiplierBase100;
            Set(i, current);
            return;
        }

        if (Count >= MaxEffects) return;
        Set(Count, effect);
        Count++;
    }

    public readonly bool HasEffect(StatusEffectType type)
    {
        for (int i = 0; i < Count; i++)
        {
            if (Get(i).Type == type) return true;
        }
        return false;
    }

    public readonly int CombinedSpeedMultiplierBase100
    {
        get
        {
            int result = 100;
            for (int i = 0; i < Count; i++)
            {
                int multiplier = Get(i).SpeedMultiplierBase100;
                if (multiplier > 0)
                    result = result * multiplier / 100;
            }
            return result;
        }
    }
}
