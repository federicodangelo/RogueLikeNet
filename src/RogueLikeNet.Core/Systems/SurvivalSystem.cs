using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Decrements hunger and thirst over time, applies starvation/dehydration damage,
/// and handles regen bonus. Active effects (speed penalties etc.) are handled by
/// <see cref="ActiveEffectsSystem"/>.
/// </summary>
public class SurvivalSystem
{
    public const int StarvationDamage = 1;
    public const int DehydrationDamage = 1;
    public const int RegenAmount = 1;
    public const int RegenTickInterval = 20 * 10; // ticks between regen heals when well-fed and hydrated (about 10 seconds at 20 TPS)

    private int _regenCounter;

    public void Update(WorldMap worldMap, bool debugInvulnerable = false)
    {
        if (debugInvulnerable) return;

        _regenCounter++;

        foreach (ref var player in worldMap.Players)
        {
            if (player.IsDead) continue;
            UpdateHunger(ref player);
            UpdateThirst(ref player);
            ApplyDamageAndRegen(ref player);
        }
    }

    private static void UpdateHunger(ref Entities.PlayerEntity player)
    {
        ref var s = ref player.Survival;

        if (s.HungerDecayRate <= 0) return;

        s.HungerDecayCounter++;
        if (s.HungerDecayCounter >= s.HungerDecayRate)
        {
            s.HungerDecayCounter = 0;
            if (s.Hunger > 0)
            {
                s.Hunger--;
            }
            else
            {
                if (s.IsStarving)
                {
                    // Starvation damage
                    player.Health.Current = Math.Max(0, player.Health.Current - StarvationDamage);
                }
            }
        }
    }

    private static void UpdateThirst(ref Entities.PlayerEntity player)
    {
        ref var s = ref player.Survival;

        if (s.ThirstDecayRate <= 0) return;

        s.ThirstDecayCounter++;
        if (s.ThirstDecayCounter >= s.ThirstDecayRate)
        {
            s.ThirstDecayCounter = 0;
            if (s.Thirst > 0)
            {
                s.Thirst--;
            }
            else
            {
                if (s.IsDehydrated)
                {
                    // Dehydration damage
                    player.Health.Current = Math.Max(0, player.Health.Current - DehydrationDamage);
                }
            }
        }
    }

    private void ApplyDamageAndRegen(ref Entities.PlayerEntity player)
    {
        ref var s = ref player.Survival;

        // Damage-over-time when starving (<20) or dehydrated (<20)
        if (s.Hunger > 0 && s.IsStarving)
        {
            if (s.HungerDecayCounter == 0)
                player.Health.Current = Math.Max(0, player.Health.Current - StarvationDamage);
        }
        if (s.Thirst > 0 && s.IsDehydrated)
        {
            if (s.ThirstDecayCounter == 0)
                player.Health.Current = Math.Max(0, player.Health.Current - DehydrationDamage);
        }

        // Regen bonus when well-fed (>80) AND well-hydrated (>80)
        if (s.IsWellFed && s.IsWellHydrated)
        {
            if (_regenCounter % RegenTickInterval == 0)
            {
                player.Health.Current = Math.Min(player.Health.Max, player.Health.Current + RegenAmount);
            }
        }
    }
}
