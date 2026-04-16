using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class LoginResponseMsg
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public bool IsNewPlayer { get; set; }
    [Key(2)] public string ErrorMessage { get; set; } = "";
}
