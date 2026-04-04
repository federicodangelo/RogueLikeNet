using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Entities;

public struct GroundItemEntity
{
    public GroundItemEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public TileAppearance Appearance;
    public ItemData Item;
    public bool IsDestroyed;
}
