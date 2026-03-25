using Arch.Core;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Handles melee and ranged combat. All damage is integer.
/// Melee attacks auto-target the closest adjacent enemy (cardinal + same tile).
/// </summary>
public class CombatSystem
{
    private readonly List<CombatEvent> _events = new();

    // Cardinal directions + same tile for melee auto-targeting
    private static readonly (int DX, int DY)[] MeleeOffsets =
        [(0, 0), (0, -1), (0, 1), (-1, 0), (1, 0)];

    public IReadOnlyList<CombatEvent> LastTickEvents => _events;

    public void Update(Arch.Core.World world)
    {
        _events.Clear();

        var attackQuery = new QueryDescription().WithAll<Position, PlayerInput, CombatStats>();
        world.Query(in attackQuery, (Entity attacker, ref Position pos, ref PlayerInput input, ref CombatStats stats) =>
        {
            if (input.ActionType != ActionTypes.Attack) return;

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
            var targetQuery = new QueryDescription().WithAll<Position, Health, CombatStats>();
            Entity attackerEntity = attacker;
            world.Query(in targetQuery, (Entity target, ref Position tPos, ref Health tHealth, ref CombatStats tStats) =>
            {
                if (target == attackerEntity) return;
                if (tPos.X == targetX && tPos.Y == targetY && tHealth.IsAlive)
                {
                    int damage = Math.Max(1, attackerAttack - tStats.Defense);
                    tHealth.Current = Math.Max(0, tHealth.Current - damage);

                    _events.Add(new CombatEvent
                    {
                        AttackerX = attackerX,
                        AttackerY = attackerY,
                        TargetX = tPos.X,
                        TargetY = tPos.Y,
                        Damage = damage,
                        TargetDied = !tHealth.IsAlive
                    });
                }
            });

            input.ActionType = ActionTypes.None;
        });

        // Mark dead entities
        var deathQuery = new QueryDescription().WithAll<Health>().WithNone<DeadTag>();
        world.Query(in deathQuery, (Entity entity, ref Health health) =>
        {
            if (!health.IsAlive)
            {
                world.Add<DeadTag>(entity);
            }
        });
    }

    private static (int X, int Y)? FindClosestAdjacentTarget(
        Arch.Core.World world, Entity attacker, int ax, int ay)
    {
        (int X, int Y)? best = null;
        int bestDist = int.MaxValue;

        var targetQuery = new QueryDescription().WithAll<Position, Health, CombatStats>();
        world.Query(in targetQuery, (Entity candidate, ref Position cPos, ref Health cHealth) =>
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
