using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class LoginMsg
{
    [Key(0)] public string PlayerName { get; set; } = "";
    [Key(1)] public int ClassId { get; set; }
}
