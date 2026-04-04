using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Entities;

public struct MonsterEntity
{
    public MonsterEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public Health Health;
    public MonsterData MonsterData;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public AIState AI;
    public MoveDelay MoveDelay;
    public AttackDelay AttackDelay;
    public bool IsDead => !Health.IsAlive;
}
