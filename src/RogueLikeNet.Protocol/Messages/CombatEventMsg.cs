using MessagePack;

namespace RogueLikeNet.Protocol.Messages;

[MessagePackObject]
public class CombatEventMsg
{
    [Key(0)] public int AttackerX { get; set; }
    [Key(1)] public int AttackerY { get; set; }
    [Key(2)] public int TargetX { get; set; }
    [Key(3)] public int TargetY { get; set; }
    [Key(4)] public int Damage { get; set; }
    [Key(5)] public bool TargetDied { get; set; }
    [Key(6)] public bool Blocked { get; set; }
    [Key(7)] public bool IsRanged { get; set; }
    [Key(8)] public int DamageType { get; set; }
    [Key(9)] public bool WasResisted { get; set; }
    [Key(10)] public bool WasWeakness { get; set; }
    [Key(11)] public int StatusEffectType { get; set; } = -1;
}
