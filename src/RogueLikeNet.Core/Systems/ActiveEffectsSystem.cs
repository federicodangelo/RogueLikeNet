using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Clears and rebuilds permanent (logic-based) <see cref="ActiveEffects"/> each tick,
/// ticks down temporary effects, and recalculates combat stats when effects change.
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

            // Tick down temporary effects and remove expired ones
            bool effectsChanged = player.ActiveEffects.TickAndRemoveExpired();

            // Clear only permanent effects (survival), keep temporary ones
            player.ActiveEffects.ClearPermanent();

            // Rebuild permanent effects
            ApplySurvivalEffects(ref player);

            // Recalculate stats if any temporary effects expired
            if (effectsChanged)
                RecalculateCombatStats(ref player);
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

    /// <summary>
    /// Recalculates combat stats from first principles: base class stats + level bonuses + equipment + active effects.
    /// Call this whenever equipment, effects, potions, or level changes.
    /// </summary>
    public static void RecalculateCombatStats(ref Entities.PlayerEntity player)
    {
        // 1. Base stats from class
        var classStats = ClassDefinitions.GetStartingStats(player.ClassData.ClassId);
        var baseStats = classStats + ClassDefinitions.BaseStats;

        // 2. Level bonuses
        var levelBonus = ClassDefinitions.GetLevelBonuses(player.ClassData.ClassId, player.ClassData.Level);

        // 3. Equipment bonuses
        int equipAtk = 0, equipDef = 0, equipHp = 0;
        for (int i = 0; i < Equipment.SlotCount; i++)
        {
            if (player.Equipment.HasItem(i))
            {
                var def = GameData.Instance.Items.Get(player.Equipment[i].ItemTypeId);
                if (def != null)
                {
                    equipAtk += def.EffectiveAttack;
                    equipDef += def.EffectiveDefense;
                    equipHp += def.BaseHealth;
                }
            }
        }

        // 4. Active effect bonuses (potion buffs)
        int effectAtk = player.ActiveEffects.CombinedAttackBonus;
        int effectDef = player.ActiveEffects.CombinedDefenseBonus;

        // Set final stats
        player.CombatStats.Attack = baseStats.Attack + levelBonus.Attack + equipAtk + effectAtk;
        player.CombatStats.Defense = baseStats.Defense + levelBonus.Defense + equipDef + effectDef;
        player.CombatStats.Speed = baseStats.Speed + levelBonus.Speed;

        // Recalculate max health
        int newMaxHealth = baseStats.Health + levelBonus.Health + equipHp;
        player.Health.Max = newMaxHealth;
        player.Health.Current = Math.Min(player.Health.Current, player.Health.Max);
    }
}
