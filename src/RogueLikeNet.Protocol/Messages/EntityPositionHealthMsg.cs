using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

/// <summary>
/// Lightweight entity update sent when only position and/or health changed.
/// </summary>
[MessagePackObject]
public class EntityPositionHealthMsg
{
    [Key(0)] public long Id { get; set; }
    [Key(1)] public int X { get; set; }
    [Key(2)] public int Y { get; set; }
    [Key(3)] public int Health { get; set; }
}
