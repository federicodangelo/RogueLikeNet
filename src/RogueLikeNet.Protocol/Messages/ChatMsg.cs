using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class ChatMsg
{
    [Key(0)] public long SenderId { get; set; }
    [Key(1)] public string SenderName { get; set; } = "";
    [Key(2)] public string Text { get; set; } = "";
    [Key(3)] public long Timestamp { get; set; }
}
