using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class PlayerActionEventMsg
{
    [Key(0)] public int EventType { get; set; }
    [Key(1)] public int ItemTypeId { get; set; }
    [Key(2)] public int StackCount { get; set; }
    [Key(3)] public bool Failed { get; set; }
}
