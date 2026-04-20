namespace RogueLikeNet.Core.Data;

public enum SpellTargetType
{
    SingleTarget = 0,
    Self = 1,
    AreaOfEffect = 2,
}

public sealed class SpellDefinition : BaseDefinition
{
    public int ManaCost { get; set; }
    public int CooldownTicks { get; set; }
    public DamageType DamageType { get; set; }
    public int BaseDamage { get; set; }
    public int Range { get; set; } = 5;
    public int AoERadius { get; set; }
    public SpellTargetType TargetType { get; set; }
    public int HealAmount { get; set; }
    public int BuffAttack { get; set; }
    public int BuffDefense { get; set; }
    public int BuffDurationTicks { get; set; }
    public int GlyphId { get; set; }
    public int FgColor { get; set; }
}
