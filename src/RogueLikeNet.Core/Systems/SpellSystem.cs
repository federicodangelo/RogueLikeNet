using RogueLikeNet.Core.Algorithms;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Data;
using RogueLikeNet.Core.Entities;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

public class SpellSystem
{
    private readonly List<CombatEvent> _events = new();
    private readonly Dictionary<long, Dictionary<int, int>> _cooldowns = new();

    public IReadOnlyList<CombatEvent> LastTickEvents => _events;

    public void Update(WorldMap map)
    {
        _events.Clear();
        TickCooldowns();

        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.CastSpell) continue;

            ProcessCastSpell(ref player, map);
        }
    }

    private void ProcessCastSpell(ref PlayerEntity player, WorldMap map)
    {
        int spellNumericId = player.Input.ItemSlot;
        var spell = GameData.Instance.Spells.Get(spellNumericId);
        if (spell == null) return;

        player.Input.ActionType = ActionTypes.None;

        // Check cooldown
        if (IsOnCooldown(player.Id, spellNumericId))
        {
            player.ActionEvents.Add(new PlayerActionEvent
            {
                EventType = PlayerActionEventType.CastSpell,
                Failed = true,
                FailReason = ActionFailReason.SpellOnCooldown,
            });
            return;
        }

        // Check mana
        int manaCost = spell.ManaCost;
        int bonusDamage = GetEquippedSpellBonus(ref player);

        if (!player.Mana.HasEnough(manaCost))
        {
            player.ActionEvents.Add(new PlayerActionEvent
            {
                EventType = PlayerActionEventType.CastSpell,
                Failed = true,
                FailReason = ActionFailReason.InsufficientMana,
            });
            return;
        }

        // Deduct mana and start cooldown
        player.Mana.Current -= manaCost;
        SetCooldown(player.Id, spellNumericId, spell.CooldownTicks);

        switch (spell.TargetType)
        {
            case SpellTargetType.Self:
                ApplySelfSpell(ref player, spell);
                break;
            case SpellTargetType.SingleTarget:
                ApplySingleTarget(ref player, map, spell, bonusDamage);
                break;
            case SpellTargetType.AreaOfEffect:
                ApplyAoE(ref player, map, spell, bonusDamage);
                break;
        }

        player.ActionEvents.Add(new PlayerActionEvent
        {
            EventType = PlayerActionEventType.CastSpell,
            ItemTypeId = spellNumericId,
        });
    }

    private static void ApplySelfSpell(ref PlayerEntity player, SpellDefinition spell)
    {
        if (spell.HealAmount > 0)
        {
            player.Health.Current = Math.Min(player.Health.Max, player.Health.Current + spell.HealAmount);
        }

        if (spell.BuffAttack > 0 || spell.BuffDefense > 0)
        {
            player.ActiveEffects.Add(new ActiveEffect(
                EffectType.StatsBoost,
                100,
                spell.BuffAttack,
                spell.BuffDefense,
                spell.BuffDurationTicks));
        }
    }

    private void ApplySingleTarget(ref PlayerEntity player, WorldMap map, SpellDefinition spell, int bonusDamage)
    {
        Position targetPos;
        if (player.Input.TargetX != 0 || player.Input.TargetY != 0)
        {
            targetPos = Position.FromCoords(
                player.Position.X + player.Input.TargetX,
                player.Position.Y + player.Input.TargetY,
                player.Position.Z);
        }
        else
        {
            var found = FindClosestTarget(map, player, spell.Range);
            if (found == null) return;
            targetPos = found.Value;
        }

        // Verify LOS
        int playerZ = player.Position.Z;
        if (!Bresenham.HasLineOfSight(
            player.Position.X, player.Position.Y,
            targetPos.X, targetPos.Y,
            (x, y) => !map.IsTransparent(Position.FromCoords(x, y, playerZ))))
        {
            return;
        }

        int damage = Math.Max(1, spell.BaseDamage + bonusDamage);
        DamageAtPosition(ref player, map, targetPos, damage);
    }

    private void ApplyAoE(ref PlayerEntity player, WorldMap map, SpellDefinition spell, int bonusDamage)
    {
        Position centerPos;
        if (player.Input.TargetX != 0 || player.Input.TargetY != 0)
        {
            centerPos = Position.FromCoords(
                player.Position.X + player.Input.TargetX,
                player.Position.Y + player.Input.TargetY,
                player.Position.Z);
        }
        else
        {
            var found = FindClosestTarget(map, player, spell.Range);
            if (found == null) return;
            centerPos = found.Value;
        }

        // Verify LOS to center
        int playerZ = player.Position.Z;
        if (!Bresenham.HasLineOfSight(
            player.Position.X, player.Position.Y,
            centerPos.X, centerPos.Y,
            (x, y) => !map.IsTransparent(Position.FromCoords(x, y, playerZ))))
        {
            return;
        }

        int damage = Math.Max(1, spell.BaseDamage + bonusDamage);
        int radius = spell.AoERadius;

        // Damage all monsters within AoE radius of center
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var monster in chunk.Monsters)
            {
                if (monster.IsDead || monster.Position.Z != centerPos.Z) continue;
                int dx = Math.Abs(monster.Position.X - centerPos.X);
                int dy = Math.Abs(monster.Position.Y - centerPos.Y);
                if (dx <= radius && dy <= radius)
                {
                    monster.Health.Current = Math.Max(0, monster.Health.Current - damage);
                    _events.Add(new CombatEvent
                    {
                        Attacker = player.Position,
                        Target = monster.Position,
                        Damage = damage,
                        TargetDied = monster.IsDead,
                        IsRanged = true,
                    });

                    if (monster.IsDead)
                    {
                        player.ActionEvents.Add(new PlayerActionEvent
                        {
                            EventType = PlayerActionEventType.Kill,
                            KilledNpcTypeId = monster.MonsterData.MonsterTypeId,
                        });
                    }
                }
            }
        }
    }

    private void DamageAtPosition(ref PlayerEntity player, WorldMap map, Position target, int damage)
    {
        var chunk = map.GetChunkForWorldPos(target);
        if (chunk == null) return;

        foreach (ref var monster in chunk.Monsters)
        {
            if (monster.IsDead || monster.Position != target) continue;

            monster.Health.Current = Math.Max(0, monster.Health.Current - damage);
            _events.Add(new CombatEvent
            {
                Attacker = player.Position,
                Target = monster.Position,
                Damage = damage,
                TargetDied = monster.IsDead,
                IsRanged = true,
            });

            if (monster.IsDead)
            {
                player.ActionEvents.Add(new PlayerActionEvent
                {
                    EventType = PlayerActionEventType.Kill,
                    KilledNpcTypeId = monster.MonsterData.MonsterTypeId,
                });
            }
            break;
        }
    }

    private static Position? FindClosestTarget(WorldMap map, PlayerEntity player, int range)
    {
        Position? best = null;
        int bestDist = int.MaxValue;

        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var monster in chunk.Monsters)
            {
                if (monster.IsDead || monster.Position.Z != player.Position.Z) continue;
                int dx = Math.Abs(monster.Position.X - player.Position.X);
                int dy = Math.Abs(monster.Position.Y - player.Position.Y);
                int dist = dx + dy;
                if (dist > range || dist >= bestDist) continue;

                int playerZ = player.Position.Z;
                if (!Bresenham.HasLineOfSight(
                    player.Position.X, player.Position.Y,
                    monster.Position.X, monster.Position.Y,
                    (x, y) => !map.IsTransparent(Position.FromCoords(x, y, playerZ))))
                    continue;

                bestDist = dist;
                best = monster.Position;
            }
        }

        return best;
    }

    private static int GetEquippedSpellBonus(ref PlayerEntity player)
    {
        var handItem = player.Equipment.Hand;
        if (handItem.IsNone) return 0;
        var def = GameData.Instance.Items.Get(handItem.ItemTypeId);
        return def?.Magic?.BonusSpellDamage ?? 0;
    }

    private void TickCooldowns()
    {
        List<long>? toRemove = null;
        foreach (var (playerId, spells) in _cooldowns)
        {
            List<int>? spellsToRemove = null;
            foreach (var (spellId, remaining) in spells)
            {
                if (remaining <= 1)
                {
                    spellsToRemove ??= new();
                    spellsToRemove.Add(spellId);
                }
                else
                {
                    spells[spellId] = remaining - 1;
                }
            }
            if (spellsToRemove != null)
                foreach (var s in spellsToRemove)
                    spells.Remove(s);
            if (spells.Count == 0)
            {
                toRemove ??= new();
                toRemove.Add(playerId);
            }
        }
        if (toRemove != null)
            foreach (var id in toRemove)
                _cooldowns.Remove(id);
    }

    private bool IsOnCooldown(int playerId, int spellNumericId)
    {
        return _cooldowns.TryGetValue(playerId, out var spells) && spells.ContainsKey(spellNumericId);
    }

    private void SetCooldown(int playerId, int spellNumericId, int ticks)
    {
        if (!_cooldowns.TryGetValue(playerId, out var spells))
        {
            spells = new Dictionary<int, int>();
            _cooldowns[playerId] = spells;
        }
        spells[spellNumericId] = ticks;
    }
}
