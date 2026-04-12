using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Clears and rebuilds <see cref="ActiveEffects"/> each tick by inspecting
/// all player components that can produce effects (survival, etc.).
/// Run this after systems that change component state (e.g. SurvivalSystem)
/// and before systems that read effects (e.g. MovementSystem).
/// </summary>
public class ActiveEffectsSystem
{
    public void Update(WorldMap worldMap)
    {
        foreach (ref var player in worldMap.Players)
        {
            if (player.IsDead) continue;

            player.ActiveEffects.Clear();

            ApplySurvivalEffects(ref player);
        }
    }

    private static void ApplySurvivalEffects(ref Entities.PlayerEntity player)
    {
        ref var s = ref player.Survival;

        if (s.IsStarving)
        {
            player.ActiveEffects.Add(new ActiveEffect(EffectType.Hungry, 75));
        }
        if (s.IsDehydrated)
        {
            player.ActiveEffects.Add(new ActiveEffect(EffectType.Thirsty, 75));
        }
    }
}
