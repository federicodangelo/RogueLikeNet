using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

public sealed class StatusEffectSystem
{
    private const int BurnDurationTicks = 60;
    private const int BurnTickInterval = 20;
    private const int PoisonDurationTicks = 90;
    private const int PoisonTickInterval = 30;
    private const int ChillDurationTicks = 60;
    private const int ChillSpeedMultiplierBase100 = 50;

    private readonly List<CombatEvent> _events = new();

    public IReadOnlyList<CombatEvent> LastTickEvents => _events;

    internal static int GetMonsterMoveDelayInterval(int speed) => Math.Max(0, 10 - speed);

    internal static int GetMonsterAttackDelayInterval(int attackSpeed) => Math.Max(0, 10 - attackSpeed);

    public void Update(WorldMap map)
    {
        _events.Clear();

        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var monster in chunk.Monsters)
            {
                if (monster.IsDead)
                {
                    monster.StatusEffects.Clear();
                    continue;
                }

                if (!monster.StatusEffects.HasAny)
                    continue;

                TickMonsterStatuses(map, ref monster);
                RecalculateMonsterDelays(ref monster);
            }
        }
    }

    public static bool TryApplyFromDamageType(
        ref MonsterEntity monster,
        DamageType damageType,
        int sourcePlayerEntityId,
        int impactDamage,
        out StatusEffectType appliedEffectType)
    {
        appliedEffectType = StatusEffectType.None;
        if (impactDamage <= 0 || monster.IsDead) return false;

        StatusEffect effect = damageType switch
        {
            DamageType.Fire => new StatusEffect
            {
                Type = StatusEffectType.Burning,
                DamageType = DamageType.Fire,
                DamagePerTick = Math.Max(1, impactDamage / 4),
                TickInterval = BurnTickInterval,
                TickCounter = BurnTickInterval,
                RemainingTicks = BurnDurationTicks,
                SpeedMultiplierBase100 = 100,
                SourcePlayerEntityId = sourcePlayerEntityId,
            },
            DamageType.Poison => new StatusEffect
            {
                Type = StatusEffectType.Poisoned,
                DamageType = DamageType.Poison,
                DamagePerTick = Math.Max(1, impactDamage / 5),
                TickInterval = PoisonTickInterval,
                TickCounter = PoisonTickInterval,
                RemainingTicks = PoisonDurationTicks,
                SpeedMultiplierBase100 = 100,
                SourcePlayerEntityId = sourcePlayerEntityId,
            },
            DamageType.Ice => new StatusEffect
            {
                Type = StatusEffectType.Chilled,
                DamageType = DamageType.Ice,
                DamagePerTick = 0,
                TickInterval = 0,
                TickCounter = 0,
                RemainingTicks = ChillDurationTicks,
                SpeedMultiplierBase100 = ChillSpeedMultiplierBase100,
                SourcePlayerEntityId = sourcePlayerEntityId,
            },
            _ => new StatusEffect { Type = StatusEffectType.None },
        };

        if (effect.Type == StatusEffectType.None) return false;

        monster.StatusEffects.AddOrRefresh(effect);
        RecalculateMonsterDelays(ref monster);
        appliedEffectType = effect.Type;
        return true;
    }

    public static void RecalculateMonsterDelays(ref MonsterEntity monster)
    {
        int moveBaseInterval = GetMonsterMoveDelayInterval(monster.MonsterData.Speed);
        int attackBaseInterval = GetMonsterAttackDelayInterval(monster.MonsterData.AttackSpeed);
        int speedMultiplier = monster.StatusEffects.CombinedSpeedMultiplierBase100;
        monster.MoveDelay.Interval = ApplySpeedMultiplier(moveBaseInterval, speedMultiplier);
        monster.MoveDelay.Current = Math.Min(monster.MoveDelay.Current, monster.MoveDelay.Interval);
        monster.AttackDelay.Interval = ApplySpeedMultiplier(attackBaseInterval, speedMultiplier);
        monster.AttackDelay.Current = Math.Min(monster.AttackDelay.Current, monster.AttackDelay.Interval);
    }

    private void TickMonsterStatuses(WorldMap map, ref MonsterEntity monster)
    {
        for (int i = monster.StatusEffects.Count - 1; i >= 0; i--)
        {
            var effect = monster.StatusEffects.Get(i);
            effect.RemainingTicks--;

            if (effect.DealsDamage)
            {
                effect.TickCounter--;
                if (effect.TickCounter <= 0)
                {
                    effect.TickCounter = effect.TickInterval;
                    ApplyStatusDamage(map, ref monster, effect);
                    if (monster.IsDead)
                    {
                        monster.StatusEffects.Clear();
                        return;
                    }
                }
            }

            if (effect.RemainingTicks <= 0)
                monster.StatusEffects.RemoveAt(i);
            else
                monster.StatusEffects.Set(i, effect);
        }
    }

    private void ApplyStatusDamage(WorldMap map, ref MonsterEntity monster, StatusEffect effect)
    {
        monster.Health.Current = Math.Max(0, monster.Health.Current - effect.DamagePerTick);
        var attacker = GetSourcePlayerPosition(map, effect.SourcePlayerEntityId) ?? monster.Position;

        _events.Add(new CombatEvent
        {
            Attacker = attacker,
            Target = monster.Position,
            Damage = effect.DamagePerTick,
            TargetDied = monster.IsDead,
            DamageType = effect.DamageType,
            StatusEffectType = effect.Type,
        });

        if (monster.IsDead)
            AwardKillToSource(map, ref monster, effect.SourcePlayerEntityId);
    }

    private static Position? GetSourcePlayerPosition(WorldMap map, int sourcePlayerEntityId)
    {
        foreach (ref var player in map.Players)
        {
            if (player.Id == sourcePlayerEntityId && !player.IsDead)
                return player.Position;
        }

        return null;
    }

    private static void AwardKillToSource(WorldMap map, ref MonsterEntity monster, int sourcePlayerEntityId)
    {
        if (sourcePlayerEntityId <= 0) return;

        foreach (ref var player in map.Players)
        {
            if (player.Id != sourcePlayerEntityId || player.IsDead) continue;

            player.ActionEvents.Add(new PlayerActionEvent
            {
                EventType = PlayerActionEventType.Kill,
                KilledNpcTypeId = monster.MonsterData.MonsterTypeId,
            });

            if (player.ClassData.Level < GameData.Instance.PlayerLevels.MaxLevel)
            {
                var npcDef = GameData.Instance.Npcs.Get(monster.MonsterData.MonsterTypeId);
                if (npcDef != null)
                {
                    player.ClassData.Experience += npcDef.XpReward;
                    ProcessLevelUp(ref player);
                }
            }

            return;
        }
    }

    private static void ProcessLevelUp(ref PlayerEntity player)
    {
        var levelTable = GameData.Instance.PlayerLevels;
        int newLevel = levelTable.GetLevelForXp(player.ClassData.Experience);
        if (newLevel <= player.ClassData.Level) return;

        int oldLevel = player.ClassData.Level;
        player.ClassData.Level = newLevel;
        player.ClassData.Experience = 0;

        ActiveEffectsSystem.RecalculatePlayerStats(ref player);

        player.Health.Current = player.Health.Max;
        player.Mana.Current = player.Mana.Max;

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.LevelUp,
            OldLevel = oldLevel,
            NewLevel = newLevel,
        });
    }

    private static int ApplySpeedMultiplier(int baseDelay, int speedMultBase100)
    {
        if (speedMultBase100 < 100 && speedMultBase100 > 0)
            return Math.Max((baseDelay + 1) * 100 / speedMultBase100 - 1, 1);

        if (speedMultBase100 > 100)
            return Math.Max(baseDelay * 100 / speedMultBase100, 0);

        return baseDelay;
    }
}
