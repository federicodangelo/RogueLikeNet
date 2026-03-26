using Arch.AOT.SourceGenerator;

namespace RogueLikeNet.Core.Components;

/// <summary>
/// Tag component marking an entity as a player (vs monster/item).
/// </summary>
[Component]
public struct PlayerTag
{
    public long ConnectionId;
}

/// <summary>
/// Tag component for monster entities.
/// </summary>
[Component]
public struct MonsterTag
{
    public int MonsterTypeId;
}

/// <summary>
/// Marks an entity for removal at end of tick.
/// </summary>
[Component]
public struct DeadTag { }

/// <summary>
/// Marks an item entity that is lying on the ground (not in any inventory).
/// </summary>
[Component]
public struct GroundItemTag { }
