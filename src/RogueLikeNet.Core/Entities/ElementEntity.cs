using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Entities;

public struct ElementEntity
{
    public ElementEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public TileAppearance Appearance;
    public LightSource? Light;
}
