using Arch.Core;
using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Handles melee and ranged combat. All damage is integer.
/// Combat happens when a player/monster tries to move into an occupied tile.
/// </summary>
public class CombatSystem
{
    private readonly List<CombatEvent> _events = new();

    public IReadOnlyList<CombatEvent> LastTickEvents => _events;

    public void Update(Arch.Core.World world)
    {
        _events.Clear();

        // Find all entities that have pending attack actions
        var attackQuery = new QueryDescription().WithAll<Position, PlayerInput, CombatStats>();
        world.Query(in attackQuery, (Entity attacker, ref Position pos, ref PlayerInput input, ref CombatStats stats) =>
        {
            if (input.ActionType != ActionTypes.Attack) return;

            int targetX = pos.X + input.TargetX;
            int targetY = pos.Y + input.TargetY;

            // Capture ref params into locals for inner query lambda
            int attackerAttack = stats.Attack;
            int attackerX = pos.X;
            int attackerY = pos.Y;

            // Find entity at target position
            var targetQuery = new QueryDescription().WithAll<Position, Health, CombatStats>();
            world.Query(in targetQuery, (Entity target, ref Position tPos, ref Health tHealth, ref CombatStats tStats) =>
            {
                if (tPos.X == targetX && tPos.Y == targetY && tHealth.IsAlive)
                {
                    // Damage = attacker's attack - defender's defense (min 1)
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
}

public struct CombatEvent
{
    public int AttackerX, AttackerY;
    public int TargetX, TargetY;
    public int Damage;
    public bool TargetDied;
}
