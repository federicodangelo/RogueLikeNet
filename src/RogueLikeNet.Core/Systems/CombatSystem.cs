using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.World;

namespace RogueLikeNet.Core.Systems;

/// <summary>
/// Handles melee combat. All damage is integer.
/// Melee attacks auto-target the closest adjacent enemy (cardinal + same tile).
/// Bumping a town NPC triggers dialogue instead of damage.
/// </summary>
public class CombatSystem
{
    private readonly List<CombatEvent> _events = new();
    private readonly List<NpcDialogueEvent> _dialogueEvents = new();

    private static readonly (int DX, int DY)[] MeleeOffsets =
        [(0, 0), (0, -1), (0, 1), (-1, 0), (1, 0)];

    public IReadOnlyList<CombatEvent> LastTickEvents => _events;
    public IReadOnlyList<NpcDialogueEvent> LastTickDialogueEvents => _dialogueEvents;

    public void Update(WorldMap map, bool debugInvulnerable = false)
    {
        _events.Clear();
        _dialogueEvents.Clear();

        foreach (var player in map.Players.Values)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.Attack) continue;
            if (player.AttackDelay.Current > 0) continue;

            int attackerAttack = player.CombatStats.Attack;
            int attackerX = player.X;
            int attackerY = player.Y;

            int targetX, targetY;
            if (player.Input.TargetX == 0 && player.Input.TargetY == 0)
            {
                var best = FindClosestAdjacentTarget(map, player, attackerX, attackerY);
                if (best == null)
                {
                    player.Input.ActionType = ActionTypes.None;
                    continue;
                }
                targetX = best.Value.X;
                targetY = best.Value.Y;
            }
            else
            {
                targetX = attackerX + player.Input.TargetX;
                targetY = attackerY + player.Input.TargetY;
            }

            // Check monsters and resource nodes at target position
            var chunk = map.GetChunkForWorldPos(targetX, targetY, player.Z);
            if (chunk != null)
            {
                // Check Town NPCs first (dialogue instead of damage)
                foreach (var npc in chunk.TownNpcs)
                {
                    if (npc.IsDead || npc.X != targetX || npc.Y != targetY) continue;
                    if (npc.NpcData.TalkTimer <= 0)
                    {
                        npc.NpcData.TalkTimer = 60;
                        string dialogue = TownNpcDefinitions.Dialogues[npc.NpcData.DialogueIndex];
                        npc.NpcData.DialogueIndex = (npc.NpcData.DialogueIndex + 1) % TownNpcDefinitions.Dialogues.Length;
                        _dialogueEvents.Add(new NpcDialogueEvent
                        {
                            NpcX = npc.X,
                            NpcY = npc.Y,
                            NpcName = npc.NpcData.Name,
                            Text = dialogue,
                        });
                    }
                }

                // Monsters
                foreach (var monster in chunk.Monsters)
                {
                    if (monster.IsDead || monster.X != targetX || monster.Y != targetY) continue;
                    if (!monster.Health.IsAlive) continue;

                    int damage = Math.Max(1, attackerAttack - monster.CombatStats.Defense);
                    monster.Health.Current = Math.Max(0, monster.Health.Current - damage);

                    _events.Add(new CombatEvent
                    {
                        AttackerX = attackerX,
                        AttackerY = attackerY,
                        TargetX = monster.X,
                        TargetY = monster.Y,
                        Damage = damage,
                        TargetDied = !monster.Health.IsAlive
                    });

                    if (!monster.Health.IsAlive)
                        monster.IsDead = true;
                }

                // Resource nodes
                foreach (var node in chunk.ResourceNodes)
                {
                    if (node.IsDead || node.X != targetX || node.Y != targetY) continue;
                    if (!node.Health.IsAlive) continue;

                    int damage = Math.Max(1, attackerAttack - node.CombatStats.Defense);
                    node.Health.Current = Math.Max(0, node.Health.Current - damage);

                    _events.Add(new CombatEvent
                    {
                        AttackerX = attackerX,
                        AttackerY = attackerY,
                        TargetX = node.X,
                        TargetY = node.Y,
                        Damage = damage,
                        TargetDied = !node.Health.IsAlive
                    });

                    if (!node.Health.IsAlive)
                        node.IsDead = true;
                }
            }

