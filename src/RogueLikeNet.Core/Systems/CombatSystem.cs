using Arch.Core;
using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Handles melee and ranged combat. All damage is integer.
/// Melee attacks auto-target the closest adjacent enemy (cardinal + same tile).
/// Bumping a town NPC triggers dialogue instead of damage.
/// </summary>
public class CombatSystem
{
    private readonly List<CombatEvent> _events = new();
    private readonly List<NpcDialogueEvent> _dialogueEvents = new();

    // Cardinal directions + same tile for melee auto-targeting
    private static readonly (int DX, int DY)[] MeleeOffsets =
        [(0, 0), (0, -1), (0, 1), (-1, 0), (1, 0)];

    public IReadOnlyList<CombatEvent> LastTickEvents => _events;
    public IReadOnlyList<NpcDialogueEvent> LastTickDialogueEvents => _dialogueEvents;

    public void Update(Arch.Core.World world, bool debugInvulnerable = false)
    {
        _events.Clear();
        _dialogueEvents.Clear();

        world.Query(in GameQueries.PlayerAttack, (Entity attacker, ref Position pos, ref PlayerInput input, ref CombatStats stats, ref AttackDelay delay) =>
        {
            if (input.ActionType != ActionTypes.Attack) return;

            // Respect player attack cooldown — preserve action to execute next tick
            if (delay.Current > 0) return;

            int attackerAttack = stats.Attack;
            int attackerX = pos.X;
            int attackerY = pos.Y;

            // Auto-target: find closest adjacent enemy
            int targetX, targetY;
            if (input.TargetX == 0 && input.TargetY == 0)
            {
                var best = FindClosestAdjacentTarget(world, attacker, attackerX, attackerY);
                if (best == null)
                {
                    input.ActionType = ActionTypes.None;
                    return;
                }
                targetX = best.Value.X;
                targetY = best.Value.Y;
            }
            else
            {
                targetX = attackerX + input.TargetX;
                targetY = attackerY + input.TargetY;
            }

            // Find entity at target position (exclude self)
            Entity attackerEntity = attacker;
            int atkX = attackerX, atkY = attackerY;
            world.Query(in GameQueries.CombatTargets, (Entity target, ref Position tPos, ref Health tHealth, ref CombatStats tStats) =>
            {
                if (target == attackerEntity) return;
                if (tPos.X != targetX || tPos.Y != targetY || !tHealth.IsAlive) return;

                // Town NPC: trigger dialogue instead of damage
                if (world.Has<TownNpcTag>(target))
                {
                    ref var npc = ref world.Get<TownNpcTag>(target);
                    if (npc.TalkTimer <= 0) // Don't re-trigger while already talking
                    {
                        npc.TalkTimer = 60; // ~3 seconds at 20 ticks/sec
                        string dialogue = TownNpcDefinitions.Dialogues[npc.DialogueIndex];
                        npc.DialogueIndex = (npc.DialogueIndex + 1) % TownNpcDefinitions.Dialogues.Length;
                        _dialogueEvents.Add(new NpcDialogueEvent
                        {
                            NpcX = tPos.X,
                            NpcY = tPos.Y,
                            NpcName = npc.Name,
                            Text = dialogue,
                        });
                    }
                    return;
                }

                int damage = Math.Max(1, attackerAttack - tStats.Defense);
                tHealth.Current = Math.Max(0, tHealth.Current - damage);

                int tx2 = tPos.X, ty2 = tPos.Y;
                _events.Add(new CombatEvent
                {
                    AttackerX = atkX,
                    AttackerY = atkY,
                    TargetX = tx2,
                    TargetY = ty2,
                    Damage = damage,
                    TargetDied = !tHealth.IsAlive
                });
            });

            input.ActionType = ActionTypes.None;
            // Reset player attack cooldown after attacking
            delay.Current = delay.Interval;
        });

        // Monster attacks: AI entities in Attack state hit adjacent players
        ProcessMonsterAttacks(world, debugInvulnerable);

        // Mark dead entities
        world.Query(in GameQueries.AliveEntities, (Entity entity, ref Health health) =>
        {
            if (!health.IsAlive)
            {
                world.Add<DeadTag>(entity);
            }
        });
    }

    /// <summary>
    /// Process monster attacks: monsters in Attack state hit an adjacent player each tick.
    /// Called from <see cref="Update"/> after player attacks.
    /// </summary>
    private void ProcessMonsterAttacks(Arch.Core.World world, bool debugInvulnerable)
    {
        // Collect player positions
        var players = new List<(Entity Entity, int X, int Y)>();
        world.Query(in GameQueries.PlayersWithCombat, (Entity entity, ref Position pos, ref Health health) =>
        {
            if (health.IsAlive)
                players.Add((entity, pos.X, pos.Y));
        });
        if (players.Count == 0) return;

        // Skip monster attacks entirely when player is invulnerable
        if (debugInvulnerable) return;

        // Process monster attacks (exclude peaceful town NPCs)
        world.Query(in GameQueries.MonsterAttackers, (Entity monster, ref Position mPos, ref AIState ai, ref CombatStats mStats, ref Health mHealth, ref AttackDelay attackDelay) =>
        {
            if (!mHealth.IsAlive || ai.StateId != AIStates.Attack) return;

            // Respect attack delay
            if (attackDelay.Current > 0) return;

            // Find adjacent player
            foreach (var (playerEntity, px, py) in players)
            {
                int dx = Math.Abs(mPos.X - px);
                int dy = Math.Abs(mPos.Y - py);
                if (dx + dy > 1) continue; // not adjacent

                // Deal damage
                ref var pHealth = ref world.Get<Health>(playerEntity);
                ref var pStats = ref world.Get<CombatStats>(playerEntity);
                if (!pHealth.IsAlive) continue;

                int damage = Math.Max(1, mStats.Attack - pStats.Defense);
                pHealth.Current = Math.Max(0, pHealth.Current - damage);

                _events.Add(new CombatEvent
                {
                    AttackerX = mPos.X,
                    AttackerY = mPos.Y,
                    TargetX = px,
                    TargetY = py,
                    Damage = damage,
                    TargetDied = !pHealth.IsAlive
                });
                // Reset attack delay after attacking
                attackDelay.Current = attackDelay.Interval;
                break; // One attack per monster per tick
            }
        });
    }

    private static (int X, int Y)? FindClosestAdjacentTarget(
        Arch.Core.World world, Entity attacker, int ax, int ay)
    {
        (int X, int Y)? best = null;
        int bestDist = int.MaxValue;

        world.Query(in GameQueries.NonNpcCombatTargets, (Entity candidate, ref Position cPos, ref Health cHealth) =>
        {
            if (candidate == attacker || !cHealth.IsAlive) return;

            int dx = cPos.X - ax;
            int dy = cPos.Y - ay;

            // Check if candidate is at one of the melee offsets
            bool adjacent = false;
            foreach (var (ox, oy) in MeleeOffsets)
            {
                if (dx == ox && dy == oy)
                {
                    adjacent = true;
                    break;
                }
            }
            if (!adjacent) return;

            int dist = Math.Abs(dx) + Math.Abs(dy);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = (cPos.X, cPos.Y);
            }
        });

        return best;
    }
}

public struct CombatEvent
{
    public int AttackerX, AttackerY;
    public int TargetX, TargetY;
    public int Damage;
    public bool TargetDied;
}

public struct NpcDialogueEvent
{
    public int NpcX, NpcY;
    public string NpcName;
    public string Text;
}
