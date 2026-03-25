using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class ClientInputMsg
{
    [Key(0)] public long Tick { get; set; }
    [Key(1)] public int ActionType { get; set; }
    [Key(2)] public int TargetX { get; set; }
    [Key(3)] public int TargetY { get; set; }
    [Key(4)] public int ItemSlot { get; set; }
    [Key(5)] public int TargetSlot { get; set; }
}
