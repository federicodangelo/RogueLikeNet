using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

/// <summary>
/// Sent when an entity is removed (destroyed or left FOV).
/// </summary>
[MessagePackObject]
public struct EntityRemovedMsg
{
    [Key(0)] public long Id { get; set; }
}
