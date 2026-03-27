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
/// Marks an entity for removal at end of tick.
/// </summary>
[Component]
public struct DeadTag { }
