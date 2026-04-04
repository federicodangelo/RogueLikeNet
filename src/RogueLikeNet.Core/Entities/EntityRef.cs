namespace RogueLikeNet.Core.Entities;

public struct EntityRef
{
    public const int NullId = 0;

    public readonly int Id;
    public readonly EntityType Type;

    public EntityRef(int id, EntityType type)
    {
        Id = id;
        Type = type;
    }
}
