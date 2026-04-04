using RogueLikeNet.Core.Components;
using RogueLikeNet.Core.Definitions;
using RogueLikeNet.Core.Entities;
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

        // Player attacks
        ProcessPlayerAttacks(map);

        // Monster attacks
        ProcessMonsterAttacks(map, debugInvulnerable);
    }

    private void ProcessPlayerAttacks(WorldMap map)
    {
        foreach (ref var player in map.Players)
        {
            if (player.IsDead) continue;
            if (player.Input.ActionType != ActionTypes.Attack) continue;
            if (player.AttackDelay.Current > 0) continue;

            int attackerAttack = player.CombatStats.Attack;

            var targetPosition = player.Position;
            if (player.Input.TargetX == 0 && player.Input.TargetY == 0)
            {
                var best = FindClosestAdjacentTarget(map, player);
                if (best == null)
                {
                    player.Input.ActionType = ActionTypes.None;
                    continue;
                }
                targetPosition = best.Value;
            }
            else
            {
                targetPosition.X += player.Input.TargetX;
                targetPosition.Y += player.Input.TargetY;
            }

            // Check monsters and resource nodes at target position
            var targetChunk = map.GetChunkForWorldPos(targetPosition);
            if (targetChunk != null)
            {
                // Check Town NPCs first (dialogue instead of damage)
                foreach (ref var npc in targetChunk.TownNpcs)
                {
                    if (npc.IsDead || npc.Position != targetPosition) continue;
                    if (npc.NpcData.TalkTimer <= 0)
                    {
                        npc.NpcData.TalkTimer = 60;
                        string dialogue = TownNpcDefinitions.Dialogues[npc.NpcData.DialogueIndex];
                        npc.NpcData.DialogueIndex = (npc.NpcData.DialogueIndex + 1) % TownNpcDefinitions.Dialogues.Length;
                        _dialogueEvents.Add(new NpcDialogueEvent
                        {
                            Npc = npc.Position,
                            NpcName = npc.NpcData.Name,
                            Text = dialogue,
                        });
                    }
                }

                // Monsters
                foreach (ref var monster in targetChunk.Monsters)
                {
                    if (monster.IsDead || monster.Position != targetPosition) continue;

                    int damage = Math.Max(1, attackerAttack - monster.CombatStats.Defense);
                    monster.Health.Current = Math.Max(0, monster.Health.Current - damage);

                    _events.Add(new CombatEvent
                    {
                        Attacker = player.Position,
                        Target = monster.Position,
                        Damage = damage,
                        TargetDied = monster.IsDead
                    });
                }

                // Resource nodes
                foreach (ref var node in targetChunk.ResourceNodes)
                {
                    if (node.IsDead || node.Position != targetPosition) continue;

                    int damage = Math.Max(1, attackerAttack - node.CombatStats.Defense);
                    node.Health.Current = Math.Max(0, node.Health.Current - damage);

                    _events.Add(new CombatEvent
                    {
                        Attacker = player.Position,
                        Target = node.Position,
                        Damage = damage,
                        TargetDied = node.IsDead
                    });
                }
            }

            player.Input.ActionType = ActionTypes.None;
            player.AttackDelay.Current = player.AttackDelay.Interval;
        }
    }

    private void ProcessMonsterAttacks(WorldMap map, bool debugInvulnerable)
    {
        if (debugInvulnerable) return;

        foreach (var chunk in map.LoadedChunks)
        {
            foreach (ref var monster in chunk.Monsters)
            {
                if (monster.IsDead || monster.AI.StateId != AIStates.Attack) continue;
                if (monster.AttackDelay.Current > 0) continue;

                foreach (ref var player in map.Players)
                {
                    if (player.IsDead) continue;

                    int dx = Math.Abs(monster.Position.X - player.Position.X);
                    int dy = Math.Abs(monster.Position.Y - player.Position.Y);
                    if (dx + dy > 1) continue;

                    int damage = Math.Max(1, monster.CombatStats.Attack - player.CombatStats.Defense);
                    player.Health.Current = Math.Max(0, player.Health.Current - damage);

                    _events.Add(new CombatEvent
                    {
                        Attacker = monster.Position,
                        Target = player.Position,
                        Damage = damage,
                        TargetDied = player.IsDead
                    });

                    monster.AttackDelay.Current = monster.AttackDelay.Interval;
                    break;
                }
            }
        }
    }

    private static Position? FindClosestAdjacentTarget(WorldMap map, PlayerEntity attacker)
    {
        Position? best = null;
        int bestDist = int.MaxValue;

        var chunk = map.GetChunkForWorldPos(attacker.Position);
        if (chunk == null) return null;

        // Check monsters and resource nodes at adjacent positions
        foreach (var monster in chunk.Monsters)
        {
            if (monster.IsDead) continue;
            int dx = monster.Position.X - attacker.Position.X;
            int dy = monster.Position.Y - attacker.Position.Y;
            bool adjacent = false;
            foreach (var (ox, oy) in MeleeOffsets)
                if (dx == ox && dy == oy) { adjacent = true; break; }
            if (!adjacent) continue;
            int dist = Math.Abs(dx) + Math.Abs(dy);
            if (dist < bestDist) { bestDist = dist; best = monster.Position; }
        }

        foreach (var node in chunk.ResourceNodes)
        {
            if (node.IsDead) continue;
            int dx = node.Position.X - attacker.Position.X;
            int dy = node.Position.Y - attacker.Position.Y;
            bool adjacent = false;
            foreach (var (ox, oy) in MeleeOffsets)
                if (dx == ox && dy == oy) { adjacent = true; break; }
            if (!adjacent) continue;
            int dist = Math.Abs(dx) + Math.Abs(dy);
            if (dist < bestDist) { bestDist = dist; best = node.Position; }
        }

        return best;
    }
}

public struct CombatEvent
{
    public Position Attacker;
    public Position Target;
    public int Damage;
    public bool TargetDied;
}

public struct NpcDialogueEvent
{
    public Position Npc;
    public string NpcName;
    public string Text;
}
