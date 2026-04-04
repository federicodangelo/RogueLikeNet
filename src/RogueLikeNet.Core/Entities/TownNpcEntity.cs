using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Entities;

public struct TownNpcEntity
{
    public TownNpcEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public Health Health;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public AIState AI;
    public TownNpcTag NpcData;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
    public bool IsDead => !Health.IsAlive;
}
