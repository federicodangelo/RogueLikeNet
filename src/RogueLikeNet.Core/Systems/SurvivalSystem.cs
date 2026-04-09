using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Decrements hunger over time and applies starvation damage.
/// </summary>
public class SurvivalSystem
{
    public const int StarvationDamage = 1;

    public void Update(WorldMap worldMap, bool debugInvulnerable = false)
    {
        if (debugInvulnerable) return;

        foreach (ref var player in worldMap.Players)
        {
            if (player.IsDead) continue;
            UpdateHunger(ref player);
        }
    }

    private static void UpdateHunger(ref Entities.PlayerEntity player)
    {
        ref var s = ref player.Survival;

        if (s.HungerDecayRate <= 0) return;

        s.DecayCounter++;
        if (s.DecayCounter >= s.HungerDecayRate)
        {
            s.DecayCounter = 0;
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
}
