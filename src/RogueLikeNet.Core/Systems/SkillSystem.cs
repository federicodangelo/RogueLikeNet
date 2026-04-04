using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Processes skill usage and cooldown ticking.
/// </summary>
public class SkillSystem
{
    public void Update(WorldMap map)
    {
        TickCooldowns(map);
        ProcessSkillUse(map);
    }

    private void TickCooldowns(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.Skills.Cooldown0 > 0) player.Skills.Cooldown0--;
            if (player.Skills.Cooldown1 > 0) player.Skills.Cooldown1--;
            if (player.Skills.Cooldown2 > 0) player.Skills.Cooldown2--;
            if (player.Skills.Cooldown3 > 0) player.Skills.Cooldown3--;
        }
    }

    private void ProcessSkillUse(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.UseSkill) continue;

            var skillSlot = player.Input.ItemSlot;
            var tx = player.Input.TargetX;
            var ty = player.Input.TargetY;

            int skillId = player.Skills.GetSkillId(skillSlot);
            if (skillId == SkillDefinitions.None) continue;

            int cooldown = player.Skills.GetCooldown(skillSlot);
            if (cooldown > 0) continue;

            int range = SkillDefinitions.GetRange(skillId);
            int targetX = player.Position.X + tx;
            int targetY = player.Position.Y + ty;

            bool used = skillId switch
            {
                SkillDefinitions.PowerStrike => ExecuteMelee(map, ref player, targetX, targetY, 200),
                SkillDefinitions.ShieldBash => ExecuteMelee(map, ref player, targetX, targetY, 50),
                SkillDefinitions.Backstab => ExecuteMelee(map, ref player, targetX, targetY, 300),
                SkillDefinitions.Dodge => ExecuteDodge(ref player),
                SkillDefinitions.Fireball => ExecuteAoe(map, ref player, targetX, targetY, 150, 1),
                SkillDefinitions.Heal => ExecuteHeal(ref player),
                SkillDefinitions.PowerShot => ExecuteRanged(map, ref player, targetX, targetY, range, 180),
                SkillDefinitions.Trap => false,
                _ => false,
            };

            if (used)
                player.Skills.SetCooldown(skillSlot, SkillDefinitions.GetCooldown(skillId));
        }
    }

    private static bool ExecuteMelee(WorldMap map, ref PlayerEntity attacker, int targetX, int targetY, int damagePercent)
    {
        bool hit = false;
        var chunk = map.GetChunkForWorldPos(targetX, targetY, attacker.Position.Z);
        if (chunk == null) return false;

        foreach (ref var monster in chunk.Monsters)
        {
            if (monster.IsDead || monster.Position.X != targetX || monster.Position.Y != targetY) continue;
            int baseDamage = Math.Max(1, attacker.CombatStats.Attack - monster.CombatStats.Defense);
            int damage = Math.Max(1, baseDamage * damagePercent / 100);
            monster.Health.Current = Math.Max(0, monster.Health.Current - damage);
            hit = true;
        }

        foreach (ref var node in chunk.ResourceNodes)
        {
            if (node.IsDead || node.Position.X != targetX || node.Position.Y != targetY) continue;
            int baseDamage = Math.Max(1, attacker.CombatStats.Attack - node.CombatStats.Defense);
            int damage = Math.Max(1, baseDamage * damagePercent / 100);
            node.Health.Current = Math.Max(0, node.Health.Current - damage);
            hit = true;
        }

        return hit;
    }

    private static bool ExecuteDodge(ref PlayerEntity player)
    {
        player.CombatStats.Defense += 10;
        return true;
    }

    private static bool ExecuteAoe(WorldMap map, ref PlayerEntity attacker, int centerX, int centerY, int damagePercent, int radius)
    {
        bool hitAny = false;
        int attackerAttack = attacker.CombatStats.Attack;

        // Check chunks around the center
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(centerX, centerY, attacker.Position.Z);
        for (int dcx = -1; dcx <= 1; dcx++)
        {
            for (int dcy = -1; dcy <= 1; dcy++)
            {
                var chunk = map.TryGetChunk(cx + dcx, cy + dcy, cz);
                if (chunk == null) continue;

                foreach (ref var monster in chunk.Monsters)
                {
                    if (monster.IsDead) continue;
                    if (Math.Abs(monster.Position.X - centerX) <= radius && Math.Abs(monster.Position.Y - centerY) <= radius)
                    {
                        int baseDamage = Math.Max(1, attackerAttack - monster.CombatStats.Defense);
                        int damage = Math.Max(1, baseDamage * damagePercent / 100);
                        monster.Health.Current = Math.Max(0, monster.Health.Current - damage);
                        hitAny = true;
                    }
                }

                foreach (ref var node in chunk.ResourceNodes)
                {
                    if (node.IsDead) continue;
                    if (Math.Abs(node.Position.X - centerX) <= radius && Math.Abs(node.Position.Y - centerY) <= radius)
                    {
                        int baseDamage = Math.Max(1, attackerAttack - node.CombatStats.Defense);
                        int damage = Math.Max(1, baseDamage * damagePercent / 100);
                        node.Health.Current = Math.Max(0, node.Health.Current - damage);
                        hitAny = true;
                    }
                }
            }
        }

        return hitAny;
    }

    private static bool ExecuteHeal(ref PlayerEntity player)
    {
        player.Health.Current = Math.Min(player.Health.Max, player.Health.Current + 30);
        return true;
    }

    private static bool ExecuteRanged(WorldMap map, ref PlayerEntity attacker, int targetX, int targetY, int maxRange, int damagePercent)
    {
        int dist = Math.Max(Math.Abs(targetX - attacker.Position.X), Math.Abs(targetY - attacker.Position.Y));
        if (dist > maxRange) return false;

        bool hit = false;
        var chunk = map.GetChunkForWorldPos(targetX, targetY, attacker.Position.Z);
        if (chunk == null) return false;

        foreach (ref var monster in chunk.Monsters)
        {
            if (monster.IsDead || monster.Position.X != targetX || monster.Position.Y != targetY) continue;
            int baseDamage = Math.Max(1, attacker.CombatStats.Attack - monster.CombatStats.Defense);
            int damage = Math.Max(1, baseDamage * damagePercent / 100);
            monster.Health.Current = Math.Max(0, monster.Health.Current - damage);
            hit = true;
        }

        foreach (ref var node in chunk.ResourceNodes)
        {
            if (node.IsDead || node.Position.X != targetX || node.Position.Y != targetY) continue;
            int baseDamage = Math.Max(1, attacker.CombatStats.Attack - node.CombatStats.Defense);
            int damage = Math.Max(1, baseDamage * damagePercent / 100);
            node.Health.Current = Math.Max(0, node.Health.Current - damage);
            hit = true;
        }

        return hit;
    }
}
