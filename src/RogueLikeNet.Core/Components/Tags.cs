namespace RogueLikeNet.Core.Components;

/// <summary>
/// Tag component marking an entity as a player (vs monster/item).
/// </summary>
public struct PlayerTag
{
    public long ConnectionId;
}

/// <summary>
/// Tag component for monster entities.
/// </summary>
public struct MonsterTag
{
    public int MonsterTypeId;
}

/// <summary>
/// Marks an entity for removal at end of tick.
/// </summary>
public struct DeadTag { }
