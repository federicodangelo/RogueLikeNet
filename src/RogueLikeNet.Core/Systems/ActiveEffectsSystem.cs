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
    internal const int NeutralWeaponAttackSpeed = 0;
    private const int BaseWeaponAttackDelayInterval = 10;

    public void Update(WorldMap worldMap)
    {
        foreach (ref var player in worldMap.Players)
        {
            if (player.IsDead) continue;

            // Tick down temporary effects and remove expired ones
            player.ActiveEffects.TickAndRemoveExpired();

            // Clear only permanent effects (survival), keep temporary ones
            player.ActiveEffects.ClearPermanent();

            // Rebuild permanent effects
            ApplySurvivalEffects(ref player);

            // Always recalculate: delays depend on current effects (speed multiplier)
            RecalculatePlayerStats(ref player);
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
    /// Recalculates player stats from first principles: base class stats + level bonuses + equipment + active effects.
    /// Move delay is derived from class speed and active effects.
    /// Attack delay is derived from the equipped hand weapon's attack speed and active effects.
    /// Call this whenever equipment, effects, potions, or level changes.
    /// </summary>
    public static void RecalculatePlayerStats(ref Entities.PlayerEntity player)
    {
        // 1. Base stats from class
        var classStats = ClassDefinitions.GetStartingStats(player.ClassData.ClassId);
        var baseStats = classStats + ClassDefinitions.BaseStats;

        // 2. Level bonuses
        var levelBonus = ClassDefinitions.GetLevelBonuses(player.ClassData.ClassId, player.ClassData.Level);

        // 3. Equipment bonuses
        int equipAtk = 0, equipDef = 0, equipHp = 0;
        int equipMana = 0;
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
                    if (def.Magic != null)
                        equipMana += def.Magic.BonusMana;
                }
            }
        }

        // 4. Active effect bonuses (potion buffs)
        int effectAtk = player.ActiveEffects.CombinedAttackBonus;
        int effectDef = player.ActiveEffects.CombinedDefenseBonus;

        // Set final combat stats
        player.CombatStats.Attack = baseStats.Attack + levelBonus.Attack + equipAtk + effectAtk;
        player.CombatStats.Defense = baseStats.Defense + levelBonus.Defense + equipDef + effectDef;
        player.CombatStats.Speed = baseStats.Speed + levelBonus.Speed;

        // Recalculate max health
        int newMaxHealth = baseStats.Health + levelBonus.Health + equipHp;
        player.Health.Max = newMaxHealth;
        player.Health.Current = Math.Min(player.Health.Current, player.Health.Max);

        // Recalculate max mana
        int baseMana = ClassDefinitions.GetStartingMana(player.ClassData.ClassId);
        int levelMana = ClassDefinitions.GetLevelManaBonus(player.ClassData.ClassId, player.ClassData.Level);
        int newMaxMana = baseMana + levelMana + equipMana;
        player.Mana.Max = newMaxMana;
        player.Mana.Current = Math.Min(player.Mana.Current, player.Mana.Max);

        // Recalculate move delay and attack delay from speed + effects
        int baseDelay = Math.Max(0, 10 - (6 + classStats.Speed + levelBonus.Speed));
        int speedMult = player.ActiveEffects.CombinedSpeedMultiplierBase100;
        int weaponAttackSpeed = GetEquippedHandAttackSpeed(ref player);

        player.MoveDelay.Interval = ApplySpeedMultiplier(baseDelay, speedMult);
        player.MoveDelay.Current = Math.Min(player.MoveDelay.Current, player.MoveDelay.Interval);
        player.AttackDelay.Interval = ApplySpeedMultiplier(GetAttackDelayIntervalForWeaponSpeed(weaponAttackSpeed), speedMult);
        player.AttackDelay.Current = Math.Min(player.AttackDelay.Current, player.AttackDelay.Interval);
    }

    private static int GetEquippedHandAttackSpeed(ref Entities.PlayerEntity player)
    {
        var handItem = player.Equipment.Hand;
        if (handItem.IsNone)
            return NeutralWeaponAttackSpeed;

        var handDef = GameData.Instance.Items.Get(handItem.ItemTypeId);
        return handDef?.Weapon?.AttackSpeed ?? NeutralWeaponAttackSpeed;
    }

    internal static int GetAttackDelayIntervalForWeaponSpeed(int weaponAttackSpeed)
    {
        return Math.Max(0, BaseWeaponAttackDelayInterval - weaponAttackSpeed);
    }

    /// <summary>
    /// Applies an active-effects speed multiplier (base 100) to a base delay value.
    /// </summary>
    private static int ApplySpeedMultiplier(int baseDelay, int speedMultBase100)
    {
        if (speedMultBase100 < 100 && speedMultBase100 > 0)
        {
            // Slowdown: e.g. 50% speed → double delay
            return Math.Max((baseDelay + 1) * 100 / speedMultBase100 - 1, 1);
        }
        if (speedMultBase100 > 100)
        {
            // Speedup: e.g. 200% speed → half delay
            return Math.Max(baseDelay * 100 / speedMultBase100, 0);
        }
        return baseDelay;
    }
}
