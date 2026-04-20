using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class NpcDialogueMsg
{
    [Key(0)] public int NpcX { get; set; }
    [Key(1)] public int NpcY { get; set; }
    [Key(2)] public string NpcName { get; set; } = "";
    [Key(3)] public string Text { get; set; } = "";
    [Key(4)] public int NpcRole { get; set; }
}
