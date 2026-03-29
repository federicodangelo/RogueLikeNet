using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class SkillSlotMsg : IEquatable<SkillSlotMsg>
{
    [Key(0)] public int Id { get; set; }
    [Key(1)] public int Cooldown { get; set; }
    [Key(2)] public string Name { get; set; } = "";

    public static bool Equals(SkillSlotMsg? a, SkillSlotMsg? b)
    {
        if (a is null) return b is null;
        if (b is null) return false;
        return a.Id == b.Id && a.Cooldown == b.Cooldown && a.Name == b.Name;
    }

    public bool Equals(SkillSlotMsg? other)
    {
        return Equals(this, other);
    }
}
