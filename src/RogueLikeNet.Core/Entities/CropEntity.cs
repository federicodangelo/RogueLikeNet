using RogueLikeNet.Core.Components;

namespace RogueLikeNet.Core.Entities;

public struct CropEntity
{
    public CropEntity(int id) { Id = id; }
    public readonly int Id;
    public Position Position;
    public TileAppearance Appearance;
    public CropData CropData;
    public bool IsDestroyed;
}
