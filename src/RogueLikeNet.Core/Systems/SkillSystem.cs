using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes skill usage and cooldown ticking.
/// Handles UseSkill actions from PlayerInput.
/// </summary>
public class SkillSystem
{
    public void Update(Arch.Core.World world)
    {
        TickCooldowns(world);
        ProcessSkillUse(world);
    }

    private void TickCooldowns(Arch.Core.World world)
    {
        world.Query(in GameQueries.SkillCooldowns, (ref SkillSlots slots) =>
        {
            if (slots.Cooldown0 > 0) slots.Cooldown0--;
            if (slots.Cooldown1 > 0) slots.Cooldown1--;
            if (slots.Cooldown2 > 0) slots.Cooldown2--;
            if (slots.Cooldown3 > 0) slots.Cooldown3--;
        });
    }

    private void ProcessSkillUse(Arch.Core.World world)
    {
        var actions = new List<(Entity Player, int SkillSlot, int TargetX, int TargetY)>();

        world.Query(in GameQueries.PlayerSkillUse, (Entity player, ref PlayerInput input) =>
        {
            if (input.ActionType != ActionTypes.UseSkill) return;
            actions.Add((player, input.ItemSlot, input.TargetX, input.TargetY));
            input.ActionType = ActionTypes.None;
        });

        foreach (var (player, skillSlot, tx, ty) in actions)
        {
            if (!world.IsAlive(player)) continue;

            ref var slots = ref world.Get<SkillSlots>(player);
            int skillId = GetSkillId(ref slots, skillSlot);
            if (skillId == SkillDefinitions.None) continue;

            int cooldown = GetCooldown(ref slots, skillSlot);
            if (cooldown > 0) continue; // still on cooldown

            ref var pos = ref world.Get<Position>(player);
            ref var stats = ref world.Get<CombatStats>(player);

            int range = SkillDefinitions.GetRange(skillId);
            int targetX = pos.X + tx;
            int targetY = pos.Y + ty;

            bool used = skillId switch
            {
                SkillDefinitions.PowerStrike => ExecuteMelee(world, player, ref stats, targetX, targetY, 200),
                SkillDefinitions.ShieldBash => ExecuteMelee(world, player, ref stats, targetX, targetY, 50),
                SkillDefinitions.Backstab => ExecuteMelee(world, player, ref stats, targetX, targetY, 300),
                SkillDefinitions.Dodge => ExecuteDodge(world, player, ref stats),
                SkillDefinitions.Fireball => ExecuteAoe(world, ref stats, targetX, targetY, 150, 1),
                SkillDefinitions.Heal => ExecuteHeal(world, player),
                SkillDefinitions.PowerShot => ExecuteRanged(world, ref stats, pos, targetX, targetY, range, 180),
                SkillDefinitions.Trap => false, // TODO: place trap entity
                _ => false,
            };

            if (used)
            {
                SetCooldown(ref slots, skillSlot, SkillDefinitions.GetCooldown(skillId));
            }
        }
    }

    private static bool ExecuteMelee(Arch.Core.World world, Entity attacker, ref CombatStats stats,
        int targetX, int targetY, int damagePercent)
    {
        bool hit = false;
        int attackerAttack = stats.Attack;
        world.Query(in GameQueries.AliveCombatTargets, (Entity target, ref Position tPos, ref Health tHealth, ref CombatStats tStats) =>
        {
            if (tPos.X == targetX && tPos.Y == targetY && target != attacker)
            {
                int baseDamage = Math.Max(1, attackerAttack - tStats.Defense);
                int damage = Math.Max(1, baseDamage * damagePercent / 100);
                tHealth.Current = Math.Max(0, tHealth.Current - damage);
                hit = true;
            }
        });
        return hit;
    }

    private static bool ExecuteDodge(Arch.Core.World world, Entity player, ref CombatStats stats)
    {
        // Temporary defense boost (+10) for one action
        stats.Defense += 10;
        return true;
    }

    private static bool ExecuteAoe(Arch.Core.World world, ref CombatStats stats,
        int centerX, int centerY, int damagePercent, int radius)
    {
        bool hitAny = false;
        int attackerAttack = stats.Attack;
        world.Query(in GameQueries.AliveEnemyCombatTargets, (ref Position tPos, ref Health tHealth, ref CombatStats tStats) =>
        {
            if (Math.Abs(tPos.X - centerX) <= radius && Math.Abs(tPos.Y - centerY) <= radius)
            {
                int baseDamage = Math.Max(1, attackerAttack - tStats.Defense);
                int damage = Math.Max(1, baseDamage * damagePercent / 100);
                tHealth.Current = Math.Max(0, tHealth.Current - damage);
                hitAny = true;
            }
        });
        return hitAny;
    }

    private static bool ExecuteHeal(Arch.Core.World world, Entity player)
    {
        if (!world.Has<Health>(player)) return false;
        ref var health = ref world.Get<Health>(player);
        health.Current = Math.Min(health.Max, health.Current + 30);
        return true;
    }

    private static bool ExecuteRanged(Arch.Core.World world, ref CombatStats stats,
        Position origin, int targetX, int targetY, int maxRange, int damagePercent)
    {
        int dist = Position.ChebyshevDistance(origin, new Position(targetX, targetY, origin.Z));
        if (dist > maxRange) return false;

        bool hit = false;
        int attackerAttack = stats.Attack;
        world.Query(in GameQueries.AliveEnemyCombatTargets, (ref Position tPos, ref Health tHealth, ref CombatStats tStats) =>
        {
            if (tPos.X == targetX && tPos.Y == targetY)
            {
                int baseDamage = Math.Max(1, attackerAttack - tStats.Defense);
                int damage = Math.Max(1, baseDamage * damagePercent / 100);
                tHealth.Current = Math.Max(0, tHealth.Current - damage);
                hit = true;
            }
        });
        return hit;
    }

    private static int GetSkillId(ref SkillSlots slots, int index) => index switch
    {
        0 => slots.Skill0,
        1 => slots.Skill1,
        2 => slots.Skill2,
        3 => slots.Skill3,
        _ => SkillDefinitions.None,
    };

    private static int GetCooldown(ref SkillSlots slots, int index) => index switch
    {
        0 => slots.Cooldown0,
        1 => slots.Cooldown1,
        2 => slots.Cooldown2,
        3 => slots.Cooldown3,
        _ => 0,
    };

    private static void SetCooldown(ref SkillSlots slots, int index, int value)
    {
        switch (index)
        {
            case 0: slots.Cooldown0 = value; break;
            case 1: slots.Cooldown1 = value; break;
            case 2: slots.Cooldown2 = value; break;
            case 3: slots.Cooldown3 = value; break;
        }
    }
}
