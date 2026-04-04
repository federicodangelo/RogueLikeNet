using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Entities;

public struct ResourceNodeEntity
{
    public ResourceNodeEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public Health Health;
    public CombatStats CombatStats;
    public TileAppearance Appearance;
    public ResourceNodeData NodeData;
    public AttackDelay AttackDelay;
    public bool IsDead => !Health.IsAlive;
}
