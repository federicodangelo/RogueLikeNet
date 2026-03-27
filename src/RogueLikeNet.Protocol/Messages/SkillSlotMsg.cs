using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class SkillSlotMsg
{
    [Key(0)] public int Id { get; set; }
    [Key(1)] public int Cooldown { get; set; }
    [Key(2)] public string Name { get; set; } = "";
}
