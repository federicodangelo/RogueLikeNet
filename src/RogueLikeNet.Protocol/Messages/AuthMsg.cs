using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class AuthRequestMsg
{
    [Key(0)] public string Username { get; set; } = "";
    [Key(1)] public string PasswordHash { get; set; } = "";
}

[MessagePackObject]
public class AuthResponseMsg
{
    [Key(0)] public bool Success { get; set; }
    [Key(1)] public string Message { get; set; } = "";
    [Key(2)] public long PlayerId { get; set; }
    [Key(3)] public string Token { get; set; } = "";
}

[MessagePackObject]
public class ChatMsg
{
    [Key(0)] public long SenderId { get; set; }
    [Key(1)] public string SenderName { get; set; } = "";
    [Key(2)] public string Text { get; set; } = "";
    [Key(3)] public long Timestamp { get; set; }
}
