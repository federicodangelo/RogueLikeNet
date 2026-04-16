using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class ClassSelectMsg
{
    [Key(0)] public int ClassId { get; set; }
}
