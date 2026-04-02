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
        foreach (var player in map.Players.Values)
        {
            if (player.Skills.Cooldown0 > 0) player.Skills.Cooldown0--;
            if (player.Skills.Cooldown1 > 0) player.Skills.Cooldown1--;
            if (player.Skills.Cooldown2 > 0) player.Skills.Cooldown2--;
            if (player.Skills.Cooldown3 > 0) player.Skills.Cooldown3--;
        }
    }

    private void ProcessSkillUse(WorldMap map)
    {
        var actions = new List<(PlayerEntity Player, int SkillSlot, int TargetX, int TargetY)>();

        foreach (var player in map.Players.Values)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.UseSkill) continue;
            actions.Add((player, player.Input.ItemSlot, player.Input.TargetX, player.Input.TargetY));
            player.Input.ActionType = ActionTypes.None;
        }

        foreach (var (player, skillSlot, tx, ty) in actions)
        {
            int skillId = GetSkillId(player, skillSlot);
            if (skillId == SkillDefinitions.None) continue;

            int cooldown = GetCooldown(player, skillSlot);
            if (cooldown > 0) continue;

            int range = SkillDefinitions.GetRange(skillId);
            int targetX = player.X + tx;
            int targetY = player.Y + ty;

            bool used = skillId switch
            {
                SkillDefinitions.PowerStrike => ExecuteMelee(map, player, targetX, targetY, 200),
                SkillDefinitions.ShieldBash => ExecuteMelee(map, player, targetX, targetY, 50),
                SkillDefinitions.Backstab => ExecuteMelee(map, player, targetX, targetY, 300),
                SkillDefinitions.Dodge => ExecuteDodge(player),
                SkillDefinitions.Fireball => ExecuteAoe(map, player, targetX, targetY, 150, 1),
                SkillDefinitions.Heal => ExecuteHeal(player),
                SkillDefinitions.PowerShot => ExecuteRanged(map, player, targetX, targetY, range, 180),
                SkillDefinitions.Trap => false,
                _ => false,
            };

            if (used)
                SetCooldown(player, skillSlot, SkillDefinitions.GetCooldown(skillId));
        }
    }

    private static bool ExecuteMelee(WorldMap map, PlayerEntity attacker, int targetX, int targetY, int damagePercent)
    {
        bool hit = false;
        var chunk = map.GetChunkForWorldPos(targetX, targetY, attacker.Z);
        if (chunk == null) return false;

        foreach (var monster in chunk.Monsters)
        {
            if (monster.IsDead || monster.X != targetX || monster.Y != targetY) continue;
            int baseDamage = Math.Max(1, attacker.CombatStats.Attack - monster.CombatStats.Defense);
            int damage = Math.Max(1, baseDamage * damagePercent / 100);
            monster.Health.Current = Math.Max(0, monster.Health.Current - damage);
            if (!monster.Health.IsAlive) monster.IsDead = true;
            hit = true;
        }

        foreach (var node in chunk.ResourceNodes)
        {
            if (node.IsDead || node.X != targetX || node.Y != targetY) continue;
            int baseDamage = Math.Max(1, attacker.CombatStats.Attack - node.CombatStats.Defense);
            int damage = Math.Max(1, baseDamage * damagePercent / 100);
            node.Health.Current = Math.Max(0, node.Health.Current - damage);
            if (!node.Health.IsAlive) node.IsDead = true;
            hit = true;
        }

        return hit;
    }

    private static bool ExecuteDodge(PlayerEntity player)
    {
        player.CombatStats.Defense += 10;
        return true;
    }

    private static bool ExecuteAoe(WorldMap map, PlayerEntity attacker, int centerX, int centerY, int damagePercent, int radius)
    {
        bool hitAny = false;
        int attackerAttack = attacker.CombatStats.Attack;

        // Check chunks around the center
        var (cx, cy, cz) = Chunk.WorldToChunkCoord(centerX, centerY, attacker.Z);
        for (int dcx = -1; dcx <= 1; dcx++)
            for (int dcy = -1; dcy <= 1; dcy++)
            {
                var chunk = map.TryGetChunk(cx + dcx, cy + dcy, cz);
                if (chunk == null) continue;

                foreach (var monster in chunk.Monsters)
                {
                    if (monster.IsDead) continue;
                    if (Math.Abs(monster.X - centerX) <= radius && Math.Abs(monster.Y - centerY) <= radius)
                    {
                        int baseDamage = Math.Max(1, attackerAttack - monster.CombatStats.Defense);
                        int damage = Math.Max(1, baseDamage * damagePercent / 100);
                        monster.Health.Current = Math.Max(0, monster.Health.Current - damage);
                        if (!monster.Health.IsAlive) monster.IsDead = true;
                        hitAny = true;
                    }
                }

                foreach (var node in chunk.ResourceNodes)
                {
                    if (node.IsDead) continue;
                    if (Math.Abs(node.X - centerX) <= radius && Math.Abs(node.Y - centerY) <= radius)
                    {
                        int baseDamage = Math.Max(1, attackerAttack - node.CombatStats.Defense);
                        int damage = Math.Max(1, baseDamage * damagePercent / 100);
                        node.Health.Current = Math.Max(0, node.Health.Current - damage);
                        if (!node.Health.IsAlive) node.IsDead = true;
                        hitAny = true;
                    }
                }
            }

        return hitAny;
    }

    private static bool ExecuteHeal(PlayerEntity player)
    {
        player.Health.Current = Math.Min(player.Health.Max, player.Health.Current + 30);
        return true;
    }

    private static bool ExecuteRanged(WorldMap map, PlayerEntity attacker, int targetX, int targetY, int maxRange, int damagePercent)
    {
        int dist = Math.Max(Math.Abs(targetX - attacker.X), Math.Abs(targetY - attacker.Y));
        if (dist > maxRange) return false;

        bool hit = false;
        var chunk = map.GetChunkForWorldPos(targetX, targetY, attacker.Z);
        if (chunk == null) return false;

        foreach (var monster in chunk.Monsters)
        {
            if (monster.IsDead || monster.X != targetX || monster.Y != targetY) continue;
            int baseDamage = Math.Max(1, attacker.CombatStats.Attack - monster.CombatStats.Defense);
            int damage = Math.Max(1, baseDamage * damagePercent / 100);
            monster.Health.Current = Math.Max(0, monster.Health.Current - damage);
            if (!monster.Health.IsAlive) monster.IsDead = true;
            hit = true;
        }

        foreach (var node in chunk.ResourceNodes)
        {
            if (node.IsDead || node.X != targetX || node.Y != targetY) continue;
            int baseDamage = Math.Max(1, attacker.CombatStats.Attack - node.CombatStats.Defense);
            int damage = Math.Max(1, baseDamage * damagePercent / 100);
            node.Health.Current = Math.Max(0, node.Health.Current - damage);
            if (!node.Health.IsAlive) node.IsDead = true;
            hit = true;
        }

        return hit;
    }

    private static int GetSkillId(PlayerEntity player, int index) => index switch
    {
        0 => player.Skills.Skill0,
        1 => player.Skills.Skill1,
        2 => player.Skills.Skill2,
        3 => player.Skills.Skill3,
        _ => SkillDefinitions.None,
    };

    private static int GetCooldown(PlayerEntity player, int index) => index switch
    {
        0 => player.Skills.Cooldown0,
        1 => player.Skills.Cooldown1,
        2 => player.Skills.Cooldown2,
        3 => player.Skills.Cooldown3,
        _ => 0,
    };

    private static void SetCooldown(PlayerEntity player, int index, int value)
    {
        switch (index)
        {
            case 0: player.Skills.Cooldown0 = value; break;
            case 1: player.Skills.Cooldown1 = value; break;
            case 2: player.Skills.Cooldown2 = value; break;
            case 3: player.Skills.Cooldown3 = value; break;
        }
    }
}
