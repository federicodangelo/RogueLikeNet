using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Entities;

public struct AnimalEntity
{
    public AnimalEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public Health Health;
    public TileAppearance Appearance;
    public AnimalData AnimalData;
    public AIState AI;
    public MoveDelay MoveDelay;
    public bool IsDead => !Health.IsAlive;
}
