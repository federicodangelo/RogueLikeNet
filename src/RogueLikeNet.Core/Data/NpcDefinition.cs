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
    public int XpReward { get; set; }
    public LootEntry[] LootTable { get; set; } = [];


}

public sealed class LootEntry
{
    public string ItemId { get; set; } = "";
    public int MinCount { get; set; } = 1;
    public int MaxCount { get; set; } = 1;
    public double Chance { get; set; } = 1.0;
}
