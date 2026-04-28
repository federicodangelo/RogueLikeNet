using System.Text.Json.Serialization;

namespace RogueLikeNet.Core.Data;

/// <summary>
/// Defines a monster/NPC type. Loaded from JSON data files.
/// </summary>
public sealed class NpcDefinition : BaseDefinition
{
    [JsonConverter(typeof(GlyphConverter))]
    public int GlyphId { get; set; }
    public int FgColor { get; set; }
    public int Health { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Speed { get; set; }
    public int AttackSpeed { get; set; }
    public int XpReward { get; set; }
    public DamageModifierDefinition[] DamageModifiers { get; set; } = [];
    public LootEntry[] LootTable { get; set; } = [];

    public int GetDamageMultiplierBase100(DamageType damageType)
    {
        for (int i = 0; i < DamageModifiers.Length; i++)
        {
            if (DamageModifiers[i].DamageType == damageType)
                return DamageModifiers[i].MultiplierBase100;
        }

        return 100;
    }
}

public sealed class DamageModifierDefinition
{
    public DamageType DamageType { get; set; }
    public int MultiplierBase100 { get; set; } = 100;
}

public sealed class LootEntry
{
    public string ItemId { get; set; } = "";
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = 1;
    public double Chance { get; set; } = 1.0;
}