            player.Input.ActionType = ActionTypes.None;
            player.AttackDelay.Current = player.AttackDelay.Interval;
        }

        // Monster attacks
        ProcessMonsterAttacks(map, debugInvulnerable);

        // Mark dead entities
        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var m in chunk.Monsters)
                if (!m.IsDead && !m.Health.IsAlive) m.IsDead = true;
            foreach (var r in chunk.ResourceNodes)
                if (!r.IsDead && !r.Health.IsAlive) r.IsDead = true;
        }

        foreach (var p in map.Players.Values)
            if (!p.IsDead && !p.Health.IsAlive) p.IsDead = true;
    }

    private void ProcessMonsterAttacks(WorldMap map, bool debugInvulnerable)
    {
        var players = new List<PlayerEntity>();
        foreach (var p in map.Players.Values)
            if (!p.IsDead && p.Health.IsAlive)
                players.Add(p);

        if (players.Count == 0 || debugInvulnerable) return;

        foreach (var chunk in map.LoadedChunks)
        {
            foreach (var monster in chunk.Monsters)
            {
                if (monster.IsDead || !monster.Health.IsAlive || monster.AI.StateId != AIStates.Attack) continue;
                if (monster.AttackDelay.Current > 0) continue;

                foreach (var player in players)
                {
                    int dx = Math.Abs(monster.X - player.X);
                    int dy = Math.Abs(monster.Y - player.Y);
                    if (dx + dy > 1) continue;
                    if (!player.Health.IsAlive) continue;

                    int damage = Math.Max(1, monster.CombatStats.Attack - player.CombatStats.Defense);
                    player.Health.Current = Math.Max(0, player.Health.Current - damage);

                    _events.Add(new CombatEvent
                    {
                        AttackerX = monster.X,
                        AttackerY = monster.Y,
                        TargetX = player.X,
                        TargetY = player.Y,
                        Damage = damage,
                        TargetDied = !player.Health.IsAlive
                    });
                    monster.AttackDelay.Current = monster.AttackDelay.Interval;
                    break;
                }
            }
        }
    }

    private static (int X, int Y)? FindClosestAdjacentTarget(
        WorldMap map, PlayerEntity attacker, int ax, int ay)
    {
        (int X, int Y)? best = null;
        int bestDist = int.MaxValue;

        var chunk = map.GetChunkForWorldPos(ax, ay, attacker.Z);
        if (chunk == null) return null;

        // Check monsters and resource nodes at adjacent positions
        foreach (var monster in chunk.Monsters)
        {
            if (monster.IsDead || !monster.Health.IsAlive) continue;
            int dx = monster.X - ax;
            int dy = monster.Y - ay;
            bool adjacent = false;
            foreach (var (ox, oy) in MeleeOffsets)
                if (dx == ox && dy == oy) { adjacent = true; break; }
            if (!adjacent) continue;
            int dist = Math.Abs(dx) + Math.Abs(dy);
            if (dist < bestDist) { bestDist = dist; best = (monster.X, monster.Y); }
        }

        foreach (var node in chunk.ResourceNodes)
        {
            if (node.IsDead || !node.Health.IsAlive) continue;
            int dx = node.X - ax;
            int dy = node.Y - ay;
            bool adjacent = false;
            foreach (var (ox, oy) in MeleeOffsets)
                if (dx == ox && dy == oy) { adjacent = true; break; }
            if (!adjacent) continue;
            int dist = Math.Abs(dx) + Math.Abs(dy);
            if (dist < bestDist) { bestDist = dist; best = (node.X, node.Y); }
        }

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
